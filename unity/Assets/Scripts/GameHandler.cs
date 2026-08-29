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
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameSurface gameSurface;

    private readonly Dictionary<ulong, Player> players = new();
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
        gameState = GameState.InProgress;
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