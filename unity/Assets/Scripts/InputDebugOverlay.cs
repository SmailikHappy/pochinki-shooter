using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;

[Preserve]
public sealed class InputDebugOverlay : MonoBehaviour
{
    private const float MovementThreshold = 0.01f;
    private const float MovementSyncInterval = 0.1f;
    private const float HeartbeatInterval = 2f;
    private const int MaxVisibleParticipants = 8;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PochinkiSendInputJson(string json);
#endif

    [Serializable]
    private sealed class LocalInputPayload
    {
        public string lastEvent;
        public float mouseX;
        public float mouseY;
        public int eventCount;
    }

    [Serializable]
    private sealed class ParticipantState
    {
        public string userId;
        public string username;
        public string avatarUrl;
        public string lastEvent;
        public float mouseX;
        public float mouseY;
        public int eventCount;
        public long updatedAt;
    }

    [Serializable]
    private sealed class MultiplayerSnapshot
    {
        public bool connected;
        public string status = "Waiting for multiplayer...";
        public string selfUserId;
        public ParticipantState[] participants = Array.Empty<ParticipantState>();
    }

    private GUIStyle _titleStyle;
    private GUIStyle _textStyle;
    private GUIStyle _participantStyle;
    private string _lastEvent = "Waiting for input";
    private int _eventCount;
    private bool _hasFocus;
    private float _nextMovementSyncAt;
    private float _nextHeartbeatAt;
    private MultiplayerSnapshot _multiplayer = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOverlay()
    {
        if (FindAnyObjectByType<InputDebugOverlay>() != null)
        {
            return;
        }

        var overlayObject = new GameObject(nameof(InputDebugOverlay));
        DontDestroyOnLoad(overlayObject);
        overlayObject.AddComponent<InputDebugOverlay>();
    }

    private void Awake()
    {
        _hasFocus = Application.isFocused;
        _nextHeartbeatAt = Time.unscaledTime + 1f;
    }

    private void Update()
    {
        ReadMouse();
        ReadKeyboard();

        if (Time.unscaledTime >= _nextHeartbeatAt)
        {
            _nextHeartbeatAt = Time.unscaledTime + HeartbeatInterval;
            SendLocalInput();
        }
    }

    private void ReadMouse()
    {
        var mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        var delta = mouse.delta.ReadValue();

        if (delta.sqrMagnitude > MovementThreshold)
        {
            RecordEvent($"Mouse moved: dx={delta.x:0.0}, dy={delta.y:0.0}", false, false);

            if (Time.unscaledTime >= _nextMovementSyncAt)
            {
                _nextMovementSyncAt = Time.unscaledTime + MovementSyncInterval;
                SendLocalInput();
            }
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            RecordEvent("Left mouse button clicked");
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            RecordEvent("Right mouse button clicked");
        }

        if (mouse.middleButton.wasPressedThisFrame)
        {
            RecordEvent("Middle mouse button clicked");
        }

        var scroll = mouse.scroll.ReadValue();

        if (scroll.sqrMagnitude > MovementThreshold)
        {
            RecordEvent($"Mouse wheel: x={scroll.x:0.0}, y={scroll.y:0.0}");
        }
    }

    private void ReadKeyboard()
    {
        var keyboard = Keyboard.current;

        if (keyboard == null || !keyboard.anyKey.wasPressedThisFrame)
        {
            return;
        }

        foreach (var key in keyboard.allKeys)
        {
            if (key.wasPressedThisFrame)
            {
                RecordEvent($"Key pressed: {key.displayName}");
                return;
            }
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        _hasFocus = hasFocus;
        RecordEvent(hasFocus ? "Canvas focus received" : "Canvas focus lost");
    }

    private void RecordEvent(
        string message,
        bool writeToLog = true,
        bool syncImmediately = true
    )
    {
        _lastEvent = message;
        _eventCount++;

        if (writeToLog)
        {
            Debug.Log($"[InputDebug] {message}");
        }

        if (syncImmediately)
        {
            SendLocalInput();
        }
    }

    private void SendLocalInput()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var mousePosition = Mouse.current?.position.ReadValue() ?? Vector2.zero;
        var payload = new LocalInputPayload
        {
            lastEvent = _lastEvent,
            mouseX = Screen.width > 0 ? Mathf.Clamp01(mousePosition.x / Screen.width) : 0f,
            mouseY = Screen.height > 0 ? Mathf.Clamp01(mousePosition.y / Screen.height) : 0f,
            eventCount = _eventCount,
        };

        PochinkiSendInputJson(JsonUtility.ToJson(payload));
#endif
    }

