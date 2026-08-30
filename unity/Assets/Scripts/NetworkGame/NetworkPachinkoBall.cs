using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Pochinki.Networking.Game
{
    /// <summary>
    /// NGO ownership wrapper for the existing PachinkoBall gameplay component.
    /// Only the owning WebGL client simulates Rigidbody physics. The server and
    /// other clients keep kinematic replicas through NetworkTransform.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(NetworkRigidbody))]
    [RequireComponent(typeof(Rigidbody), typeof(PachinkoBall))]
    public sealed class NetworkPachinkoBall : NetworkBehaviour
    {
        private readonly NetworkVariable<byte> playerSlot = new(
            NetworkSessionPlayer.UnassignedSlot,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private PachinkoBall pachinkoBall;
        private NetworkTransform networkTransform;
        private byte configuredServerSlot = NetworkSessionPlayer.UnassignedSlot;
        private PachinkoField boundField;
        private Coroutine bindRoutine;
        private uint nextZoneSequence;
        private uint lastServerZoneSequence;
        private bool postSpawnReady;

        public int PlayerSlot => playerSlot.Value == NetworkSessionPlayer.UnassignedSlot
            ? -1
            : playerSlot.Value;

        public bool HasPhysicsAuthority => IsSpawned && IsOwner;

        private void Awake()
        {
            pachinkoBall = GetComponent<PachinkoBall>();
            networkTransform = GetComponent<NetworkTransform>();
        }

        public void ConfigureBeforeSpawn(int slot)
        {
            configuredServerSlot = (byte)Mathf.Clamp(
                slot,
                0,
                NetworkGameBootstrap.MaxSupportedPlayers - 1);
        }

        public override void OnNetworkSpawn()
        {
            playerSlot.OnValueChanged += HandleSlotChanged;

            if (IsServer && configuredServerSlot != NetworkSessionPlayer.UnassignedSlot)
            {
                playerSlot.Value = configuredServerSlot;
            }

        }

        protected override void OnNetworkPostSpawn()
        {
            postSpawnReady = true;
            ApplyPhysicsAuthority(HasPhysicsAuthority);
            NetworkGameBootstrap.Instance?.RegisterPachinkoBall(this);
            QueueBindToGameplayField();
        }

        public override void OnNetworkDespawn()
        {
            postSpawnReady = false;
            ApplyPhysicsAuthority(false);
            playerSlot.OnValueChanged -= HandleSlotChanged;
            NetworkGameBootstrap.Instance?.UnregisterPachinkoBall(this);

            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }

            DetachBoundField();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();

            if (!postSpawnReady)
                return;

            ApplyPhysicsAuthority(true);
            QueueBindToGameplayField();
        }

        public override void OnLostOwnership()
        {
            ApplyPhysicsAuthority(false);
            base.OnLostOwnership();
        }

        public void BindToGameplayField()
        {
            if (!postSpawnReady || !IsSpawned || PlayerSlot < 0)
            {
                return;
            }

            if (GameHandler.instance == null ||
                !GameHandler.instance.TryGetPachinkoFieldForSlot(PlayerSlot, out PachinkoField field))
            {
                QueueBindToGameplayField();
                return;
            }

            BindToField(field);
        }

        public void DetachFromGameplayField()
        {
            ApplyPhysicsAuthority(false);
            DetachBoundField();
        }

        public void TeleportFromOwner(Vector3 position, Quaternion rotation)
        {
            if (!HasPhysicsAuthority)
            {
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
            networkTransform.Teleport(position, rotation, transform.localScale);
        }

        public bool ReportZoneHit(ScoreZoneType zoneType)
        {
            if (!HasPhysicsAuthority ||
                (byte)zoneType > (byte)ScoreZoneType.Event)
            {
                return false;
            }

            nextZoneSequence++;
            ReportZoneHitRpc((byte)zoneType, nextZoneSequence);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void ReportZoneHitRpc(byte rawZoneType, uint sequence, RpcParams rpcParams = default)
        {
            if (!IsServer ||
                rpcParams.Receive.SenderClientId != OwnerClientId ||
                sequence <= lastServerZoneSequence ||
                rawZoneType > (byte)ScoreZoneType.Event)
            {
                return;
            }

            lastServerZoneSequence = sequence;
            NetworkMatchState.Instance?.TryApplyPachinkoZoneHit(
                PlayerSlot,
                (ScoreZoneType)rawZoneType);
        }

        private void HandleSlotChanged(byte previousValue, byte newValue)
        {
            if (!postSpawnReady)
                return;

            NetworkGameBootstrap.Instance?.RegisterPachinkoBall(this);
            QueueBindToGameplayField();
        }

        private void QueueBindToGameplayField()
        {
            if (!isActiveAndEnabled || bindRoutine != null)
            {
                return;
            }

            bindRoutine = StartCoroutine(BindWhenGameplayIsReady());
        }

        private IEnumerator BindWhenGameplayIsReady()
        {
            while (IsSpawned)
            {
                if (PlayerSlot >= 0 &&
                    GameHandler.instance != null &&
                    GameHandler.instance.TryGetPachinkoFieldForSlot(PlayerSlot, out PachinkoField field))
                {
                    bindRoutine = null;
                    BindToField(field);
                    yield break;
                }

                yield return null;
            }

            bindRoutine = null;
        }

        private void BindToField(PachinkoField field)
        {
            bool fieldChanged = boundField != field;
            if (fieldChanged && boundField != null)
                boundField.DetachNetworkBall(pachinkoBall);

            boundField = field;
            bool hasPhysicsAuthority = HasPhysicsAuthority;
            field.AttachNetworkBall(pachinkoBall, hasPhysicsAuthority);
            pachinkoBall.Initialize(
                field,
                resetAndLaunch: fieldChanged && hasPhysicsAuthority);
        }

        private void ApplyPhysicsAuthority(bool active)
        {
            pachinkoBall?.SetNetworkPhysicsAuthority(active);

            if (boundField != null)
                boundField.AttachNetworkBall(pachinkoBall, active);
        }

        private void DetachBoundField()
        {
            if (boundField != null)
                boundField.DetachNetworkBall(pachinkoBall);

            boundField = null;
            pachinkoBall?.DetachFromField();
        }
    }
}
