// DiscordHandler.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class DiscordHandler : MonoBehaviour
{
    public static DiscordHandler Instance { get; private set; }

    public ulong LocalUserId { get; private set; }

    private readonly Dictionary<ulong, DiscordUser> _users = new();

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PochinkiSendInputJson(string json);
#endif

    [Serializable]
    private sealed class ParticipantJson
    {
        public ulong userId;
        public string username;
        public float mouseX;
        public float mouseY;
    }

    [Serializable]
    private sealed class SnapshotJson
    {
        public ulong selfUserId;
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

    private void Start()
    {
#if UNITY_EDITOR
        const ulong editorLocalId = 1;
        var local = new DiscordUser(editorLocalId, "EditorTester", isSelf: true);
        LocalUserId = local.UniqueId;
        _users[local.UniqueId] = local;
        GameHandler.Instance?.OnUserJoined?.Invoke(local);
#endif
    }

    // Called from JS bridge in build.
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
        LocalUserId = snapshot.selfUserId;

        var activeIds = new HashSet<ulong>();

        foreach (var p in snapshot.participants)
        {
            if (p == null || p.userId == 0) continue;
            activeIds.Add(p.userId);

            if (!_users.TryGetValue(p.userId, out var user))
            {
                user = new DiscordUser(p.userId, p.username, p.userId == LocalUserId);
                _users[p.userId] = user;
                GameHandler.Instance?.OnUserJoined?.Invoke(user);
            }

            user.Username = string.IsNullOrWhiteSpace(p.username) ? user.Username : p.username;
            user.MouseX = p.mouseX;
            user.MouseY = p.mouseY;
        }

        List<ulong> toRemove = null;
        foreach (var id in _users.Keys)
        {
            if (!activeIds.Contains(id))
            {
                (toRemove ??= new List<ulong>()).Add(id);
            }
        }

        if (toRemove == null) return;
        foreach (var id in toRemove)
        {
            if (!_users.Remove(id, out var user)) continue;
            GameHandler.Instance?.OnUserLeft?.Invoke(user);
        }
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