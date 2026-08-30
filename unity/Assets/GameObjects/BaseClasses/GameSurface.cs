using System.Collections.Generic;
using UnityEngine;

public class GameSurface : MonoBehaviour
{
    private const string PixelParentName = "PixelParent";
    private const string CanonParentName = "CanonParent";

    [Header("Grid Settings")]
    [SerializeField] private int rows = 5;
    [SerializeField] private int columns = 5;

    [Tooltip("Фиксированный физический размер поля (ширина, глубина) в мировых единицах. " +
             "Не меняется от rows/columns/spacing — только от этого значения.")]
    [SerializeField] private Vector2 fieldWorldSize = new Vector2(30f, 30f);

    [Tooltip("Коэффициент соотношения визуальный пиксель / зона захвата, не мировая дистанция. " +
             "Больше — пиксель мельче, зона триггера крупнее. Меньше — наоборот. 1 = нейтрально.")]
    [SerializeField, Min(0.01f)] private float spacingRatioX = 1f;
    [SerializeField, Min(0.01f)] private float spacingRatioZ = 1f;

    private float cellPitchX;
    private float cellPitchZ;
    private Vector3 effectivePixelScale;
    private float triggerSizeX;
    private float triggerSizeZ;

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

    private readonly Dictionary<Player, Canon> spawnedCanons = new();
    public IReadOnlyDictionary<Player, Canon> SpawnedCanons => spawnedCanons;

    public bool SpawnGrid(IReadOnlyList<Player> players)
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

        // Шаг сетки всегда подгоняется под фиксированный физический размер поля —
        // rows/columns влияют только на плотность, не на общий занимаемый объём.
        cellPitchX = fieldWorldSize.x / Mathf.Max(1, columns - 1);
        cellPitchZ = fieldWorldSize.y / Mathf.Max(1, rows - 1);

        effectivePixelScale = new Vector3(
            pixelSpawnScale.x * cellPitchX / spacingRatioX,
            pixelSpawnScale.y,
            pixelSpawnScale.z * cellPitchZ / spacingRatioZ);

        triggerSizeX = cellPitchX * spacingRatioX;
        triggerSizeZ = cellPitchZ * spacingRatioZ;

        pixelParent = new GameObject(PixelParentName);
        pixelParent.transform.SetParent(transform);
        pixelParent.transform.localPosition = Vector3.zero;
        pixelParent.transform.localRotation = Quaternion.identity;
        pixelParent.transform.localScale = Vector3.one;

        float totalWidth = (columns - 1) * cellPitchX;
        float totalDepth = (rows - 1) * cellPitchZ;
        Vector3 halfOffset = new Vector3(totalWidth / 2f, 0, totalDepth / 2f);

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
                Player owner = GetPlayerForPixel(x, z, players);
                pixel?.Init(owner);
                pixel?.SetCaptureZoneWorldSize(triggerSizeX, triggerSizeZ);
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
            if (player == null || i >= cornerSpawns.Count)
            {
                if (player == null)
                    Debug.LogWarning($"Player {i} is null. Skipping spawn.");
                continue;
            }

            Transform spawnTransform = cornerSpawns[i];
            GameObject canonInstance = Instantiate(canonPrefab, canonParent.transform);

            Vector3 canonLocalPosition = spawnTransform.localPosition;
            canonLocalPosition.y += canonHeightOffset;
            canonInstance.transform.localPosition = canonLocalPosition;

            canonInstance.transform.localRotation = spawnTransform.localRotation;
            canonInstance.transform.localScale = canonSpawnScale;

            CanonRotator rotator = canonInstance.GetComponent<CanonRotator>();
            if (rotator != null)
            {
                Vector3 facingDirection = -spawnTransform.localPosition;
                facingDirection.y = 0f;
                rotator.SetFacingDirection(facingDirection.normalized);
                rotator.SetPhaseByIndex(i, players.Count);
            }

            Canon canon = canonInstance.GetComponent<Canon>();
            canon.Init(player, canonInstance.transform.position, canonInstance.transform.rotation);
            spawnedCanons[player] = canon;

            Pixel masterPixel = spawnTransform.GetComponent<Pixel>();
            if (masterPixel != null)
                masterPixel.MarkAsMasterPixel(player);
        }

        return true;
    }

    public List<Transform> GetSpawnTransforms()
    {
        List<Transform> spawnPoints = new();
        GameObject generatedPixelParent = GetGeneratedContainer(pixelParent, PixelParentName);
        if (generatedPixelParent == null)
            return spawnPoints;

        List<Pixel> pixels = new(generatedPixelParent.GetComponentsInChildren<Pixel>(false));

        int[] xIndices = { 0, columns - 1, 0, columns - 1 };
        int[] zIndices = { 0, 0, rows - 1, rows - 1 };

        for (int i = 0; i < 4; i++)
        {
            Pixel cornerPixel = pixels.Find(pixel =>
                pixel != null &&
                pixel.transform.localPosition.x == xIndices[i] * cellPitchX - ((columns - 1) * cellPitchX / 2f) &&
                pixel.transform.localPosition.z == zIndices[i] * cellPitchZ - ((rows - 1) * cellPitchZ / 2f));

            if (cornerPixel != null)
                spawnPoints.Add(cornerPixel.transform);
        }

        return spawnPoints;
    }

    public Player GetPlayerForPixel(int x, int z, IReadOnlyList<Player> players)
    {
        if (players == null || players.Count == 0)
            return null;

        int territory = Mathf.Max(1, startingTerritorySize);

        int[] cornerX = { 0, columns - 1, 0, columns - 1 };
        int[] cornerZ = { 0, 0, rows - 1, rows - 1 };

        for (int i = 0; i < players.Count && i < 4; i++)
        {
            if (players[i] == null)
                continue;

            bool withinX = Mathf.Abs(x - cornerX[i]) < territory;
            bool withinZ = Mathf.Abs(z - cornerZ[i]) < territory;

            if (withinX && withinZ)
                return players[i];
        }

        return null;
    }

    public void ClearChildren()
    {
        DestroyGeneratedContainer(ref pixelParent, PixelParentName);
        DestroyGeneratedContainer(ref canonParent, CanonParentName);
        spawnedCanons.Clear();
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
}