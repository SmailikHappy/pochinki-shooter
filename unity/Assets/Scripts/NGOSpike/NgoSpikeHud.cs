using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Pochinki.Networking.Spike
{
    [DisallowMultipleComponent]
    public sealed class NgoSpikeHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text controlsText;
        [SerializeField] private TMP_Text physicsText;

        public void Configure(TMP_Text status, TMP_Text controls, TMP_Text physics)
        {
            statusText = status;
            controlsText = controls;
            physicsText = physics;
        }

        private void Awake()
        {
            if (controlsText != null)
            {
                controlsText.text = "WASD / arrows: move your server-owned cube    SPACE: push the shared ball";
            }
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                SetStatus("NGO DEDICATED SERVER SPIKE\nStatus: NetworkManager missing");
                SetPhysics("Waiting for the shared server ball...");
                return;
            }

            string status;
            if (manager.IsConnectedClient)
            {
                int playerCount = FindObjectsByType<NgoSpikePlayer>(FindObjectsSortMode.None).Length;
                status = $"CONNECTED  |  client {manager.LocalClientId}  |  players {playerCount}";
            }
            else if (manager.IsClient)
            {
                status = "CONNECTING";
            }
            else
            {
                status = "OFFLINE";
            }

            string endpoint = NgoSpikeBootstrap.Instance != null
                ? NgoSpikeBootstrap.Instance.EndpointDescription
                : "unknown endpoint";
            SetStatus($"NGO DEDICATED SERVER SPIKE\nStatus: {status}\n{endpoint}");

            NgoSpikeBall ball = NgoSpikeBall.Instance;
            if (ball == null || !ball.IsSpawned)
            {
                SetPhysics("Waiting for the shared server ball...");
                return;
            }

            Vector3 position = ball.transform.position;
            SetPhysics(
                $"Shared ball: ({position.x:0.00}, {position.y:0.00}, {position.z:0.00})    " +
                $"impulses: {ball.ImpulseCount.Value}    last: {ball.LastImpulseSource()}");
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
            }
        }

        private void SetPhysics(string value)
        {
            if (physicsText != null)
            {
                physicsText.text = value;
            }
        }
    }
}
