using System.Collections.Generic;
using Pochinki.Networking.Game;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GameSurfaceRenderer))]
public class GameSurface : MonoBehaviour
{
    private const string PixelParentName = "PixelParent";
    private const string CanonParentName = "CanonParent";

    [Header("Grid Settings")]
    [SerializeField] private int rows = 5;
    [SerializeField] private int columns = 5;
    [Tooltip("Fixed X/Z size of the field. Grid dimensions change density, not the field footprint.")]
    [SerializeField] private Vector2 fieldWorldSize = new Vector2(30f, 30f);
    [Tooltip("Visual spacing ratio. A larger value makes the rendered pixels smaller without changing the field footprint.")]
    [SerializeField, Min(0.01f)] private float spacingRatioX = 1f;
    [SerializeField, Min(0.01f)] private float spacingRatioZ = 1f;
    private float cellPitchX;
    private float cellPitchZ;
    private Vector3 effectivePixelScale;

    [Header("Runtime Rendering")]
    [Tooltip("Batches Pixel visuals in player builds while keeping the ordinary Pixel prefab as the authoring source.")]
    [SerializeField] private GameSurfaceRenderer surfaceRenderer;
    [SerializeField] private bool useInstancedRenderingInPlayer = true;

    [Header("Prefab Settings")]
    [SerializeField] private GameObject pixelPrefab;
    [SerializeField] private Vector3 pixelSpawnScale = new Vector3(1f, 1f, 1f);
    [SerializeField] private GameObject canonPrefab;
    [SerializeField] private Vector3 canonSpawnScale = new Vector3(1f, 1f, 1f);
    [Tooltip("Смещение пушки по Y относительно её пикселя — чтобы стоять сверху, а не внутри столбика.")]
    [SerializeField] private float canonHeightOffset = 1.5f;

    [Header("Territory Preset")]
    [Tooltip("Размер стартовой территории игрока вокруг его угла, в клетках (3 = квадрат 3x3).")]
    [SerializeField, Min(1)] private int startingTerritorySize = 3;

    private GameObject pixelParent;
    private GameObject canonParent;
    private readonly Transform[] cornerPixelTransforms = new Transform[4];

    private readonly Dictionary<Player, Canon> spawnedCanons = new();
    private readonly List<Pixel> spawnedPixels = new();
    public IReadOnlyDictionary<Player, Canon> SpawnedCanons => spawnedCanons;
    public IReadOnlyList<Pixel> SpawnedPixels => spawnedPixels;

    private void Awake()
    {
        if (surfaceRenderer == null)
            surfaceRenderer = GetComponent<GameSurfaceRenderer>();
    }

