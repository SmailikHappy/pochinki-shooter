// GameHandler.cs
using System;
using System.Collections.Generic;
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
    private bool warnedAboutPlayerLimit;

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
    }

    private void Start()
    {
        // Do not start gameplay automatically on scene load. The roster can be
        // loaded and updated without launching the match until the user presses
        // the explicit start button.
    }

    public bool AreAllPlayersReady()
    {
        if (players.Count == 0)
        {
            return false;
        }

        foreach (Player player in players.Values)
        {
            if (player == null || !player.IsReady)
            {
                return false;
            }
        }

        return true;
    }

    public void MarkPlayerReady(Player player)
    {
        if (player == null)
        {
            return;
        }

        player.SetReady(true);

        if (AreAllPlayersReady())
        {
            StartGame();
        }
    }

    public void StartGame(bool force = false)
    {
        if (gameState == GameState.InProgress)
        {
            return;
        }

        if (!force && !AreAllPlayersReady())
        {
            return;
        }

        RebuildGameplay();
        HideLobbyButtons();
    }

    public void ApplyRoster(IReadOnlyList<DiscordUser> roster)
    {
        List<DiscordUser> orderedUsers = BuildOrderedRoster(roster);
        bool rosterChanged = HasRosterChanged(orderedUsers);

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
                player.Bind(user);
                player.Bind(user);
                player.SetMaterial(baseMaterial);

                if (user is DiscordUser discordUser && !string.IsNullOrWhiteSpace(discordUser.AvatarUrl))
                    player.LoadAvatarTexture(discordUser.AvatarUrl);
            }
        }

        activeRosterIds.Clear();
        foreach (DiscordUser user in orderedUsers)
        {
            activeRosterIds.Add(user.UniqueId);
        }

        if (rosterChanged && gameState == GameState.InProgress)
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
        if (!gameSurface.SpawnGrid(playersForGrid))
        {
            gameState = GameState.WaitingForPlayers;
            return;
        }

        if (playersForGrid.Count == 0)
        {
            gameState = GameState.WaitingForPlayers;
            return;
        }

        SpawnPachinkoFields(playersForGrid);
        gameState = GameState.InProgress;
        RefreshCanonInputOwnership();
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

        for (int index = 0; index < playersForGrid.Count && index < pachinkoSpawnPoints.Length; index++)
        {
            Player player = playersForGrid[index];
            Transform spawnPoint = pachinkoSpawnPoints[index];

            if (spawnPoint == null)
            {
                Debug.LogWarning($"GameHandler: pachinkoSpawnPoints[{index}] is not assigned.", this);
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
            field.Initialize(player, canon);
            spawnedFieldsByPlayer[player] = field;
            spawnedFieldObjects.Add(fieldObject);
        }
    }

    private readonly Dictionary<Player, PachinkoField> spawnedFieldsByPlayer = new();
    public IReadOnlyDictionary<Player, PachinkoField> SpawnedFields => spawnedFieldsByPlayer;

    public IReadOnlyDictionary<Player, Canon> SpawnedCanons =>
        gameSurface != null ? gameSurface.SpawnedCanons : null;

    private void ClearPachinkoFieldsAndBalls()
    {
        PachinkoBall[] balls = FindObjectsByType<PachinkoBall>(FindObjectsInactive.Include);

        foreach (PachinkoBall ball in balls)
        {
            if (ball != null)
            {
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
        spawnedFieldsByPlayer.Clear();
    }

    public Action<Player> onPlayerEliminated;

    public bool IsLastPlayerStanding()
    {
        return gameSurface != null && gameSurface.SpawnedCanons.Count == 1;
    }

    public void NotifyMasterPixelCaptured(Player eliminatedPlayer)
    {
        if (eliminatedPlayer == null)
        {
            Debug.LogWarning("NotifyMasterPixelCaptured called with null eliminatedPlayer.", this);   
            return;
        }

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

    private void HideLobbyButtons()
    {
        GameUI[] gameUis = FindObjectsByType<GameUI>(FindObjectsInactive.Include);
        foreach (GameUI gameUi in gameUis)
        {
            if (gameUi != null)
            {
                gameUi.HideLobbyButtons();
            }
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
