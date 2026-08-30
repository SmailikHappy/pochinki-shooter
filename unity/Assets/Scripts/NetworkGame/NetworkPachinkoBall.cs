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
        private Rigidbody body;
        private byte configuredServerSlot = NetworkSessionPlayer.UnassignedSlot;
        private PachinkoField boundField;
        private Coroutine bindRoutine;
        private uint nextZoneSequence;
        private uint lastServerZoneSequence;

        public int PlayerSlot => playerSlot.Value == NetworkSessionPlayer.UnassignedSlot
            ? -1
            : playerSlot.Value;

        public bool HasPhysicsAuthority => IsSpawned && IsOwner;

        private void Awake()
        {
            pachinkoBall = GetComponent<PachinkoBall>();
            networkTransform = GetComponent<NetworkTransform>();
            body = GetComponent<Rigidbody>();
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

            NetworkGameBootstrap.Instance?.RegisterPachinkoBall(this);
            QueueBindToGameplayField();
        }

        public override void OnNetworkDespawn()
        {
            playerSlot.OnValueChanged -= HandleSlotChanged;
            NetworkGameBootstrap.Instance?.UnregisterPachinkoBall(this);

            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }

            boundField = null;
            pachinkoBall?.DetachFromField();
        }

        public void PrepareStandaloneSimulation()
        {
            if (!IsSpawned && body != null)
            {
                body.isKinematic = false;
            }
        }

        public void BindToGameplayField()
        {
            if (!IsSpawned || PlayerSlot < 0)
            {
                return;
            }

            if (GameHandler.instance == null ||
                !GameHandler.instance.TryGetPachinkoFieldForSlot(PlayerSlot, out PachinkoField field))
            {
                QueueBindToGameplayField();
                return;
            }

            bool fieldChanged = boundField != field;
            boundField = field;
            field.AttachNetworkBall(pachinkoBall);
            pachinkoBall.Initialize(field, resetAndLaunch: fieldChanged && IsOwner);
        }

        public void DetachFromGameplayField()
        {
            boundField = null;
            pachinkoBall?.DetachFromField();
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
                    bool fieldChanged = boundField != field;
                    boundField = field;
                    field.AttachNetworkBall(pachinkoBall);
                    pachinkoBall.Initialize(field, resetAndLaunch: fieldChanged && IsOwner);
                    yield break;
                }

                yield return null;
            }

            bindRoutine = null;
        }
    }
}
