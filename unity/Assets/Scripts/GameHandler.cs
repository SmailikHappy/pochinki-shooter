// GameHandler.cs
using System;
using System.Collections.Generic;
using Pochinki.Networking.Game;
using UnityEngine;

public sealed class GameHandler : MonoBehaviour
{
    private const int MaxPlayers = 4;

    public enum GameState
    {
        WaitingForPlayers,
        InProgress
    }

    public static GameHandler instance { get; private set; }

    // Notifications for other gameplay systems. Roster mutation itself is
    // handled atomically by ApplyRoster, not one event at a time.
    public Action<User> onUserJoined;
    public Action<User> onUserLeft;
    public Action<Player> onPlayerEliminated;
    public Action<Player> onMatchEnded;
    public Action onMatchReset;

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameSurface gameSurface;
    public Material baseMaterial;

    [Header("Pachinko")]
    [SerializeField] private GameObject pachinkoFieldPrefab;
    [Tooltip("PachinkoSpawn (1)..(4) из сцены — назначить вручную в том же порядке, в котором должны занимать слоты игроки.")]
    [SerializeField] private Transform[] pachinkoSpawnPoints = new Transform[4];
    [SerializeField] private Vector3 pachinkoFieldRotationOffset = new(90f, 0f, 0f);

    private readonly Dictionary<string, Player> players = new(StringComparer.Ordinal);
    private readonly List<string> activeRosterIds = new();
    private readonly List<GameObject> spawnedFieldObjects = new();
    private readonly Dictionary<string, int> networkSlotsByUser = new(StringComparer.Ordinal);
    private readonly Dictionary<int, PachinkoField> spawnedFieldsBySlot = new();
    private bool warnedAboutPlayerLimit;
    private bool networkRosterActive;
    private byte appliedEliminatedMask;
    private NetworkMatchPhase appliedMatchPhase = NetworkMatchPhase.WaitingForPlayers;
    private int appliedWinnerSlot = -1;

    public GameState gameState { get; private set; } = GameState.WaitingForPlayers;
    public GameSurface Surface => gameSurface;
    public int ActivePlayerCount => activeRosterIds.Count;
    public int PixelCount => gameSurface?.SpawnedPixels.Count ?? 0;
    public bool IsGameplayReadyForNetworkState => gameSurface != null &&
        (activeRosterIds.Count == 0 || gameSurface.SpawnedPixels.Count > 0);

    public IReadOnlyList<int> ActiveNetworkSlots
    {
        get
        {
            var slots = new List<int>(networkSlotsByUser.Values);
            slots.Sort();
            return slots;
        }
    }