    public bool SpawnGrid(IReadOnlyList<Player> players, IReadOnlyList<int> playerSlots = null)
    {
        if (pixelPrefab == null)
        {
            Debug.LogError("GameSurface: pixelPrefab is not assigned.", this);
            return false;
        }

        int activePlayerCount = players?.Count ?? 0;
        if (activePlayerCount > 0 && canonPrefab == null)
        {
            Debug.LogError("GameSurface: canonPrefab is not assigned.", this);
            return false;
        }

        ClearChildren();

        cellPitchX = Mathf.Max(0.01f, fieldWorldSize.x) / Mathf.Max(1, columns - 1);
        cellPitchZ = Mathf.Max(0.01f, fieldWorldSize.y) / Mathf.Max(1, rows - 1);

        effectivePixelScale = new Vector3(
            pixelSpawnScale.x * cellPitchX / Mathf.Max(0.01f, spacingRatioX),
            pixelSpawnScale.y,
            pixelSpawnScale.z * cellPitchZ / Mathf.Max(0.01f, spacingRatioZ));

        pixelParent = new GameObject(PixelParentName);
        pixelParent.transform.SetParent(transform);
        pixelParent.transform.localPosition = Vector3.zero;
        pixelParent.transform.localRotation = Quaternion.identity;
        pixelParent.transform.localScale = Vector3.one;

        float totalWidth = (columns - 1) * cellPitchX;
        float totalDepth = (rows - 1) * cellPitchZ;
        Vector3 halfOffset = new Vector3(totalWidth / 2f, 0, totalDepth / 2f);
        NetworkGameBootstrap networkBootstrap = NetworkGameBootstrap.Instance;
        bool capturePhysicsEnabled = networkBootstrap == null ||
            !networkBootstrap.ControlsGameplayRoster ||
            networkBootstrap.IsServer;

        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                Vector3 localPos = new Vector3(x * cellPitchX, 0, z * cellPitchZ) - halfOffset;
                GameObject instance = Instantiate(pixelPrefab, pixelParent.transform);

                instance.transform.localPosition = localPos;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = effectivePixelScale;

                Pixel pixel = instance.GetComponent<Pixel>();
                int ownerIndex = GetPlayerIndexForPixel(x, z, players, playerSlots);
                Player owner = ownerIndex >= 0 ? players[ownerIndex] : null;
                int ownerSlot = ownerIndex >= 0
                    ? ResolvePlayerSlot(ownerIndex, playerSlots)
                    : -1;
                pixel?.Init(owner, x * rows + z, ownerSlot);
                pixel?.SetCapturePhysicsAuthority(capturePhysicsEnabled);

                if (pixel != null)
                    spawnedPixels.Add(pixel);

                int cornerIndex = GetCornerIndex(x, z);
                if (cornerIndex >= 0)
                    cornerPixelTransforms[cornerIndex] = instance.transform;
            }
        }

        canonParent = new GameObject(CanonParentName);
        canonParent.transform.SetParent(transform);
        canonParent.transform.localPosition = Vector3.zero;
        canonParent.transform.localRotation = Quaternion.identity;
        canonParent.transform.localScale = Vector3.one;

        List<Transform> cornerSpawns = GetSpawnTransforms();

        for (int i = 0; i < activePlayerCount; i++)
        {
            Player player = players[i];
            int playerSlot = ResolvePlayerSlot(i, playerSlots);
            if (player == null || playerSlot < 0 || playerSlot >= cornerSpawns.Count)
            {
                if (player == null)
                    Debug.LogWarning($"Player {i} is null. Skipping spawn.");
                continue;
            }

            Transform spawnTransform = cornerSpawns[playerSlot];
            GameObject canonInstance = Instantiate(canonPrefab, canonParent.transform);

            Vector3 canonLocalPosition = spawnTransform.localPosition;
            canonLocalPosition.y += canonHeightOffset;
            canonInstance.transform.localPosition = canonLocalPosition;

            canonInstance.transform.localRotation = spawnTransform.localRotation;
            canonInstance.transform.localScale = canonSpawnScale;

            CanonRotator rotator = canonInstance.GetComponent<CanonRotator>();
            if (rotator != null)
            {
                Vector3 facingDirection = -spawnTransform.localPosition; // от угла к центру поля
                facingDirection.y = 0f;
                rotator.SetFacingDirection(facingDirection.normalized);
                rotator.SetPhaseByIndex(playerSlot, 4);
            }

            Canon canon = canonInstance.GetComponent<Canon>();
            canon.Init(player, canonInstance.transform.position, canonInstance.transform.rotation);
            spawnedCanons[player] = canon;
            Pixel masterPixel = spawnTransform.GetComponent<Pixel>();
            if (masterPixel != null)
                masterPixel.MarkAsMasterPixel(player, playerSlot);
        }

        if (ShouldUseOptimizedRendering())
        {
            if (surfaceRenderer == null)
                surfaceRenderer = GetComponent<GameSurfaceRenderer>() ??
                    gameObject.AddComponent<GameSurfaceRenderer>();

            surfaceRenderer.Rebuild(spawnedPixels);
        }

        return true;
    }

    public List<Transform> GetSpawnTransforms()
    {
        var spawnPoints = new List<Transform>(cornerPixelTransforms.Length);
        foreach (Transform cornerTransform in cornerPixelTransforms)
        {
            if (cornerTransform != null)
                spawnPoints.Add(cornerTransform);
        }

        return spawnPoints;
    }

    /// <summary>
    /// 0 = bottom-left, 1 = bottom-right, 2 = top-left, 3 = top-right.
    /// </summary>
    private int GetCornerIndex(int x, int z)
    {
        bool isLeftEdge = x == 0;
        bool isRightEdge = x == columns - 1;
        bool isBottomEdge = z == 0;
        bool isTopEdge = z == rows - 1;

        if (isLeftEdge && isBottomEdge) return 0;
        if (isRightEdge && isBottomEdge) return 1;
        if (isLeftEdge && isTopEdge) return 2;
        if (isRightEdge && isTopEdge) return 3;
        return -1;
    }

    public Player GetPlayerForPixel(int x, int z, IReadOnlyList<Player> players)
    {
        int playerIndex = GetPlayerIndexForPixel(x, z, players, null);
        return playerIndex >= 0 ? players[playerIndex] : null;
    }

    private int GetPlayerIndexForPixel(
        int x,
        int z,
        IReadOnlyList<Player> players,
        IReadOnlyList<int> playerSlots)
    {
        if (players == null || players.Count == 0)
            return -1;

        int territory = Mathf.Max(1, startingTerritorySize);

        // Порядок углов совпадает с GetSpawnTransforms: 0=низ-лево, 1=низ-право, 2=верх-лево, 3=верх-право.
        int[] cornerX = { 0, columns - 1, 0, columns - 1 };
        int[] cornerZ = { 0, 0, rows - 1, rows - 1 };

        for (int i = 0; i < players.Count && i < 4; i++)
        {
            if (players[i] == null)
                continue;

            int playerSlot = ResolvePlayerSlot(i, playerSlots);
            if (playerSlot < 0 || playerSlot >= cornerX.Length)
                continue;

            bool withinX = Mathf.Abs(x - cornerX[playerSlot]) < territory;
            bool withinZ = Mathf.Abs(z - cornerZ[playerSlot]) < territory;

            if (withinX && withinZ)
                return i;
        }

        return -1; // клетка вне чьей-либо стартовой территории — нейтральная
    }

    private static int ResolvePlayerSlot(int playerIndex, IReadOnlyList<int> playerSlots)
    {
        return playerSlots != null && playerIndex >= 0 && playerIndex < playerSlots.Count
            ? playerSlots[playerIndex]
            : playerIndex;
    }

    public void ClearChildren()
    {
        surfaceRenderer?.Clear();
        DestroyGeneratedContainer(ref pixelParent, PixelParentName);
        DestroyGeneratedContainer(ref canonParent, CanonParentName);
        spawnedCanons.Clear();
        spawnedPixels.Clear();
        System.Array.Clear(cornerPixelTransforms, 0, cornerPixelTransforms.Length);
    }

    private GameObject GetGeneratedContainer(GameObject cachedContainer, string containerName)
    {
        if (cachedContainer != null && cachedContainer.transform.parent == transform)
            return cachedContainer;

        Transform child = transform.Find(containerName);
        return child != null ? child.gameObject : null;
    }

    public void RemoveCanon(Player player)
    {
        spawnedCanons.Remove(player);
    }

    public bool TryGetPixel(int gridIndex, out Pixel pixel)
    {
        if (gridIndex >= 0 && gridIndex < spawnedPixels.Count)
        {
            pixel = spawnedPixels[gridIndex];
            return pixel != null;
        }

        pixel = null;
        return false;
    }

    public void ApplyNetworkPixelOwner(int gridIndex, Player owner)
    {
        if (TryGetPixel(gridIndex, out Pixel pixel))
            pixel.ApplyNetworkOwner(owner);
    }

    private void DestroyGeneratedContainer(ref GameObject cachedContainer, string containerName)
    {
        GameObject container = GetGeneratedContainer(cachedContainer, containerName);
        cachedContainer = null;

        if (container == null)
            return;

        container.SetActive(false);

        if (Application.isPlaying)
            Destroy(container);
        else
            DestroyImmediate(container);
    }

    private bool ShouldUseOptimizedRendering()
    {
        if (!useInstancedRenderingInPlayer)
            return false;

#if UNITY_EDITOR
        // Keep ordinary renderers visible and selectable while artists work in Play Mode.
        return false;
#else
        return true;
#endif
    }
}
