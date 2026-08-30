using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pochinki.Networking.Spike
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NgoSpikePlayer : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private Renderer visualRenderer;

        private Vector2 serverMoveInput;
        private Vector2 lastSentInput;
        private float nextInputSendTime;

        public void Configure(Renderer renderer)
        {
            visualRenderer = renderer;
        }

        public override void OnNetworkSpawn()
        {
            ApplyOwnerColor();

            if (IsServer)
            {
                float x = ((int)(OwnerClientId % 5) - 2) * 2f;
                transform.position = new Vector3(x, 0.65f, 3f);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || !IsClient || Keyboard.current == null)
            {
                return;
            }

            Vector2 input = ReadMovementInput();
            bool inputChanged = (input - lastSentInput).sqrMagnitude > 0.001f;

            if (inputChanged || Time.unscaledTime >= nextInputSendTime)
            {
                SubmitMovementRpc(input);
                lastSentInput = input;
                nextInputSendTime = Time.unscaledTime + 0.1f;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                PushSharedBallRpc();
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            Vector3 movement = new Vector3(serverMoveInput.x, 0f, serverMoveInput.y) * (moveSpeed * Time.fixedDeltaTime);
            Vector3 nextPosition = transform.position + movement;
            nextPosition.x = Mathf.Clamp(nextPosition.x, -6f, 6f);
            nextPosition.z = Mathf.Clamp(nextPosition.z, -3.5f, 4f);
            transform.position = nextPosition;
        }

        [Rpc(SendTo.Server)]
        private void SubmitMovementRpc(Vector2 input)
        {
            serverMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        [Rpc(SendTo.Server)]
        private void PushSharedBallRpc(RpcParams rpcParams = default)
        {
            NgoSpikeBall.Instance?.ApplyImpulseOnServer(rpcParams.Receive.SenderClientId);
        }

        private static Vector2 ReadMovementInput()
        {
            Keyboard keyboard = Keyboard.current;
            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                horizontal -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                horizontal += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                vertical -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                vertical += 1f;
            }

            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private void ApplyOwnerColor()
        {
            if (visualRenderer == null)
            {
                visualRenderer = GetComponentInChildren<Renderer>();
            }

            if (visualRenderer == null)
            {
                return;
            }

            float hue = (OwnerClientId * 0.217f + 0.56f) % 1f;
            Color color = Color.HSVToRGB(hue, 0.7f, 1f);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            visualRenderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            visualRenderer.SetPropertyBlock(properties);
        }
    }
}
