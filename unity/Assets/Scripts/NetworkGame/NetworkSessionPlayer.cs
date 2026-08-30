using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Pochinki.Networking.Game
{
    /// <summary>
    /// The server-owned identity record for one approved NGO connection.
    /// Gameplay objects are still built by GameHandler, but their roster and slot
    /// now come from these replicated records instead of Discord participant order.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkSessionPlayer : NetworkBehaviour
    {
        public const byte UnassignedSlot = byte.MaxValue;

        private readonly NetworkVariable<FixedString64Bytes> discordUserId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString128Bytes> username = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> slot = new(
            UnassignedSlot,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> isEliminated = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public string DiscordUserId => discordUserId.Value.ToString();
        public string Username => username.Value.ToString();
        public int Slot => slot.Value == UnassignedSlot ? -1 : slot.Value;
        public bool IsEliminated => isEliminated.Value;
        public bool HasIdentity => discordUserId.Value.Length > 0 && Slot >= 0;

        public override void OnNetworkSpawn()
        {
            discordUserId.OnValueChanged += HandleIdentityChanged;
            username.OnValueChanged += HandleUsernameChanged;
            slot.OnValueChanged += HandleSlotChanged;
            isEliminated.OnValueChanged += HandleEliminationChanged;

            NetworkGameBootstrap.Instance?.RegisterSessionPlayer(this);
        }

        public override void OnNetworkDespawn()
        {
            discordUserId.OnValueChanged -= HandleIdentityChanged;
            username.OnValueChanged -= HandleUsernameChanged;
            slot.OnValueChanged -= HandleSlotChanged;
            isEliminated.OnValueChanged -= HandleEliminationChanged;

            NetworkGameBootstrap.Instance?.UnregisterSessionPlayer(this);
        }

        public void InitializeOnServer(string userId, string displayName, int assignedSlot)
        {
            if (!IsServer)
            {
                Debug.LogWarning("NetworkSessionPlayer identity can only be initialized by the server.", this);
                return;
            }

            discordUserId.Value = new FixedString64Bytes(userId ?? string.Empty);
            username.Value = new FixedString128Bytes(displayName ?? string.Empty);
            slot.Value = (byte)Mathf.Clamp(assignedSlot, 0, NetworkGameBootstrap.MaxSupportedPlayers - 1);
            isEliminated.Value = false;

            gameObject.name = $"Network Session Player - slot {slot.Value} - {discordUserId.Value}";
            NetworkGameBootstrap.Instance?.NotifySessionPlayerChanged(this);
        }

        public void SetEliminatedOnServer(bool eliminated)
        {
            if (IsServer)
            {
                isEliminated.Value = eliminated;
            }
        }

        private void HandleIdentityChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
        {
            NetworkGameBootstrap.Instance?.NotifySessionPlayerChanged(this);
        }

        private void HandleUsernameChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
        {
            NetworkGameBootstrap.Instance?.NotifySessionPlayerChanged(this);
        }

        private void HandleSlotChanged(byte previousValue, byte newValue)
        {
            NetworkGameBootstrap.Instance?.NotifySessionPlayerChanged(this);
        }

        private void HandleEliminationChanged(bool previousValue, bool newValue)
        {
            NetworkGameBootstrap.Instance?.NotifySessionPlayerChanged(this);
        }
    }
}