    public string NetworkRosterSignature
    {
        get
        {
            var signature = new System.Text.StringBuilder();
            foreach (string userId in activeRosterIds)
            {
                if (networkSlotsByUser.TryGetValue(userId, out int slot))
                    signature.Append(slot).Append(':').Append(userId).Append('|');
            }

            return signature.ToString();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Covers unusual script execution orders and scene reloads. Reapplying
        // an unchanged roster is intentionally a no-op for generated gameplay.
        DiscordHandler.Instance?.ApplyRosterToGame();
    }

    public void ApplyRoster(IReadOnlyList<DiscordUser> roster)
    {
        NetworkGameBootstrap networkBootstrap = NetworkGameBootstrap.Instance;
        if (networkBootstrap != null && networkBootstrap.ControlsGameplayRoster)
        {
            return;
        }

        networkRosterActive = false;
        networkSlotsByUser.Clear();
        ApplyOrderedRoster(BuildOrderedRoster(roster), forceRebuild: false);
    }

    public void ApplyNetworkRoster(IReadOnlyList<DiscordUser> roster, IReadOnlyList<int> slots)
    {
        var entries = new List<(DiscordUser User, int Slot)>();
        var seenUsers = new HashSet<string>(StringComparer.Ordinal);
        var seenSlots = new HashSet<int>();
        int entryCount = Mathf.Min(roster?.Count ?? 0, slots?.Count ?? 0);

        for (int index = 0; index < entryCount; index++)
        {
            DiscordUser user = roster[index];
            int slot = slots[index];

            if (user == null ||
                string.IsNullOrWhiteSpace(user.UniqueId) ||
                slot < 0 ||
                slot >= MaxPlayers ||
                !seenUsers.Add(user.UniqueId) ||
                !seenSlots.Add(slot))
            {
                continue;
            }

            entries.Add((user, slot));
        }

        entries.Sort((left, right) => left.Slot.CompareTo(right.Slot));

        var orderedUsers = new List<DiscordUser>(entries.Count);
        var updatedSlots = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((DiscordUser user, int slot) in entries)
        {
            orderedUsers.Add(user);
            updatedSlots[user.UniqueId] = slot;
        }

        bool slotsChanged = networkSlotsByUser.Count != updatedSlots.Count;
        if (!slotsChanged)
        {
            foreach (KeyValuePair<string, int> pair in updatedSlots)
            {
                if (!networkSlotsByUser.TryGetValue(pair.Key, out int previousSlot) ||
                    previousSlot != pair.Value)
                {
                    slotsChanged = true;
                    break;
                }
            }
        }

        networkRosterActive = true;
        networkSlotsByUser.Clear();
        foreach (KeyValuePair<string, int> pair in updatedSlots)
        {
            networkSlotsByUser[pair.Key] = pair.Value;
        }

        ApplyOrderedRoster(orderedUsers, slotsChanged);
    }

    private void ApplyOrderedRoster(List<DiscordUser> orderedUsers, bool forceRebuild)
    {
        bool rosterChanged = forceRebuild || HasRosterChanged(orderedUsers);

        RemovePlayersMissingFrom(orderedUsers);

        foreach (DiscordUser user in orderedUsers)
        {
            if (players.TryGetValue(user.UniqueId, out Player existingPlayer))
            {
                existingPlayer.Bind(user);
                continue;
            }

            Player player = CreatePlayer(user);
            if (player != null)
            {
                players[user.UniqueId] = player;
                rosterChanged = true;
            }
        }

        activeRosterIds.Clear();
        foreach (DiscordUser user in orderedUsers)
        {
            activeRosterIds.Add(user.UniqueId);
        }

        if (rosterChanged)
        {
            RebuildGameplay();
        }
        else
        {
            RefreshCanonInputOwnership();
        }
    }

    private List<DiscordUser> BuildOrderedRoster(IReadOnlyList<DiscordUser> roster)
    {
        var uniqueUsers = new Dictionary<string, DiscordUser>(StringComparer.Ordinal);

        if (roster != null)
        {
            foreach (DiscordUser user in roster)
            {
                if (user == null || string.IsNullOrWhiteSpace(user.UniqueId))
                {
                    continue;
                }

                uniqueUsers[user.UniqueId] = user;
            }
        }

        var orderedUsers = new List<DiscordUser>(uniqueUsers.Values);
        orderedUsers.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.UniqueId, right.UniqueId));

        bool overPlayerLimit = orderedUsers.Count > MaxPlayers;
        if (overPlayerLimit && !warnedAboutPlayerLimit)
        {
            Debug.LogWarning(
                $"GameHandler supports {MaxPlayers} players; extra Discord participants are spectators.",
                this);
        }

        warnedAboutPlayerLimit = overPlayerLimit;

        if (orderedUsers.Count > MaxPlayers)
        {
            orderedUsers.RemoveRange(MaxPlayers, orderedUsers.Count - MaxPlayers);
        }

