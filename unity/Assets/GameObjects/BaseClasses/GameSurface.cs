using System.Collections.Generic;
using UnityEngine;

public class GameSurface : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int rows = 5;
    [SerializeField] private int columns = 5;
    [SerializeField] private float spacingX = 2f;
    [SerializeField] private float spacingZ = 2f;

    [Header("Prefab Settings")]
    [SerializeField] private GameObject pixelPrefab;
    [SerializeField] private Vector3 pixelSpawnScale = new Vector3(1f, 1f, 1f);
    [SerializeField] private GameObject canonPrefab;
    [SerializeField] private Vector3 canonSpawnScale = new Vector3(1f, 1f, 1f);

    private GameObject pixelParent;
    private GameObject canonParent;

    private readonly Dictionary<Player, Canon> spawnedCanons = new();
    public IReadOnlyDictionary<Player, Canon> SpawnedCanons => spawnedCanons;

    public bool SpawnGrid(IReadOnlyList<Player> players = null)
    {
        ClearChildren();
        spawnedCanons.Clear();

        pixelParent = new GameObject("PixelParent");
        pixelParent.transform.SetParent(transform);
        pixelParent.transform.localPosition = Vector3.zero;
        pixelParent.transform.localRotation = Quaternion.identity;
        pixelParent.transform.localScale = Vector3.one;

        float totalWidth = (columns - 1) * spacingX;
        float totalDepth = (rows - 1) * spacingZ;
        Vector3 halfOffset = new Vector3(totalWidth / 2f, 0, totalDepth / 2f);

        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                Vector3 localPos = new Vector3(x * spacingX, 0, z * spacingZ) - halfOffset;
                GameObject instance = Instantiate(pixelPrefab, pixelParent.transform);

                instance.transform.localPosition = localPos;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = pixelSpawnScale;

                Pixel pixel = instance.GetComponent<Pixel>();
                Player owner = GetPlayerForPixel(x, z, players);
                pixel?.Init(owner);
            }
        }

        canonParent = new GameObject("CanonParent");
        canonParent.transform.SetParent(transform);
        canonParent.transform.localPosition = Vector3.zero;
        canonParent.transform.localRotation = Quaternion.identity;
        canonParent.transform.localScale = Vector3.one;

        List<Transform> cornerSpawns = GetSpawnTransforms();

        for (int i = 0; i < players.Count; i++)
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

            canonInstance.transform.localPosition = spawnTransform.localPosition;
            canonInstance.transform.localRotation = spawnTransform.localRotation;
            canonInstance.transform.localScale = canonSpawnScale;

            CanonRotator rotator = canonInstance.GetComponent<CanonRotator>();
            if (rotator != null)
            {
                Vector3 facingDirection = -spawnTransform.localPosition; // от угла к центру поля
                facingDirection.y = 0f;
                rotator.SetFacingDirection(facingDirection.normalized);
                rotator.SetPhaseByIndex(i, players.Count);
            }

            Canon canon = canonInstance.GetComponent<Canon>();
            canon.Init(player, canonInstance.transform.position, canonInstance.transform.rotation);
            spawnedCanons[player] = canon;
        }

        return true;
        }

    public List<Transform> GetSpawnTransforms()
    {
        List<Transform> spawnPoints = new();
        List<Pixel> pixels = new(GetComponentsInChildren<Pixel>(true));

        int[] xIndices = { 0, columns - 1, 0, columns - 1 };
        int[] zIndices = { 0, 0, rows - 1, rows - 1 };

        for (int i = 0; i < 4; i++)
        {
            Pixel cornerPixel = pixels.Find(pixel =>
                pixel != null &&
                pixel.transform.localPosition.x == xIndices[i] * spacingX - ((columns - 1) * spacingX / 2f) &&
                pixel.transform.localPosition.z == zIndices[i] * spacingZ - ((rows - 1) * spacingZ / 2f));

            if (cornerPixel != null)
                spawnPoints.Add(cornerPixel.transform);
        }

        return spawnPoints;
    }

    public Player GetPlayerForPixel(int x, int z, IReadOnlyList<Player> players)
    {
        if (players == null || players.Count == 0)
            return null;

        int playerCount = Mathf.Min(players.Count, 4);
        int halfColumns = Mathf.Max(1, Mathf.CeilToInt(columns / 2f));
        int halfRows = Mathf.Max(1, Mathf.CeilToInt(rows / 2f));

        int playerIndex = 0;

        if (z >= halfRows)
        {
            playerIndex += 2;
        }

        if (x >= halfColumns)
        {
            playerIndex += 1;
        }

        playerIndex = Mathf.Clamp(playerIndex, 0, playerCount - 1);
        return players[playerIndex];
    }

    public void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}