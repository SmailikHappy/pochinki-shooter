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
    [Tooltip("Absolute world position of the bottom-left player's field (slot 0).")]
    [SerializeField] private Vector3 bottomLeftFieldPosition = new(-4.2f, 0f, -4.2f);
    [Tooltip("Absolute world position of the top-left player's field (slot 2).")]
    [SerializeField] private Vector3 topLeftFieldPosition = new(-7.02f, 0f, -2.42f);
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
        // Covers unusual script execution orders and scene reloads. Reapplying
        // an unchanged roster is intentionally a no-op for generated gameplay.
        DiscordHandler.Instance?.ApplyRosterToGame();
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

        List<Transform> cornerSpawns = gameSurface.GetSpawnTransforms();
        float centerX = gameSurface.transform.position.x;
        Quaternion rotation = Quaternion.Euler(pachinkoFieldRotationOffset);

        for (int index = 0; index < playersForGrid.Count && index < cornerSpawns.Count; index++)
        {
            Player player = playersForGrid[index];
            Vector3 spawnPosition = GetFieldPosition(index, centerX);
            GameObject fieldObject = Instantiate(pachinkoFieldPrefab, spawnPosition, rotation);
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
            spawnedFieldObjects.Add(fieldObject);
        }
    }

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

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