        return orderedUsers;
    }

    private bool HasRosterChanged(IReadOnlyList<DiscordUser> orderedUsers)
    {
        if (activeRosterIds.Count != orderedUsers.Count)
        {
            return true;
        }

        for (int index = 0; index < orderedUsers.Count; index++)
        {
            if (!string.Equals(
                    activeRosterIds[index],
                    orderedUsers[index].UniqueId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void RemovePlayersMissingFrom(IReadOnlyList<DiscordUser> roster)
    {
        var activeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DiscordUser user in roster)
        {
            activeIds.Add(user.UniqueId);
        }

        List<string> idsToRemove = null;
        foreach (string id in players.Keys)
        {
            if (!activeIds.Contains(id))
            {
                (idsToRemove ??= new List<string>()).Add(id);
            }
        }

        if (idsToRemove == null)
        {
            return;
        }

        foreach (string id in idsToRemove)
        {
            if (!players.Remove(id, out Player player) || player == null)
            {
                continue;
            }

            player.gameObject.SetActive(false);
            Destroy(player.gameObject);
        }
    }

    private Player CreatePlayer(DiscordUser user)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("GameHandler: playerPrefab is not assigned.", this);
            return null;
        }

        GameObject instanceObject = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        Player player = instanceObject.GetComponent<Player>();

        if (player == null)
        {
            Debug.LogError("GameHandler: playerPrefab has no Player component.", instanceObject);
            Destroy(instanceObject);
            return null;
        }

        player.Bind(user);
        player.SetMaterial(baseMaterial);
        return player;
    }

    private void RebuildGameplay()
    {
        ClearPachinkoFieldsAndBalls();

        if (gameSurface == null)
        {
            gameState = GameState.WaitingForPlayers;
            Debug.LogError("GameHandler: gameSurface is not assigned.", this);
            return;
        }

        List<Player> playersForGrid = GetPlayersInRosterOrder();
        var slotsForGrid = new List<int>(playersForGrid.Count);
        for (int index = 0; index < playersForGrid.Count; index++)
        {
            Player player = playersForGrid[index];
            int slot = networkRosterActive && player?.user != null &&
                networkSlotsByUser.TryGetValue(player.user.UniqueId, out int assignedSlot)
                    ? assignedSlot
                    : index;
            slotsForGrid.Add(slot);
        }

        if (!gameSurface.SpawnGrid(playersForGrid, slotsForGrid))
        {
            gameState = GameState.WaitingForPlayers;
            return;
        }

        if (playersForGrid.Count == 0)
        {
            gameState = GameState.WaitingForPlayers;
            NetworkMatchState.Instance?.HandleGameplayRebuilt();
            return;
        }

        SpawnPachinkoFields(playersForGrid);
        gameState = GameState.InProgress;
        RefreshCanonInputOwnership();
        NetworkMatchState.Instance?.HandleGameplayRebuilt();
    }

    private List<Player> GetPlayersInRosterOrder()
    {
        var orderedPlayers = new List<Player>(activeRosterIds.Count);

        foreach (string id in activeRosterIds)
        {
            if (players.TryGetValue(id, out Player player) && player != null)
            {
                orderedPlayers.Add(player);
            }
        }

        return orderedPlayers;
    }

    private void SpawnPachinkoFields(IReadOnlyList<Player> playersForGrid)
    {
        if (pachinkoFieldPrefab == null)
        {
            Debug.LogWarning("GameHandler: pachinkoFieldPrefab is not assigned.", this);
            return;
        }

        if (pachinkoSpawnPoints == null || pachinkoSpawnPoints.Length == 0)
        {
            Debug.LogWarning("GameHandler: pachinkoSpawnPoints is not assigned.", this);
            return;
        }

        Quaternion rotationOffset = Quaternion.Euler(pachinkoFieldRotationOffset);

        for (int index = 0; index < playersForGrid.Count; index++)
        {
            Player player = playersForGrid[index];
            int playerSlot = networkRosterActive &&
                player?.user != null &&
                networkSlotsByUser.TryGetValue(player.user.UniqueId, out int assignedSlot)
                    ? assignedSlot
                    : index;

            if (playerSlot < 0 || playerSlot >= pachinkoSpawnPoints.Length)
            {
                Debug.LogWarning($"GameHandler: Pachinko slot {playerSlot} has no spawn point.", this);
                continue;
            }

            Transform spawnPoint = pachinkoSpawnPoints[playerSlot];

            if (spawnPoint == null)
            {
                Debug.LogWarning($"GameHandler: pachinkoSpawnPoints[{playerSlot}] is not assigned.", this);
                continue;
            }

            Quaternion rotation = spawnPoint.rotation * rotationOffset;
            GameObject fieldObject = Instantiate(pachinkoFieldPrefab, spawnPoint.position, rotation);
            PachinkoField field = fieldObject.GetComponentInChildren<PachinkoField>(true);

            if (field == null)
            {
                Debug.LogWarning(
                    "GameHandler: pachinkoFieldPrefab has no PachinkoField component.",
                    fieldObject);
                Destroy(fieldObject);
                continue;
            }

            gameSurface.SpawnedCanons.TryGetValue(player, out Canon canon);
            field.Initialize(player, canon, playerSlot);
            spawnedFieldObjects.Add(fieldObject);
            spawnedFieldsBySlot[playerSlot] = field;
        }

        NetworkGameBootstrap.Instance?.RebindPachinkoBalls();
    }

    private void ClearPachinkoFieldsAndBalls()
    {
        PachinkoBall[] balls = FindObjectsByType<PachinkoBall>(FindObjectsInactive.Include);

        foreach (PachinkoBall ball in balls)
        {
            if (ball != null)
            {
                if (ball.IsPersistentNetworkBall)
                {
                    ball.DetachFromField();
                    continue;
                }

                ball.gameObject.SetActive(false);
                Destroy(ball.gameObject);
            }
        }

        foreach (GameObject fieldObject in spawnedFieldObjects)
        {
            if (fieldObject != null)
            {
                fieldObject.SetActive(false);
                Destroy(fieldObject);
            }
        }

        spawnedFieldObjects.Clear();
        spawnedFieldsBySlot.Clear();
    }

    public bool TryGetPachinkoFieldForSlot(int slot, out PachinkoField field)
    {
        return spawnedFieldsBySlot.TryGetValue(slot, out field) && field != null;
    }

    public void NotifyMasterPixelCaptured(Player eliminatedPlayer)
    {
        if (eliminatedPlayer == null)
            return;

        NetworkGameBootstrap bootstrap = NetworkGameBootstrap.Instance;
        if (bootstrap != null && bootstrap.ControlsGameplayRoster)
            return;

        EliminatePlayer(eliminatedPlayer);
        onPlayerEliminated?.Invoke(eliminatedPlayer);
    }

    private void EliminatePlayer(Player eliminatedPlayer)
    {
        if (gameSurface == null)
            return;

        if (gameSurface.SpawnedCanons.TryGetValue(eliminatedPlayer, out Canon canon) && canon != null)
            Destroy(canon.gameObject);

        gameSurface.RemoveCanon(eliminatedPlayer);
    }

    public bool TryGetPlayerForSlot(int slot, out Player player)
    {
        foreach (KeyValuePair<string, int> pair in networkSlotsByUser)
        {
            if (pair.Value == slot && players.TryGetValue(pair.Key, out player) && player != null)
                return true;
        }

        player = null;
        return false;
    }

    public bool TryGetSlotForPlayer(Player player, out int slot)
    {
        string userId = player?.user?.UniqueId;
        if (!string.IsNullOrEmpty(userId) && networkSlotsByUser.TryGetValue(userId, out slot))
            return true;

        slot = -1;
        return false;
    }

    public bool IsNetworkSlotActive(int slot)
    {
        return TryGetPlayerForSlot(slot, out _);
    }

    public bool TryGetCanonForSlot(int slot, out Canon canon)
    {
        canon = null;
        return TryGetPlayerForSlot(slot, out Player player) &&
            gameSurface != null &&
            gameSurface.SpawnedCanons.TryGetValue(player, out canon) &&
            canon != null;
    }

    public bool TryGetPixel(int gridIndex, out Pixel pixel)
    {
        if (gameSurface != null)
            return gameSurface.TryGetPixel(gridIndex, out pixel);

        pixel = null;
        return false;
    }

    public void ApplyNetworkCounterState(int slot, int value, bool releasing, bool triggerEvent)
    {
        if (!TryGetPachinkoFieldForSlot(slot, out PachinkoField field))
            return;

        field.Counter?.ApplyNetworkState(value, releasing);
        field.SetZonesActive(!releasing);

        if (triggerEvent)
            field.Counter?.TriggerNetworkEvent();
    }

    public void ApplyNetworkPixelOwner(int gridIndex, int ownerSlot)
    {
        Player owner = ownerSlot >= 0 && TryGetPlayerForSlot(ownerSlot, out Player resolvedOwner)
            ? resolvedOwner
            : null;
        gameSurface?.ApplyNetworkPixelOwner(gridIndex, owner);
    }

    public void ApplyNetworkMatchOutcome(
        byte eliminatedMask,
        int winnerSlot,
        NetworkMatchPhase matchPhase)
    {
        byte newlyEliminated = (byte)(eliminatedMask & ~appliedEliminatedMask);
        for (int slot = 0; slot < MaxPlayers; slot++)
        {
            if ((newlyEliminated & (1 << slot)) == 0 ||
                !TryGetPlayerForSlot(slot, out Player eliminatedPlayer))
                continue;

            EliminatePlayer(eliminatedPlayer);
            onPlayerEliminated?.Invoke(eliminatedPlayer);
        }

        if (matchPhase != NetworkMatchPhase.GameOver &&
            appliedMatchPhase == NetworkMatchPhase.GameOver)
        {
            onMatchReset?.Invoke();
        }

        if (matchPhase == NetworkMatchPhase.GameOver &&
            (appliedMatchPhase != NetworkMatchPhase.GameOver || appliedWinnerSlot != winnerSlot))
        {
            TryGetPlayerForSlot(winnerSlot, out Player winner);
            onMatchEnded?.Invoke(winner);
        }

        appliedEliminatedMask = eliminatedMask;
        appliedWinnerSlot = winnerSlot;
        appliedMatchPhase = matchPhase;
    }

    public void RefreshInputOwnership()
    {
        RefreshCanonInputOwnership();
    }

    private void RefreshCanonInputOwnership()
    {
        if (gameSurface == null)
        {
            return;
        }

        foreach (Canon canon in gameSurface.SpawnedCanons.Values)
        {
            canon?.RefreshInputOwnership();
        }
    }


    private static Vector3 MirrorX(Vector3 position, float centerX)
    {
        position.x = 2f * centerX - position.x;
        return position;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
