using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Pochinki.Networking.Game
{
    /// <summary>
    /// Server-authoritative wrapper for the authored gameplay Bullet prefab.
    /// Only the dedicated server runs Rigidbody/collision; clients display the
    /// replicated transform and resolve the owner colour from the network slot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(NetworkRigidbody))]
    [RequireComponent(typeof(Rigidbody), typeof(Bullet))]
    public sealed class NetworkBullet : NetworkBehaviour
    {
        private readonly NetworkVariable<byte> playerSlot = new(
            NetworkSessionPlayer.UnassignedSlot,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Bullet bullet;
        private byte configuredSlot = NetworkSessionPlayer.UnassignedSlot;
        private Vector3 configuredDirection;
        private float configuredSpeed;
        private Coroutine bindRoutine;

        public int PlayerSlot => playerSlot.Value == NetworkSessionPlayer.UnassignedSlot
            ? -1
            : playerSlot.Value;

        private void Awake()
        {
            bullet = GetComponent<Bullet>();
        }

        public void ConfigureBeforeSpawn(int slot, Player owner, Vector3 direction, float speed, float scale)
        {
            configuredSlot = (byte)Mathf.Clamp(
                slot,
                0,
                NetworkGameBootstrap.MaxSupportedPlayers - 1);
            configuredDirection = direction;
            configuredSpeed = speed;
            bullet.PrepareNetworkSpawn(owner, scale);
        }

        public override void OnNetworkSpawn()
        {
            playerSlot.OnValueChanged += HandleSlotChanged;

            if (IsServer)
            {
                playerSlot.Value = configuredSlot;
                bullet.BeginServerNetworkSimulation(configuredDirection, configuredSpeed);
            }

            QueueOwnerBinding();
        }

        public override void OnNetworkDespawn()
        {
            playerSlot.OnValueChanged -= HandleSlotChanged;

            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }
        }

        public void DespawnOnServer()
        {
            if (IsServer && IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        private void HandleSlotChanged(byte previousValue, byte newValue)
        {
            QueueOwnerBinding();
        }

        private void QueueOwnerBinding()
        {
            if (!isActiveAndEnabled || bindRoutine != null)
            {
                return;
            }

            bindRoutine = StartCoroutine(BindOwnerWhenReady());
        }

        private IEnumerator BindOwnerWhenReady()
        {
            while (IsSpawned)
            {
                if (PlayerSlot >= 0 &&
                    GameHandler.instance != null &&
                    GameHandler.instance.TryGetPlayerForSlot(PlayerSlot, out Player owner))
                {
                    bullet.BindNetworkOwner(owner);
                    bindRoutine = null;
                    yield break;
                }

                yield return null;
            }

            bindRoutine = null;
        }
    }
}
