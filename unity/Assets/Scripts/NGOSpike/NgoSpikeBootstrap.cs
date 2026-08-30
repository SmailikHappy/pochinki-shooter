using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Pochinki.Networking.Spike
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class NgoSpikeBootstrap : MonoBehaviour
    {
        public const ushort ServerPort = 7777;
        public const string WebSocketPath = "/ngo-ws";

        [Header("Server-authoritative objects")]
        [SerializeField] private GameObject networkBallPrefab;
        [SerializeField] private Vector3 ballSpawnPosition = new Vector3(0f, 6f, 0f);

        private NetworkManager networkManager;
        private UnityTransport transport;

        public static NgoSpikeBootstrap Instance { get; private set; }

        public string EndpointDescription { get; private set; } = "not configured";

        public void Configure(GameObject ballPrefab, Vector3 spawnPosition)
        {
            networkBallPrefab = ballPrefab;
            ballSpawnPosition = spawnPosition;
        }

        private void Awake()
        {
            Instance = this;
            networkManager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();

            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void Start()
        {
#if UNITY_SERVER
            StartDedicatedServer();
#else
            StartWebClient();
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (networkManager == null)
            {
                return;
            }

            networkManager.OnServerStarted -= HandleServerStarted;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        private void StartDedicatedServer()
        {
            ConfigureConnection("127.0.0.1", ServerPort, false, "0.0.0.0");
            EndpointDescription = $"ws://0.0.0.0:{ServerPort}{WebSocketPath}";

            if (!networkManager.StartServer())
            {
                Debug.LogError($"[NGO Spike] Dedicated server failed to start at {EndpointDescription}.");
                return;
            }

            Debug.Log($"[NGO Spike] Dedicated server starting at {EndpointDescription}.");
        }

        private void StartWebClient()
        {
            ResolveClientEndpoint(out string host, out ushort port, out bool secure);
            ConfigureConnection(host, port, secure, host);

            string scheme = secure ? "wss" : "ws";
            EndpointDescription = $"{scheme}://{host}:{port}{WebSocketPath}";

            if (!networkManager.StartClient())
            {
                Debug.LogError($"[NGO Spike] Client failed to start for {EndpointDescription}.");
                return;
            }

            Debug.Log($"[NGO Spike] Client connecting to {EndpointDescription}.");
        }

        private void ConfigureConnection(string host, ushort port, bool secure, string listenAddress)
        {
            transport.UseWebSockets = true;
            transport.UseEncryption = secure;
            transport.SetConnectionData(host, port, listenAddress);

            UnityTransport.ConnectionAddressData data = transport.ConnectionData;
            data.WebSocketPath = WebSocketPath;
            transport.ConnectionData = data;

            if (secure)
            {
                // In WebGL this makes Unity Transport create a wss:// URL. The browser
                // validates the Cloudflare/Discord certificate; the Unity server stays WS
                // behind the HTTPS/WSS reverse proxies.
                transport.SetClientSecrets(host);
            }
        }

        private static void ResolveClientEndpoint(out string host, out ushort port, out bool secure)
        {
            host = "localhost";
            port = 5173;
            secure = false;

            if (!Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri pageUri))
            {
                return;
            }

            host = pageUri.Host;
            secure = string.Equals(pageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

            int resolvedPort = pageUri.IsDefaultPort ? (secure ? 443 : 80) : pageUri.Port;
            if (resolvedPort > 0 && resolvedPort <= ushort.MaxValue)
            {
                port = (ushort)resolvedPort;
            }
        }

        private void HandleServerStarted()
        {
            if (networkBallPrefab == null)
            {
                Debug.LogError("[NGO Spike] Network ball prefab is not assigned.");
                return;
            }

            GameObject ball = Instantiate(networkBallPrefab, ballSpawnPosition, Quaternion.identity);
            ball.GetComponent<NetworkObject>().Spawn();
            Debug.Log("[NGO Spike] Server spawned the shared physics ball.");
        }

        private static void HandleClientConnected(ulong clientId)
        {
            Debug.Log($"[NGO Spike] Client {clientId} connected.");
        }

        private static void HandleClientDisconnected(ulong clientId)
        {
            Debug.Log($"[NGO Spike] Client {clientId} disconnected.");
        }
    }
}
