using Unity.Netcode;
using UnityEngine;

namespace Pochinki.Networking.Spike
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public sealed class NgoSpikeBall : NetworkBehaviour
    {
        private static readonly ulong ServerGeneratedImpulse = ulong.MaxValue;

        [SerializeField] private float pushForce = 8f;
        [SerializeField] private float automaticKickInterval = 5f;
        [SerializeField] private float resetHeight = -4f;
        [SerializeField] private float resetCeiling = 11f;
        [SerializeField] private Vector2 resetHorizontalBounds = new Vector2(6.5f, 4.5f);
        [SerializeField] private Vector3 resetPosition = new Vector3(0f, 6f, 0f);

        public readonly NetworkVariable<int> ImpulseCount = new NetworkVariable<int>(0);
        public readonly NetworkVariable<ulong> LastPushedBy = new NetworkVariable<ulong>(ServerGeneratedImpulse);

        private Rigidbody body;
        private float nextAutomaticKick;

        public static NgoSpikeBall Instance { get; private set; }

        public Rigidbody Body => body;

        public void Configure(Vector3 spawnPosition)
        {
            resetPosition = spawnPosition;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;

            if (IsServer)
            {
                nextAutomaticKick = Time.time + automaticKickInterval;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            Vector3 position = transform.position;
            if (position.y < resetHeight ||
                position.y > resetCeiling ||
                Mathf.Abs(position.x) > resetHorizontalBounds.x ||
                Mathf.Abs(position.z) > resetHorizontalBounds.y)
            {
                ResetOnServer();
                return;
            }

            if (Time.time >= nextAutomaticKick)
            {
                ApplyImpulseOnServer(ServerGeneratedImpulse);
            }
        }

        public void ApplyImpulseOnServer(ulong senderClientId)
        {
            if (!IsServer)
            {
                return;
            }

            int impulseIndex = ImpulseCount.Value + 1;
            float horizontalSign = impulseIndex % 2 == 0 ? 1f : -1f;
            float forwardSign = impulseIndex % 3 == 0 ? -1f : 1f;
            Vector3 direction = new Vector3(0.55f * horizontalSign, 1f, 0.35f * forwardSign).normalized;

            body.WakeUp();
            body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, 4f);
            body.AddForce(direction * pushForce, ForceMode.Impulse);
            ImpulseCount.Value = impulseIndex;
            LastPushedBy.Value = senderClientId;
            nextAutomaticKick = Time.time + automaticKickInterval;

            string source = senderClientId == ServerGeneratedImpulse ? "server timer" : $"client {senderClientId}";
            Debug.Log($"[NGO Spike] Shared ball impulse #{impulseIndex} from {source}.");
        }

        private void ResetOnServer()
        {
            body.position = resetPosition;
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            nextAutomaticKick = Time.time + 1f;
            Debug.Log("[NGO Spike] Server reset the shared ball.");
        }

        public string LastImpulseSource()
        {
            return LastPushedBy.Value == ServerGeneratedImpulse
                ? "server timer"
                : $"client {LastPushedBy.Value}";
        }
    }
}
