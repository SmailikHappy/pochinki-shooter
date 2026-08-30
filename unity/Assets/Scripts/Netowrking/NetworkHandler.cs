using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public sealed class NetworkHandler : MonoBehaviour
{
    public static NetworkHandler Instance { get; private set; }

    private readonly Dictionary<ulong, User> usersByClientId = new();
    private NetworkManager networkManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        networkManager = GetComponent<NetworkManager>();
        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void Start()
    {
#if UNITY_SERVER
        if (networkManager == null)
        {
            Debug.LogError("[NetworkHandler] NetworkManager component is missing.");
            return;
        }

        if (networkManager.IsListening)
        {
            return;
        }

        if (!networkManager.StartServer())
        {
            Debug.LogError("[NetworkHandler] Failed to start dedicated server.");
            return;
        }

        Debug.Log("[NetworkHandler] Dedicated server started.");
#else
        Debug.Log("[NetworkHandler] NetworkHandler initialized.");
#endif
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"[NetworkHandler] Client connected successfully. ClientId: {clientId}");
        CreateUser(clientId);
    }

    private User CreateUser(ulong clientId)
    {
        if (usersByClientId.TryGetValue(clientId, out var existingUser))
        {
            return existingUser;
        }

        var user = new User();
        usersByClientId[clientId] = user;
        GameHandler.instance?.onUserJoined?.Invoke(user);
        return user;
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetworkHandler] Client disconnected. ClientId: {clientId}");

        if (usersByClientId.Remove(clientId, out var user))
        {
            GameHandler.instance?.onUserLeft?.Invoke(user);
        }
    }
}
