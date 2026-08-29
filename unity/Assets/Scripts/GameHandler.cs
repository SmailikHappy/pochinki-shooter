// GameHandler.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameHandler : MonoBehaviour
{
    public enum GameState
    {
        WaitingForPlayers,
        InProgress
    }

    public static GameHandler instance { get; private set; }

    public Action<User> onUserJoined;
    public Action<User> onUserLeft;

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameSurface gameSurface;
    public Material baseMaterial;

    [Header("Pachinko")]
    [SerializeField] private GameObject pachinkoFieldPrefab;
    [Tooltip("Абсолютная мировая позиция поля для нижнего-левого угла (индекс 0).")]
    [SerializeField] private Vector3 bottomLeftFieldPosition = new Vector3(-4.2f, 0f, -4.2f);
    [Tooltip("Абсолютная мировая позиция поля для верхнего-левого угла (индекс 2).")]
    [SerializeField] private Vector3 topLeftFieldPosition = new Vector3(-7.02f, 0f, -2.42f);
    [SerializeField] private Vector3 pachinkoFieldRotationOffset = new Vector3(90f, 0f, 0f);

    private readonly Dictionary<ulong, Player> players = new();
    private readonly List<PachinkoField> spawnedFields = new();
    public GameState gameState { get; private set; } = GameState.WaitingForPlayers;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        onUserJoined += SpawnPlayer;
        onUserLeft += DespawnPlayer;
    }

    private void StartGame()
    {
        if (gameState != GameState.WaitingForPlayers)
        {
            Debug.LogWarning("Game is already in progress. Cannot start a new game.");
            return;
        }

        List<Player> playersForGrid = new()
        {
            players.TryGetValue(1, out var player1) ? player1 : null,
            players.TryGetValue(2, out var player2) ? player2 : null,
            players.TryGetValue(3, out var player3) ? player3 : null,
            players.TryGetValue(4, out var player4) ? player4 : null,
        };

        gameSurface.SpawnGrid(playersForGrid);
        SpawnPachinkoFields(playersForGrid);

        gameState = GameState.InProgress;
    }

    private void SpawnPachinkoFields(IReadOnlyList<Player> playersForGrid)
    {
        if (pachinkoFieldPrefab == null)
        {
            Debug.LogWarning("GameHandler: не задан pachinkoFieldPrefab.", this);
            return;
        }

        List<Transform> cornerSpawns = gameSurface.GetSpawnTransforms();
        float centerX = gameSurface.transform.position.x;
        Quaternion rotation = Quaternion.Euler(pachinkoFieldRotationOffset);

        for (int i = 0; i < playersForGrid.Count && i < cornerSpawns.Count; i++)
        {
            Player player = playersForGrid[i];
            if (player == null)
                continue;

            Vector3 spawnPosition = GetFieldPosition(i, centerX);
            GameObject fieldInstance = Instantiate(pachinkoFieldPrefab, spawnPosition, rotation);
            PachinkoField field = fieldInstance.GetComponent<PachinkoField>();

            if (field == null)
            {
                Debug.LogWarning("GameHandler: на pachinkoFieldPrefab нет PachinkoField.", fieldInstance);
                continue;
            }

            gameSurface.SpawnedCanons.TryGetValue(player, out Canon canon);
            field.Initialize(player, canon);
            spawnedFields.Add(field);
        }
    }

    /// <summary>
    /// Индексы из GetSpawnTransforms: 0 = низ-лево, 1 = низ-право, 2 = верх-лево, 3 = верх-право.
    /// Правая сторона получается зеркалированием X левой относительно центра GameSurface —
    /// тюнить в инспекторе нужно только bottomLeftFieldPosition и topLeftFieldPosition.
    /// </summary>
    private Vector3 GetFieldPosition(int cornerIndex, float centerX)
    {
        return cornerIndex switch
        {
            0 => bottomLeftFieldPosition,
            1 => MirrorX(bottomLeftFieldPosition, centerX),
            2 => topLeftFieldPosition,
            3 => MirrorX(topLeftFieldPosition, centerX),
            _ => Vector3.zero,
        };
    }

private static Vector3 MirrorX(Vector3 position, float centerX)
{
    position.x = 2f * centerX - position.x;
    return position;
}

    private void SpawnPlayer(User user)
    {
        if (players.ContainsKey(user.UniqueId))
        {
            Debug.LogWarning($"Player with UniqueId {user.UniqueId} already exists. Skipping spawn.");
            return;
        }

        var instance = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        var player = instance.GetComponent<Player>();
        player.Bind(user);
        player.SetMaterial(baseMaterial);

        players[user.UniqueId] = player;

        if (gameState == GameState.WaitingForPlayers)
        {
            StartGame();
        }
    }

    private void DespawnPlayer(User user)
    {
        if (!players.Remove(user.UniqueId, out var player)) return;
        if (player != null) Destroy(player.gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}