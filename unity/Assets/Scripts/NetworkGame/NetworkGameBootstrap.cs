using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Pochinki.Networking.Game
{
    /// <summary>
    /// Production NGO entry point. Dedicated builds start a server immediately;
    /// WebGL waits until DiscordHandler supplies identity and Activity instance.
    /// The current payload is intentionally a development identity, not a trusted
    /// authentication ticket. Replacing it with a Node-issued ticket is a later step.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class NetworkGameBootstrap : MonoBehaviour
    {
        public const ushort ServerPort = 7777;
        public const string WebSocketPath = "/ngo-ws";
        public const int MaxSupportedPlayers = 4;
        // Increment whenever separately built clients and servers would interpret
        // replicated gameplay state differently (for example, a grid layout change).
        public const ushort GameSchemaVersion = 6;

        [Serializable]
        private sealed class ConnectionIdentityPayload
        {
            public string discordId;
            public string username;
            public string instanceId;
            public int gameSchemaVersion;
        }

        private sealed class ApprovedIdentity
        {
            public string DiscordId;
            public string Username;
            public string InstanceId;
            public int Slot;
        }

        [Header("Production network prefabs")]
        [SerializeField] private GameObject sessionPlayerPrefab;
        [SerializeField] private GameObject pachinkoBallPrefab;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField, Range(1, MaxSupportedPlayers)] private int maxPlayers = MaxSupportedPlayers;

        private readonly Dictionary<ulong, ApprovedIdentity> pendingIdentities = new();
        private readonly Dictionary<ulong, NetworkSessionPlayer> sessionPlayers = new();
        private readonly Dictionary<int, NetworkPachinkoBall> pachinkoBallsBySlot = new();

        private NetworkManager networkManager;
        private UnityTransport transport;
        private string serverInstanceId = string.Empty;
        private bool clientStartRequested;
        private bool rosterRefreshQueued;

        public static NetworkGameBootstrap Instance { get; private set; }

        public bool IsServer => networkManager != null && networkManager.IsServer;
        public bool IsClient => networkManager != null && networkManager.IsClient;
        public bool IsListening => networkManager != null && networkManager.IsListening;

        public bool ControlsGameplayRoster
        {
            get
            {
#if UNITY_SERVER
                return true;
#elif UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public void Configure(
            GameObject playerPrefab,
            GameObject ballPrefab,
            GameObject networkBulletPrefab,
            int playerLimit)
        {
            sessionPlayerPrefab = playerPrefab;
            pachinkoBallPrefab = ballPrefab;
            bulletPrefab = networkBulletPrefab;
            maxPlayers = Mathf.Clamp(playerLimit, 1, MaxSupportedPlayers);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            networkManager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Release players do not need NGO profiling samples or routine network logs.
            // Errors remain enabled so production connection failures stay diagnosable.
            networkManager.NetworkConfig.EnableNetworkLogs = false;
            networkManager.NetworkConfig.NetworkProfilingMetrics = false;
            networkManager.LogLevel = LogLevel.Error;
#endif

            // NGO validates this in both directions before gameplay state is accepted.
            // Unlike an extra JSON field, this also protects a new client from an old
            // server that does not yet know how to validate gameSchemaVersion itself.
            networkManager.NetworkConfig.ProtocolVersion = GameSchemaVersion;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void Start()
        {
#if UNITY_SERVER
            StartDedicatedServer();
#elif UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[Network Game] Waiting for Discord identity/session before starting NGO client.");
#else
            Debug.Log("[Network Game] Production networking is idle in the Editor. Legacy editor gameplay remains available.");
#endif
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= HandleServerStarted;
                networkManager.OnClientConnectedCallback -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;

                if (networkManager.ConnectionApprovalCallback == ApproveConnection)
                {
                    networkManager.ConnectionApprovalCallback = null;
                }
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SubmitIdentitySession(string discordId, string username, string instanceId)
        {
            if (!ControlsGameplayRoster || clientStartRequested || IsListening)
            {
                return;
            }

            string cleanDiscordId = Sanitize(discordId, 64);
            string cleanInstanceId = Sanitize(instanceId, 128);
            string cleanUsername = Sanitize(username, 30);

            if (string.IsNullOrEmpty(cleanDiscordId) || string.IsNullOrEmpty(cleanInstanceId))
            {
                Debug.LogWarning("[Network Game] Identity is incomplete; NGO client is still waiting.", this);
                return;
            }

            clientStartRequested = true;
            var payload = new ConnectionIdentityPayload
            {
                discordId = cleanDiscordId,
                username = string.IsNullOrEmpty(cleanUsername) ? "Player" : cleanUsername,
                instanceId = cleanInstanceId,
                gameSchemaVersion = GameSchemaVersion,
            };

            networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            ResolveClientEndpoint(out string host, out ushort port, out bool secure);
            ConfigureConnection(host, port, secure, host);

            if (!networkManager.StartClient())
            {
                clientStartRequested = false;
                Debug.LogError("[Network Game] NGO client failed to start.", this);
                return;
            }

            string scheme = secure ? "wss" : "ws";
            Debug.Log($"[Network Game] NGO client connecting to {scheme}://{host}:{port}{WebSocketPath}.");
        }

        public void RegisterSessionPlayer(NetworkSessionPlayer player)
        {
            if (player == null || !player.IsSpawned)
            {
                return;
            }

            sessionPlayers[player.OwnerClientId] = player;

            if (IsServer && pendingIdentities.Remove(player.OwnerClientId, out ApprovedIdentity identity))
            {
                player.InitializeOnServer(identity.DiscordId, identity.Username, identity.Slot);
            }

            QueueRosterRefresh();
        }

        public void UnregisterSessionPlayer(NetworkSessionPlayer player)
        {
            if (player != null &&
                sessionPlayers.TryGetValue(player.OwnerClientId, out NetworkSessionPlayer current) &&
                current == player)
            {
                sessionPlayers.Remove(player.OwnerClientId);
            }

            QueueRosterRefresh();
        }

        public void NotifySessionPlayerChanged(NetworkSessionPlayer player)
        {
            QueueRosterRefresh();
        }

        public bool TryGetClientIdForSlot(int slot, out ulong clientId)
        {
            foreach (KeyValuePair<ulong, NetworkSessionPlayer> pair in sessionPlayers)
            {
                if (pair.Value != null && pair.Value.HasIdentity && pair.Value.Slot == slot)
                {
                    clientId = pair.Key;
                    return true;
                }
            }

            clientId = default;
            return false;
        }

        public bool TryGetPachinkoBallForSlot(int slot, out NetworkPachinkoBall ball)
        {
            return pachinkoBallsBySlot.TryGetValue(slot, out ball) && ball != null && ball.IsSpawned;
        }

        public void RegisterPachinkoBall(NetworkPachinkoBall ball)
        {
            if (ball == null || !ball.IsSpawned || ball.PlayerSlot < 0)
            {
                return;
            }

            UnregisterPachinkoBall(ball);
            pachinkoBallsBySlot[ball.PlayerSlot] = ball;
            ball.BindToGameplayField();
        }

        public void UnregisterPachinkoBall(NetworkPachinkoBall ball)
        {
            if (ball == null)
            {
                return;
            }

            List<int> slotsToRemove = null;
            foreach (KeyValuePair<int, NetworkPachinkoBall> pair in pachinkoBallsBySlot)
            {
                if (pair.Value == ball)
                {
                    (slotsToRemove ??= new List<int>()).Add(pair.Key);
                }
            }

            if (slotsToRemove == null)
            {
                return;
            }

            foreach (int slot in slotsToRemove)
            {
                pachinkoBallsBySlot.Remove(slot);
            }
        }

        public void RebindPachinkoBalls()
        {
            foreach (NetworkPachinkoBall ball in pachinkoBallsBySlot.Values)
            {
                ball?.BindToGameplayField();
            }
        }

        public bool TrySpawnPachinkoBall(PachinkoField field, int slot)
        {
            if (!IsServer || field == null || pachinkoBallPrefab == null)
            {
                return false;
            }

            if (TryGetPachinkoBallForSlot(slot, out NetworkPachinkoBall existingBall))
            {
                existingBall.BindToGameplayField();
                return true;
            }

            if (!TryGetClientIdForSlot(slot, out ulong ownerClientId))
            {
                Debug.LogWarning($"[Network Game] No approved NGO client owns Pachinko slot {slot}.", field);
                return false;
            }

            GameObject instance = Instantiate(pachinkoBallPrefab, field.SpawnPosition, field.SpawnRotation);
            NetworkPachinkoBall networkBall = instance.GetComponent<NetworkPachinkoBall>();
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();

            if (networkBall == null || networkObject == null)
            {
                Debug.LogError("[Network Game] Pachinko ball prefab is missing networking components.", instance);
                Destroy(instance);
                return false;
            }

            networkBall.ConfigureBeforeSpawn(slot);
            networkObject.SpawnWithOwnership(ownerClientId, destroyWithScene: true);
            return true;
        }

        public bool TrySpawnNetworkBullet(Canon canon)
        {
            if (!IsServer || canon == null || bulletPrefab == null ||
                GameHandler.instance == null ||
                !GameHandler.instance.TryGetSlotForPlayer(canon.Owner, out int slot) ||
                !GameHandler.instance.IsNetworkSlotActive(slot))
            {
                return false;
            }

            GameObject instance = Instantiate(
                bulletPrefab,
                canon.FirePosition,
                canon.FireRotation);
            NetworkBullet networkBullet = instance.GetComponent<NetworkBullet>();
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();

            if (networkBullet == null || networkObject == null)
            {
                Debug.LogError("[Network Game] Bullet prefab is missing networking components.", instance);
                Destroy(instance);
                return false;
            }

            networkBullet.ConfigureBeforeSpawn(
                slot,
                canon.Owner,
                canon.FireDirection,
                canon.BulletSpeed,
                canon.BulletScale);
            networkObject.Spawn(destroyWithScene: true);
            return true;
        }

        public void DespawnAllNetworkBullets()
        {
            if (!IsServer)
                return;

            NetworkBullet[] bullets = FindObjectsByType<NetworkBullet>(FindObjectsInactive.Include);
            foreach (NetworkBullet bullet in bullets)
            {
                if (bullet != null && bullet.IsSpawned)
                    bullet.DespawnOnServer();
            }
        }

        public void SetSessionPlayerEliminated(int slot, bool eliminated)
        {
            if (!IsServer)
                return;

            foreach (NetworkSessionPlayer player in sessionPlayers.Values)
            {
                if (player != null && player.IsSpawned && player.Slot == slot)
                {
                    player.SetEliminatedOnServer(eliminated);
                    return;
                }
            }
        }

        private void StartDedicatedServer()
        {
            ConfigureConnection("127.0.0.1", ServerPort, false, "0.0.0.0");

            if (!networkManager.StartServer())
            {
                Debug.LogError($"[Network Game] Dedicated server failed to start on port {ServerPort}.", this);
                return;
            }

            Debug.Log($"[Network Game] Dedicated server starting at ws://0.0.0.0:{ServerPort}{WebSocketPath}.");
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
                transport.SetClientSecrets(host);
            }
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;

            if (!TryReadIdentity(request.Payload, out ApprovedIdentity identity, out string rejectionReason))
            {
                response.Reason = rejectionReason;
                return;
            }

            if (!string.IsNullOrEmpty(serverInstanceId) &&
                !string.Equals(serverInstanceId, identity.InstanceId, StringComparison.Ordinal))
            {
                response.Reason = "This dedicated server is already assigned to another Activity instance.";
                return;
            }

            if (IsDiscordUserConnected(identity.DiscordId))
            {
                response.Reason = "This Discord user is already connected.";
                return;
            }

            int assignedSlot = FindFreeSlot();
            if (assignedSlot < 0)
            {
                response.Reason = $"The match is full ({maxPlayers} players).";
                return;
            }

            identity.Slot = assignedSlot;
            pendingIdentities[request.ClientNetworkId] = identity;
            serverInstanceId = identity.InstanceId;

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.PlayerPrefabHash = null;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
        }

        private bool TryReadIdentity(
            byte[] payloadBytes,
            out ApprovedIdentity identity,
            out string rejectionReason)
        {
            identity = null;
            rejectionReason = string.Empty;

            if (payloadBytes == null || payloadBytes.Length == 0 || payloadBytes.Length > 1024)
            {
                rejectionReason = "Missing or oversized connection identity.";
                return false;
            }

            ConnectionIdentityPayload payload;
            try
            {
                payload = JsonUtility.FromJson<ConnectionIdentityPayload>(Encoding.UTF8.GetString(payloadBytes));
            }
            catch (Exception)
            {
                rejectionReason = "Malformed connection identity.";
                return false;
            }

            string discordId = Sanitize(payload?.discordId, 64);
            string instanceId = Sanitize(payload?.instanceId, 128);
            string username = Sanitize(payload?.username, 30);

            if (payload == null || payload.gameSchemaVersion != GameSchemaVersion)
            {
                int clientSchema = payload?.gameSchemaVersion ?? 0;
                rejectionReason =
                    $"Client/server version mismatch (client schema {clientSchema}, " +
                    $"server schema {GameSchemaVersion}).";
                return false;
            }

            if (string.IsNullOrEmpty(discordId) || string.IsNullOrEmpty(instanceId))
            {
                rejectionReason = "Discord user or Activity instance is missing.";
                return false;
            }

            identity = new ApprovedIdentity
            {
                DiscordId = discordId,
                Username = string.IsNullOrEmpty(username) ? "Player" : username,
                InstanceId = instanceId,
                Slot = -1,
            };
            return true;
        }

        private bool IsDiscordUserConnected(string discordId)
        {
            foreach (ApprovedIdentity pending in pendingIdentities.Values)
            {
                if (string.Equals(pending.DiscordId, discordId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (NetworkSessionPlayer player in sessionPlayers.Values)
            {
                if (player != null &&
                    player.HasIdentity &&
                    string.Equals(player.DiscordUserId, discordId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private int FindFreeSlot()
        {
            int limit = Mathf.Clamp(maxPlayers, 1, MaxSupportedPlayers);
            for (int candidate = 0; candidate < limit; candidate++)
            {
                bool occupied = false;

                foreach (ApprovedIdentity pending in pendingIdentities.Values)
                {
                    if (pending.Slot == candidate)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied)
                {
                    foreach (NetworkSessionPlayer player in sessionPlayers.Values)
                    {
                        if (player != null && player.HasIdentity && player.Slot == candidate)
                        {
                            occupied = true;
                            break;
                        }
                    }
                }

                if (!occupied)
                {
                    return candidate;
                }
            }

            return -1;
        }

        private void HandleServerStarted()
        {
            Debug.Log(
                $"[Network Game] Server is ready for approved Discord sessions " +
                $"(game schema {GameSchemaVersion}).");
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (IsServer &&
                sessionPlayers.TryGetValue(clientId, out NetworkSessionPlayer player) &&
                pendingIdentities.Remove(clientId, out ApprovedIdentity identity))
            {
                player.InitializeOnServer(identity.DiscordId, identity.Username, identity.Slot);
            }

            Debug.Log($"[Network Game] NGO client {clientId} connected.");
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            pendingIdentities.Remove(clientId);
            sessionPlayers.Remove(clientId);
            QueueRosterRefresh();

            if (IsServer && sessionPlayers.Count == 0 && pendingIdentities.Count == 0)
            {
                serverInstanceId = string.Empty;
            }

            Debug.Log($"[Network Game] NGO client {clientId} disconnected.");
        }

        private void QueueRosterRefresh()
        {
            if (!ControlsGameplayRoster || rosterRefreshQueued || !isActiveAndEnabled)
            {
                return;
            }

            rosterRefreshQueued = true;
            StartCoroutine(RefreshRosterAtEndOfFrame());
        }

        private IEnumerator RefreshRosterAtEndOfFrame()
        {
            yield return null;
            rosterRefreshQueued = false;

            var readyPlayers = new List<NetworkSessionPlayer>();
            foreach (NetworkSessionPlayer player in sessionPlayers.Values)
            {
                if (player != null && player.IsSpawned && player.HasIdentity)
                {
                    readyPlayers.Add(player);
                }
            }

            readyPlayers.Sort((left, right) => left.Slot.CompareTo(right.Slot));
            var roster = new List<DiscordUser>(readyPlayers.Count);
            var slots = new List<int>(readyPlayers.Count);
            string localDiscordId = DiscordHandler.Instance?.LocalUserId ?? string.Empty;

            foreach (NetworkSessionPlayer player in readyPlayers)
            {
                roster.Add(new DiscordUser(
                    player.DiscordUserId,
                    player.Username,
                    string.Equals(player.DiscordUserId, localDiscordId, StringComparison.Ordinal)));
                slots.Add(player.Slot);
            }

            GameHandler.instance?.ApplyNetworkRoster(roster, slots);
            RebindPachinkoBalls();
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

        private static string Sanitize(string value, int maxLength)
        {
            string clean = value?.Trim() ?? string.Empty;
            return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength);
        }
    }
}
