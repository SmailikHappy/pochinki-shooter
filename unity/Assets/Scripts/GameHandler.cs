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
    public static GameHandler Instance { get; private set; }

    public Action<User> OnUserJoined;
    public Action<User> OnUserLeft;

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameSurface gameSurface;

    private readonly Dictionary<ulong, Player> _players = new();
    public GameState gameState { get; private set; } = GameState.WaitingForPlayers;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        OnUserJoined += SpawnPlayer;
        OnUserLeft += DespawnPlayer;
    }

    private void SpawnPlayer(User user)
    {
        if (_players.ContainsKey(user.UniqueId)) return;

        if (gameState == GameState.WaitingForPlayers)
        {
            gameSurface.SpawnGrid();
            gameState = GameState.InProgress;
        }

        var pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        var instance = Instantiate(playerPrefab, pos, Quaternion.identity);
        var player = instance.GetComponent<Player>();
        player.Bind(user);
        _players[user.UniqueId] = player;
    }

    private void DespawnPlayer(User user)
    {
        if (!_players.Remove(user.UniqueId, out var player)) return;
        if (player != null) Destroy(player.gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}