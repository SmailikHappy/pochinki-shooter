// DiscordHandler.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class DiscordHandler : MonoBehaviour
{
    public static DiscordHandler Instance { get; private set; }

    public string LocalUserId { get; private set; } = string.Empty;

    private readonly Dictionary<string, DiscordUser> _users = new(StringComparer.Ordinal);

    #if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PochinkiSendInputJson(string json);
    #endif

    [Serializable]
    private sealed class ParticipantJson
    {
        public string userId;
        public string username;
        public float mouseX;
        public float mouseY;
    }

    [Serializable]
    private sealed class SnapshotJson
    {
        public string selfUserId;
        public ParticipantJson[] participants = Array.Empty<ParticipantJson>();
    }

    [Serializable]
    private sealed class LocalInputJson
    {
        public float mouseX;
        public float mouseY;
    }

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

    public void SetLocalUserIdForDebug(string userId)
    {
        LocalUserId = userId;
    }

    #if UNITY_EDITOR
    private string[] _debugPlayerIds;
    #endif

    private void Start()
    {
    #if UNITY_EDITOR
        const int debugPlayerCount = 4;
        _debugPlayerIds = new string[debugPlayerCount];

        for (int i = 0; i < debugPlayerCount; i++)
        {
            string id = System.Guid.NewGuid().ToString("N");
            _debugPlayerIds[i] = id;

            var user = new DiscordUser(id, $"EditorTester{i}", isSelf: i == 0);
            _users[id] = user;
        }

        LocalUserId = _debugPlayerIds[0];
        ApplyRosterToGame();

        foreach (var user in _users.Values)
            GameHandler.instance?.onUserJoined?.Invoke(user);
    #endif
    }

    #if UNITY_EDITOR
    private void Update()
    {
        if (_debugPlayerIds == null)
            return;

        for (int i = 0; i < _debugPlayerIds.Length; i++)
        {
            UnityEngine.InputSystem.Key key = UnityEngine.InputSystem.Key.Digit1 + i;
            if (UnityEngine.InputSystem.Keyboard.current[key].wasPressedThisFrame)
            {
                SetLocalUserIdForDebug(_debugPlayerIds[i]);
                GameHandler.instance?.RefreshInputOwnership();
            }
        }
    }
    #endif

    // Called directly by the WebGL template through Unity SendMessage.
    [Preserve]
    public void ReceiveSnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        SnapshotJson snapshot;
        try
        {
            snapshot = JsonUtility.FromJson<SnapshotJson>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DiscordHandler] Invalid snapshot: {e.Message}");
            return;
        }

        if (snapshot == null) return;
        snapshot.participants ??= Array.Empty<ParticipantJson>();
        LocalUserId = snapshot.selfUserId ?? string.Empty;

        var activeIds = new HashSet<string>(StringComparer.Ordinal);
        var joinedUsers = new List<DiscordUser>();

        foreach (var p in snapshot.participants)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.userId)) continue;
            if (!activeIds.Add(p.userId)) continue;

            if (!_users.TryGetValue(p.userId, out var user))
            {
                user = new DiscordUser(p.userId, p.username, p.userId == LocalUserId);
                _users[p.userId] = user;
                joinedUsers.Add(user);
            }

            user.Username = string.IsNullOrWhiteSpace(p.username) ? user.Username : p.username;
            user.MouseX = p.mouseX;
            user.MouseY = p.mouseY;
            user.IsSelf = p.userId == LocalUserId;
        }

        List<string> toRemove = null;
        foreach (var id in _users.Keys)
        {
            if (!activeIds.Contains(id))
            {
                (toRemove ??= new List<string>()).Add(id);
            }
        }

        var leftUsers = new List<DiscordUser>();
        if (toRemove != null)
        {
            foreach (var id in toRemove)
            {
                if (_users.Remove(id, out var user))
                {
                    leftUsers.Add(user);
                }
            }
        }

        // Apply the complete roster once. This prevents the game from starting
        // after the first participant while the rest of the same snapshot is
        // still being parsed.
        ApplyRosterToGame();

        foreach (var user in joinedUsers)
        {
            GameHandler.instance?.onUserJoined?.Invoke(user);
        }

        foreach (var user in leftUsers)
        {
            GameHandler.instance?.onUserLeft?.Invoke(user);
        }
    }

    public void ApplyRosterToGame()
    {
        var roster = new List<DiscordUser>(_users.Values);
        GameHandler.instance?.ApplyRoster(roster);
    }

    public void SendLocalInput(float mouseX, float mouseY)
    {
        if (_users.TryGetValue(LocalUserId, out var localUser))
        {
            localUser.MouseX = mouseX;
            localUser.MouseY = mouseY;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        PochinkiSendInputJson(JsonUtility.ToJson(new LocalInputJson { mouseX = mouseX, mouseY = mouseY }));
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
