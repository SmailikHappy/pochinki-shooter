// GameHandler.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameHandler : MonoBehaviour
{
    public static GameHandler Instance { get; private set; }

    public Action<User> OnUserJoined;
    public Action<User> OnUserLeft;

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform spawnPoint;

    private readonly Dictionary<ulong, Player> _players = new();

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