    [Preserve]
    public void ReceiveMultiplayerSnapshot(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var snapshot = JsonUtility.FromJson<MultiplayerSnapshot>(json);

            if (snapshot == null)
            {
                return;
            }

            snapshot.participants ??= Array.Empty<ParticipantState>();
            snapshot.status ??= "Multiplayer";
            snapshot.selfUserId ??= string.Empty;
            _multiplayer = snapshot;
            MultiplayerParticipantCubes.ReceiveSnapshot(json);
        }
        catch (Exception error)
        {
            Debug.LogWarning($"[Multiplayer] Invalid snapshot: {error.Message}");
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawInputPanel();
        DrawMultiplayerPanel();
    }

    private void DrawInputPanel()
    {
        var scale = Mathf.Clamp(Screen.width / 1280f, 0.8f, 1.4f);
        var panelRect = new Rect(16f, 16f, 390f * scale, 176f * scale);
        var padding = 16f * scale;
        var lineHeight = 25f * scale;

        DrawPanelBackground(panelRect);

        var mouse = Mouse.current;
        var position = mouse?.position.ReadValue() ?? Vector2.zero;
        var delta = mouse?.delta.ReadValue() ?? Vector2.zero;
        var contentX = panelRect.x + padding;
        var contentWidth = panelRect.width - padding * 2f;
        var contentY = panelRect.y + padding;

        GUI.Label(
            new Rect(contentX, contentY, contentWidth, lineHeight),
            "INPUT DEBUG",
            _titleStyle
        );

        contentY += lineHeight * 1.35f;
        GUI.Label(
            new Rect(contentX, contentY, contentWidth, lineHeight),
            $"Focus: {(_hasFocus ? "YES" : "NO")}    Events: {_eventCount}",
            _textStyle
        );

        contentY += lineHeight;
        GUI.Label(
            new Rect(contentX, contentY, contentWidth, lineHeight),
            $"Mouse: {position.x:0}, {position.y:0}    Delta: {delta.x:0.0}, {delta.y:0.0}",
            _textStyle
        );

        contentY += lineHeight;
        GUI.Label(
            new Rect(contentX, contentY, contentWidth, lineHeight),
            $"Last: {_lastEvent}",
            _textStyle
        );

        contentY += lineHeight;
        GUI.Label(
            new Rect(contentX, contentY, contentWidth, lineHeight),
            "Click the game once, then move/click/type",
            _textStyle
        );
    }

    private void DrawMultiplayerPanel()
    {
        var scale = Mathf.Clamp(Screen.width / 1280f, 0.8f, 1.4f);
        var padding = 16f * scale;
        var lineHeight = 25f * scale;
        var participantCount = Mathf.Min(
            _multiplayer.participants?.Length ?? 0,
            MaxVisibleParticipants
        );
        var panelWidth = Mathf.Min(560f * scale, Screen.width - 32f);
        var panelHeight = (82f + participantCount * 30f) * scale;
        var panelRect = new Rect(
            Mathf.Max(16f, Screen.width - panelWidth - 16f),
            16f,
            panelWidth,
            panelHeight
        );

        DrawPanelBackground(panelRect);

        var contentX = panelRect.x + padding;
        var contentWidth = panelRect.width - padding * 2f;
        var contentY = panelRect.y + padding;
        var connectionMarker = _multiplayer.connected ? "ONLINE" : "OFFLINE";

        GUI.Label(
            new Rect(contentX, contentY, contentWidth, lineHeight),
            $"MULTIPLAYER  •  {connectionMarker}",
            _titleStyle
        );

        contentY += lineHeight * 1.35f;
        GUI.Label(
            new Rect(contentX, contentY, contentWidth, lineHeight),
            _multiplayer.status,
            _textStyle
        );

        contentY += lineHeight;

        for (var index = 0; index < participantCount; index++)
        {
            var participant = _multiplayer.participants[index];
            var isSelf = participant.userId == _multiplayer.selfUserId;
            var prefix = isSelf ? "YOU  " : "     ";
            var username = string.IsNullOrWhiteSpace(participant.username)
                ? "Unknown"
                : participant.username;
            var lastEvent = string.IsNullOrWhiteSpace(participant.lastEvent)
                ? "Waiting for input"
                : participant.lastEvent;
            var coordinates =
                $"{participant.mouseX * 100f:0}%, {participant.mouseY * 100f:0}%";

            GUI.Label(
                new Rect(contentX, contentY, contentWidth, lineHeight),
                $"{prefix}{username}  •  {lastEvent}  •  {coordinates}",
                _participantStyle
            );
            contentY += lineHeight;
        }
    }

    private static void DrawPanelBackground(Rect panelRect)
    {
        var previousColor = GUI.color;
        GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.9f);
        GUI.Box(panelRect, GUIContent.none);
        GUI.color = previousColor;
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null)
        {
            return;
        }

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        _textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.9f, 0.92f, 0.95f) },
        };

        _participantStyle = new GUIStyle(_textStyle)
        {
            clipping = TextClipping.Clip,
        };
    }
}
