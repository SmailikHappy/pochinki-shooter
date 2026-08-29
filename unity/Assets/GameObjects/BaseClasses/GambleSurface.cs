using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class GambleSurface : MonoBehaviour
{
    private const string GeneratedRootName = "Generated Board";

    [Header("Prefabs")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private GameObject pegPrefab;
    [SerializeField] private GameObject slotPrefab;

    [Header("Peg Grid")]
    [SerializeField, Min(2)] private int rows = 8;
    [SerializeField, Min(2)] private int columns = 7;
    [SerializeField, Min(0.2f)] private float horizontalSpacing = 1.15f;
    [SerializeField, Min(0.2f)] private float verticalSpacing = 0.85f;
    [SerializeField, Min(0.05f)] private float pegDiameter = 0.28f;

    [Header("Slots")]
    [SerializeField, Min(2)] private int slotCount = 7;
    [SerializeField, Min(0.3f)] private float slotHeight = 1.35f;
    [SerializeField] private GambleReward[] slotRewards =
    {
        new(GambleRewardType.Ammo, 1),
        new(GambleRewardType.Ammo, 2),
        new(GambleRewardType.Ammo, 3),
        new(GambleRewardType.Ammo, 5),
        new(GambleRewardType.Ammo, 3),
        new(GambleRewardType.Ammo, 2),
        new(GambleRewardType.Ammo, 1),
    };

    [Header("Runtime")]
    [SerializeField] private bool generateOnPlay = true;
    [SerializeField] private bool allowSpaceDrop = true;
    [SerializeField, Min(1)] private int maxActiveBalls = 3;
    [SerializeField, Min(0f)] private float dropCooldown = 0.25f;

    private readonly List<Ball> _activeBalls = new();
    private Transform _generatedRoot;
    private Transform _ballRoot;
    private float _nextDropTime;
    private string _lastResult = "Press SPACE or click DROP";
    private int _ammoWon;
    private GUIStyle _titleStyle;
    private GUIStyle _bodyStyle;

    public event Action<GambleReward, int> BallResolved;

    public int AmmoWon => _ammoWon;
    public string LastResult => _lastResult;

    private float BoardWidth => (columns - 1) * horizontalSpacing + 2f;
    private float TopPegY => (rows - 1) * verticalSpacing * 0.5f;
    private float BottomPegY => -TopPegY;
    private float SpawnY => TopPegY + 1.25f;
    private float SlotTopY => BottomPegY - 0.95f;
    private float DespawnY => SlotTopY - slotHeight - 1.5f;

    private void Awake()
    {
        if (Application.isPlaying && generateOnPlay)
        {
            RebuildBoard();
        }
    }

    private void Update()
    {
        if (!allowSpaceDrop)
        {
            return;
        }

        var keyboard = Keyboard.current;

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            DropBall();
        }
    }

    private void OnValidate()
    {
        rows = Mathf.Max(2, rows);
        columns = Mathf.Max(2, columns);
        slotCount = Mathf.Max(2, slotCount);
        horizontalSpacing = Mathf.Max(0.2f, horizontalSpacing);
        verticalSpacing = Mathf.Max(0.2f, verticalSpacing);
        pegDiameter = Mathf.Max(0.05f, pegDiameter);
        slotHeight = Mathf.Max(0.3f, slotHeight);
        maxActiveBalls = Mathf.Max(1, maxActiveBalls);
        dropCooldown = Mathf.Max(0f, dropCooldown);
    }

    [ContextMenu("Generate Board")]
    public void RebuildBoard()
    {
        ClearBoard();
        EnsureRewards();

        var rootObject = new GameObject(GeneratedRootName);
        rootObject.transform.SetParent(transform, false);
        _generatedRoot = rootObject.transform;

        var pegRoot = CreateContainer("Pegs");
        var wallRoot = CreateContainer("Walls");
        var slotRoot = CreateContainer("Slots");
        _ballRoot = CreateContainer("Balls");

        CreateBackground();
        CreatePegs(pegRoot);
        CreateWalls(wallRoot);
        CreateSlots(slotRoot);

        var spawnPoint = new GameObject("BallSpawnPoint");
        spawnPoint.transform.SetParent(_generatedRoot, false);
        spawnPoint.transform.localPosition = new Vector3(0f, SpawnY, 0f);
    }

    [ContextMenu("Clear Board")]
    public void ClearBoard()
    {
        _activeBalls.Clear();
        _ballRoot = null;

        var existingRoot = transform.Find(GeneratedRootName);

        if (existingRoot == null)
        {
            _generatedRoot = null;
            return;
        }

        existingRoot.gameObject.SetActive(false);

        if (Application.isPlaying)
        {
            Destroy(existingRoot.gameObject);
        }
        else
        {
            DestroyImmediate(existingRoot.gameObject);
        }

        _generatedRoot = null;
    }

    public void DropBall()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Gamble] Enter Play Mode before dropping a ball.", this);
            return;
        }

        _activeBalls.RemoveAll(ball => ball == null);

        if (_generatedRoot == null || _ballRoot == null)
        {
            RebuildBoard();
        }

        if (_activeBalls.Count >= maxActiveBalls)
        {
            _lastResult = $"Wait: {_activeBalls.Count} balls are still falling";
            return;
        }

        if (Time.time < _nextDropTime)
        {
            return;
        }

        _nextDropTime = Time.time + dropCooldown;
        var ballObject = CreateBallObject();
        ballObject.name = $"Ball {_activeBalls.Count + 1}";
        ballObject.transform.SetParent(_ballRoot, false);
        ballObject.transform.localPosition = new Vector3(UnityEngine.Random.Range(-0.12f, 0.12f), SpawnY, 0f);

        var ball = ballObject.GetComponent<Ball>();

        if (ball == null)
        {
            ball = ballObject.AddComponent<Ball>();
        }

        ball.Configure(this, DespawnY);
        var body = ballObject.GetComponent<Rigidbody>();

        if (body != null)
        {
            body.linearVelocity = new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), 0f, 0f);
            body.angularVelocity = Vector3.zero;
        }

        _activeBalls.Add(ball);
        _lastResult = "Ball dropped...";
        Debug.Log("[Gamble] Ball dropped.", this);
    }

    public void ResolveBall(Ball ball, GambleReward reward, int slotIndex)
    {
        if (ball == null)
        {
            return;
        }

        _activeBalls.Remove(ball);

        if (reward.Type == GambleRewardType.Ammo)
        {
            _ammoWon += reward.Value;
        }

        _lastResult = $"Ball landed in slot {slotIndex + 1}: {reward.Label}";
        Debug.Log($"[Gamble] {_lastResult}", this);
        BallResolved?.Invoke(reward, slotIndex);
        Destroy(ball.gameObject);
    }

    public void HandleMissedBall(Ball ball)
    {
        if (ball == null || !ball.TryResolve())
        {
            return;
        }

        _activeBalls.Remove(ball);
        _lastResult = "Ball missed every slot";
        Debug.LogWarning("[Gamble] Ball missed every slot and was cleaned up.", this);
        Destroy(ball.gameObject);
    }

    private Transform CreateContainer(string name)
    {
        var container = new GameObject(name);
        container.transform.SetParent(_generatedRoot, false);
        return container.transform;
    }

    private void CreateBackground()
    {
        var height = SpawnY - DespawnY;
        var background = CreatePrimitive(
            PrimitiveType.Cube,
            "Background",
            _generatedRoot,
            new Vector3(0f, (SpawnY + DespawnY) * 0.5f, 0.7f),
            new Vector3(BoardWidth + 0.8f, height + 0.4f, 0.2f),
            new Color(0.08f, 0.11f, 0.18f)
        );
        RemoveCollider(background);
    }

    private void CreatePegs(Transform parent)
    {
        for (var row = 0; row < rows; row++)
        {
            var pegCount = row % 2 == 0 ? columns : columns - 1;
            var rowWidth = (pegCount - 1) * horizontalSpacing;
            var y = TopPegY - row * verticalSpacing;

            for (var column = 0; column < pegCount; column++)
            {
                var x = -rowWidth * 0.5f + column * horizontalSpacing;
                var peg = CreatePrefabOrPrimitive(pegPrefab, PrimitiveType.Sphere, $"Peg {row + 1}-{column + 1}");
                peg.transform.SetParent(parent, false);
                peg.transform.localPosition = new Vector3(x, y, 0f);
                peg.transform.localScale = Vector3.one * pegDiameter;
                ApplyColor(peg, new Color(0.28f, 0.72f, 1f));

                var body = peg.GetComponent<Rigidbody>();

                if (body != null)
                {
                    DestroyComponent(body);
                }
            }
        }
    }

    private void CreateWalls(Transform parent)
    {
        var left = -BoardWidth * 0.5f;
        var right = BoardWidth * 0.5f;
        var wallBottom = SlotTopY - slotHeight;
        var wallHeight = SpawnY - wallBottom + 0.5f;
        var wallY = (SpawnY + wallBottom) * 0.5f;
        const float thickness = 0.28f;

        CreatePrimitive(
            PrimitiveType.Cube,
            "LeftWall",
            parent,
            new Vector3(left, wallY, 0f),
            new Vector3(thickness, wallHeight, 1f),
            new Color(0.24f, 0.3f, 0.42f)
        );
        CreatePrimitive(
            PrimitiveType.Cube,
            "RightWall",
            parent,
            new Vector3(right, wallY, 0f),
            new Vector3(thickness, wallHeight, 1f),
            new Color(0.24f, 0.3f, 0.42f)
        );
    }

    private void CreateSlots(Transform parent)
    {
        var left = -BoardWidth * 0.5f;
        var slotWidth = BoardWidth / slotCount;
        const float dividerThickness = 0.16f;

        for (var dividerIndex = 0; dividerIndex <= slotCount; dividerIndex++)
        {
            var dividerX = left + dividerIndex * slotWidth;
            CreatePrimitive(
                PrimitiveType.Cube,
                $"Divider {dividerIndex}",
                parent,
                new Vector3(dividerX, SlotTopY - slotHeight * 0.5f, 0f),
                new Vector3(dividerThickness, slotHeight, 1f),
                new Color(0.3f, 0.36f, 0.48f)
            );
        }

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var reward = RewardForSlot(slotIndex);
            var slot = CreatePrefabOrPrimitive(slotPrefab, PrimitiveType.Cube, $"Slot {slotIndex + 1} - {reward.Label}");
            slot.transform.SetParent(parent, false);
            slot.transform.localPosition = new Vector3(
                left + (slotIndex + 0.5f) * slotWidth,
                SlotTopY - slotHeight + 0.18f,
                0f
            );
            slot.transform.localScale = new Vector3(slotWidth - dividerThickness, 0.34f, 0.85f);
            ApplyColor(slot, RewardColor(reward.Value));

            var collider = slot.GetComponent<BoxCollider>();

            if (collider == null)
            {
                collider = slot.AddComponent<BoxCollider>();
            }

            collider.isTrigger = true;
            var gambleSlot = slot.GetComponent<GambleSlot>();

            if (gambleSlot == null)
            {
                gambleSlot = slot.AddComponent<GambleSlot>();
            }

            gambleSlot.Configure(this, slotIndex, reward);
        }
    }

    private GameObject CreateBallObject()
    {
        if (ballPrefab != null)
        {
            return Instantiate(ballPrefab);
        }

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.transform.localScale = Vector3.one * 0.5f;
        ball.AddComponent<Rigidbody>();
        ball.AddComponent<Ball>();
        ApplyColor(ball, new Color(1f, 0.45f, 0.18f));
        return ball;
    }

    private GameObject CreatePrefabOrPrimitive(GameObject prefab, PrimitiveType fallbackType, string objectName)
    {
        var instance = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(fallbackType);
        instance.name = objectName;
        return instance;
    }

    private GameObject CreatePrimitive(
        PrimitiveType primitiveType,
        string objectName,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Color color
    )
    {
        var instance = GameObject.CreatePrimitive(primitiveType);
        instance.name = objectName;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = position;
        instance.transform.localScale = scale;
        ApplyColor(instance, color);
        return instance;
    }

    private static void ApplyColor(GameObject target, Color color)
    {
        var renderer = target.GetComponentInChildren<Renderer>();

        if (renderer == null)
        {
            return;
        }

        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }

    private static void RemoveCollider(GameObject target)
    {
        var collider = target.GetComponent<Collider>();

        if (collider != null)
        {
            DestroyComponent(collider);
        }
    }

    private static void DestroyComponent(Component component)
    {
        if (Application.isPlaying)
        {
            Destroy(component);
        }
        else
        {
            DestroyImmediate(component);
        }
    }

    private void EnsureRewards()
    {
        if (slotRewards != null && slotRewards.Length == slotCount)
        {
            return;
        }

        slotRewards = new GambleReward[slotCount];

        for (var index = 0; index < slotCount; index++)
        {
            var distanceFromCenter = Mathf.Abs(index - (slotCount - 1) * 0.5f);
            var value = distanceFromCenter < 0.5f ? 5 : distanceFromCenter < 1.5f ? 3 : distanceFromCenter < 2.5f ? 2 : 1;
            slotRewards[index] = new GambleReward(GambleRewardType.Ammo, value);
        }
    }

    private GambleReward RewardForSlot(int slotIndex)
    {
        EnsureRewards();
        return slotRewards[Mathf.Clamp(slotIndex, 0, slotRewards.Length - 1)];
    }

    private static Color RewardColor(int value)
    {
        return value switch
        {
            >= 5 => new Color(1f, 0.72f, 0.15f),
            >= 3 => new Color(0.72f, 0.35f, 1f),
            >= 2 => new Color(0.2f, 0.8f, 0.55f),
            _ => new Color(0.25f, 0.55f, 1f),
        };
    }

    private void OnGUI()
    {
        EnsureGuiStyles();
        var panel = new Rect(Screen.width - 270f, 20f, 250f, 154f);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, 220f, 28f), "GAMBLE BOARD", _titleStyle);
        GUI.Label(new Rect(panel.x + 14f, panel.y + 42f, 220f, 44f), $"Ammo won: {_ammoWon}\n{_lastResult}", _bodyStyle);

        if (GUI.Button(new Rect(panel.x + 14f, panel.y + 100f, 222f, 38f), "DROP  [SPACE]"))
        {
            DropBall();
        }
    }

    private void EnsureGuiStyles()
    {
        if (_titleStyle != null)
        {
            return;
        }

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = new Color(0.85f, 0.9f, 1f) },
        };
    }
}
