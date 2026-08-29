using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class MultiplayerParticipantCubes : MonoBehaviour
{
    private const float MinimumWidth = 0.5f;
    private const float MaximumWidth = 2.7f;
    private const float MinimumHeight = 0.5f;
    private const float MaximumHeight = 2.7f;
    private const float CubeDepth = 0.35f;
    private const float DistanceFromCamera = 9f;
    private const float ParticipantSpacing = 3.2f;
    private const float FollowSharpness = 12f;

    [Serializable]
    private sealed class ParticipantState
    {
        public string userId;
        public string username;
        public float mouseX;
        public float mouseY;
    }

    [Serializable]
    private sealed class MultiplayerSnapshot
    {
        public string selfUserId;
        public ParticipantState[] participants = Array.Empty<ParticipantState>();
    }

    private sealed class CubeVisual
    {
        public GameObject GameObject;
        public Material Material;
        public string Username;
        public Vector3 TargetScale;
    }

    private static MultiplayerParticipantCubes _instance;
    private readonly Dictionary<string, CubeVisual> _cubes = new();
    private readonly List<string> _orderedUserIds = new();
    private readonly List<string> _removalBuffer = new();
    private Camera _camera;
    private GUIStyle _labelStyle;
    private string _selfUserId = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateVisualizer()
    {
        EnsureInstance();
    }

    public static void ReceiveSnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        EnsureInstance();
        _instance?.ApplySnapshot(json);
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
        {
            return;
        }

        _instance = FindAnyObjectByType<MultiplayerParticipantCubes>();

        if (_instance != null)
        {
            return;
        }

        var visualizerObject = new GameObject(nameof(MultiplayerParticipantCubes));
        DontDestroyOnLoad(visualizerObject);
        _instance = visualizerObject.AddComponent<MultiplayerParticipantCubes>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void ApplySnapshot(string json)
    {
        MultiplayerSnapshot snapshot;

        try
        {
            snapshot = JsonUtility.FromJson<MultiplayerSnapshot>(json);
        }
        catch (Exception error)
        {
            Debug.LogWarning($"[ParticipantCubes] Invalid snapshot: {error.Message}");
            return;
        }

        if (snapshot == null)
        {
            return;
        }

        snapshot.participants ??= Array.Empty<ParticipantState>();
        _selfUserId = snapshot.selfUserId ?? string.Empty;
        _orderedUserIds.Clear();
        var activeUserIds = new HashSet<string>();

        foreach (var participant in snapshot.participants)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.userId))
            {
                continue;
            }

            var userId = participant.userId;
            activeUserIds.Add(userId);
            _orderedUserIds.Add(userId);

            if (!_cubes.TryGetValue(userId, out var visual))
            {
                visual = CreateCube(userId);
                _cubes.Add(userId, visual);
            }

            visual.Username = string.IsNullOrWhiteSpace(participant.username)
                ? "Unknown"
                : participant.username;
            visual.GameObject.name = $"Player Cube - {visual.Username}";
            visual.TargetScale = new Vector3(
                Mathf.Lerp(MinimumWidth, MaximumWidth, Mathf.Clamp01(participant.mouseX)),
                Mathf.Lerp(MinimumHeight, MaximumHeight, Mathf.Clamp01(participant.mouseY)),
                CubeDepth
            );
        }

        _removalBuffer.Clear();

        foreach (var userId in _cubes.Keys)
        {
            if (!activeUserIds.Contains(userId))
            {
                _removalBuffer.Add(userId);
            }
        }

        foreach (var userId in _removalBuffer)
        {
            RemoveCube(userId);
        }
    }

    private CubeVisual CreateCube(string userId)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(transform, true);
        cube.transform.localScale = new Vector3(
            MinimumWidth,
            MinimumHeight,
            CubeDepth
        );

        var collider = cube.GetComponent<Collider>();

        if (collider != null)
        {
            Destroy(collider);
        }

        var renderer = cube.GetComponent<Renderer>();
        Material material = null;

        if (renderer != null && renderer.sharedMaterial != null)
        {
            material = new Material(renderer.sharedMaterial)
            {
                color = ColorForUser(userId),
            };
            renderer.sharedMaterial = material;
        }

        return new CubeVisual
        {
            GameObject = cube,
            Material = material,
            Username = "Connecting...",
            TargetScale = cube.transform.localScale,
        };
    }

    private void RemoveCube(string userId)
    {
        if (!_cubes.Remove(userId, out var visual))
        {
            return;
        }

        if (visual.Material != null)
        {
            Destroy(visual.Material);
        }

        if (visual.GameObject != null)
        {
            Destroy(visual.GameObject);
        }
    }

    private void LateUpdate()
    {
        if (_cubes.Count == 0)
        {
            return;
        }

        if (_camera == null || !_camera.isActiveAndEnabled)
        {
            _camera = Camera.main;
        }

        if (_camera == null)
        {
            return;
        }

        var count = _orderedUserIds.Count;
        var firstX = -(count - 1) * ParticipantSpacing * 0.5f;
        var smoothing = 1f - Mathf.Exp(-FollowSharpness * Time.unscaledDeltaTime);

        for (var index = 0; index < count; index++)
        {
            if (!_cubes.TryGetValue(_orderedUserIds[index], out var visual))
            {
                continue;
            }

            var cameraLocalPosition = new Vector3(
                firstX + index * ParticipantSpacing,
                -0.35f,
                DistanceFromCamera
            );
            var targetPosition = _camera.transform.TransformPoint(cameraLocalPosition);
            var cubeTransform = visual.GameObject.transform;

            cubeTransform.position = Vector3.Lerp(
                cubeTransform.position,
                targetPosition,
                smoothing
            );
            cubeTransform.rotation = _camera.transform.rotation;
            cubeTransform.localScale = Vector3.Lerp(
                cubeTransform.localScale,
                visual.TargetScale,
                smoothing
            );
        }
    }

    private void OnGUI()
    {
        if (_camera == null || _cubes.Count == 0)
        {
            return;
        }

        EnsureLabelStyle();

        foreach (var userId in _orderedUserIds)
        {
            if (!_cubes.TryGetValue(userId, out var visual))
            {
                continue;
            }

            var cubeTransform = visual.GameObject.transform;
            var worldLabelPosition =
                cubeTransform.position
                + _camera.transform.up * (cubeTransform.localScale.y * 0.5f + 0.25f);
            var screenPosition = _camera.WorldToScreenPoint(worldLabelPosition);

            if (screenPosition.z <= 0f)
            {
                continue;
            }

            var label = userId == _selfUserId
                ? $"YOU • {visual.Username}"
                : visual.Username;
            var labelRect = new Rect(
                screenPosition.x - 100f,
                Screen.height - screenPosition.y - 12f,
                200f,
                24f
            );
            GUI.Label(labelRect, label, _labelStyle);
        }
    }

    private void EnsureLabelStyle()
    {
        if (_labelStyle != null)
        {
            return;
        }

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
    }

    private static Color ColorForUser(string userId)
    {
        uint hash = 2166136261;

        foreach (var character in userId)
        {
            hash ^= character;
            hash *= 16777619;
        }

        var hue = hash % 360 / 360f;
        return Color.HSVToRGB(hue, 0.72f, 0.95f);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
