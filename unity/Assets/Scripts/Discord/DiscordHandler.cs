// DiscordHandler.cs
using System;
using System.Collections.Generic;
using Pochinki.Networking.Game;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class DiscordHandler : MonoBehaviour
{
    [Serializable]
    private sealed class DiscordSessionJson
    {
        public string selfUserId;
        public string selfUsername;
        public string instanceId;
    }

    public static DiscordHandler Instance { get; private set; }
    public string LocalUserId { get; private set; } = string.Empty;

    private readonly Dictionary<string, DiscordUser> users = new(StringComparer.Ordinal);

#if UNITY_EDITOR
    private string[] debugPlayerIds;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_EDITOR
        const int debugPlayerCount = 4;
        debugPlayerIds = new string[debugPlayerCount];

        for (int index = 0; index < debugPlayerCount; index++)
        {
            string id = Guid.NewGuid().ToString("N");
            debugPlayerIds[index] = id;

            var user = new DiscordUser(id, $"EditorTester{index}", isSelf: index == 0);
            users[id] = user;
        }

        LocalUserId = debugPlayerIds[0];
        ApplyRosterToGame();

        foreach (DiscordUser user in users.Values)
            GameHandler.instance?.onUserJoined?.Invoke(user);
#endif
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (debugPlayerIds == null)
            return;

        for (int index = 0; index < debugPlayerIds.Length; index++)
        {
            UnityEngine.InputSystem.Key key = UnityEngine.InputSystem.Key.Digit1 + index;
            if (UnityEngine.InputSystem.Keyboard.current[key].wasPressedThisFrame)
            {
                SetLocalUserIdForDebug(debugPlayerIds[index]);
                GameHandler.instance?.RefreshInputOwnership();
            }
        }
    }
#endif

    public void SetLocalUserIdForDebug(string userId)
    {
        LocalUserId = userId ?? string.Empty;
    }

    // Called by the authored WebGL template after Discord OAuth completes.
    [Preserve]
    public void ReceiveSession(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        DiscordSessionJson session;
        try
        {
            session = JsonUtility.FromJson<DiscordSessionJson>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DiscordHandler] Invalid Discord session: {exception.Message}");
            return;
        }

        if (session == null ||
            string.IsNullOrWhiteSpace(session.selfUserId) ||
            string.IsNullOrWhiteSpace(session.instanceId))
        {
            Debug.LogWarning("[DiscordHandler] Discord session is incomplete.", this);
            return;
        }

        LocalUserId = session.selfUserId;
        NetworkGameBootstrap.Instance?.SubmitIdentitySession(
            session.selfUserId,
            session.selfUsername,
            session.instanceId);
    }

    public void ApplyRosterToGame()
    {
        NetworkGameBootstrap networkBootstrap = NetworkGameBootstrap.Instance;
        if (networkBootstrap != null && networkBootstrap.ControlsGameplayRoster)
            return;

        var roster = new List<DiscordUser>(users.Values);
        GameHandler.instance?.ApplyRoster(roster);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
