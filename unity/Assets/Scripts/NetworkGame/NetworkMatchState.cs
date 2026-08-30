using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Pochinki.Networking.Game
{
    public enum NetworkMatchPhase : byte
    {
        WaitingForPlayers,
        InProgress,
        GameOver,
    }

    /// <summary>
    /// Compact server-owned gameplay truth. Scene geometry remains authored and
    /// instantiated locally, while counters, territory and match outcome replicate
    /// from this one in-scene NetworkObject.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMatchState : NetworkBehaviour
    {
        public const byte NeutralSlot = byte.MaxValue;
        public const int MaxCounterValue = 128;

        [SerializeField, Min(0.01f)] private float releaseShotInterval = 0.15f;

        private readonly NetworkList<int> counterValues = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkList<byte> releasingSlots = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkList<uint> eventVersions = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkList<byte> pixelOwners = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> eliminatedMask = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<byte> winnerSlot = new(
            NeutralSlot,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<NetworkMatchPhase> phase = new(
            NetworkMatchPhase.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly Coroutine[] releaseRoutines =
            new Coroutine[NetworkGameBootstrap.MaxSupportedPlayers];
        private readonly uint[] observedEventVersions =
            new uint[NetworkGameBootstrap.MaxSupportedPlayers];

        private string serverRosterSignature = string.Empty;
        private Coroutine applyRoutine;
        private bool eventVersionsInitialized;
        private bool resettingServerState;

        public static NetworkMatchState Instance { get; private set; }

        public NetworkMatchPhase Phase => phase.Value;
        public int WinnerSlot => winnerSlot.Value == NeutralSlot ? -1 : winnerSlot.Value;
        public byte EliminatedMask => eliminatedMask.Value;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            counterValues.OnListChanged += HandleCounterChanged;
            releasingSlots.OnListChanged += HandleReleaseChanged;
            eventVersions.OnListChanged += HandleEventChanged;
            pixelOwners.OnListChanged += HandlePixelChanged;
            eliminatedMask.OnValueChanged += HandleOutcomeChanged;
            winnerSlot.OnValueChanged += HandleWinnerChanged;
            phase.OnValueChanged += HandlePhaseChanged;

            if (IsServer)
            {
                EnsureSlotLists();
            }

            QueueApplySnapshot();
        }

        public override void OnNetworkDespawn()
        {
            counterValues.OnListChanged -= HandleCounterChanged;
            releasingSlots.OnListChanged -= HandleReleaseChanged;
            eventVersions.OnListChanged -= HandleEventChanged;
            pixelOwners.OnListChanged -= HandlePixelChanged;
            eliminatedMask.OnValueChanged -= HandleOutcomeChanged;
            winnerSlot.OnValueChanged -= HandleWinnerChanged;
            phase.OnValueChanged -= HandlePhaseChanged;

            StopReleaseRoutines();

            if (applyRoutine != null)
            {
                StopCoroutine(applyRoutine);
                applyRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void HandleGameplayRebuilt()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                string rosterSignature = GameHandler.instance?.NetworkRosterSignature ?? string.Empty;
                if (!string.Equals(serverRosterSignature, rosterSignature, System.StringComparison.Ordinal))
                {
                    ResetServerMatch(rosterSignature);
                    return;
                }
            }

            QueueApplySnapshot();
        }

        public bool TryApplyPachinkoZoneHit(int slot, ScoreZoneType zoneType)
        {
            if (!IsServer || !IsSlotGameplayActive(slot) || IsSlotReleasing(slot))
            {
                return false;
            }

            EnsureSlotLists();

            switch (zoneType)
            {
                case ScoreZoneType.Multiplier:
                {
                    int multiplier = 2;
                    if (GameHandler.instance != null &&
                        GameHandler.instance.TryGetPachinkoFieldForSlot(slot, out PachinkoField field))
                    {
                        multiplier = field.MultiplierValue;
                    }

                    long nextValue = (long)counterValues[slot] * Mathf.Max(1, multiplier);
                    counterValues[slot] = (int)Mathf.Min(nextValue, MaxCounterValue);
                    break;
                }

                case ScoreZoneType.R:
                    BeginRelease(slot);
                    break;

                case ScoreZoneType.Event:
                    eventVersions[slot] = eventVersions[slot] + 1;
                    break;

                default:
                    return false;
            }

            ApplySnapshotToGameplay();
            return true;
        }

        public bool TryCapturePixel(int pixelIndex, int shooterSlot)
        {
            if (!IsServer || !IsSlotGameplayActive(shooterSlot))
            {
                return false;
            }

            if (pixelIndex < 0 || pixelIndex >= pixelOwners.Count)
            {
                return false;
            }

            byte currentOwner = pixelOwners[pixelIndex];
            if (currentOwner == shooterSlot)
            {
                return false;
            }

            pixelOwners[pixelIndex] = (byte)shooterSlot;

            if (GameHandler.instance != null &&
                GameHandler.instance.TryGetPixel(pixelIndex, out Pixel pixel) &&
                pixel.IsMasterPixel &&
                pixel.MasterOwnerSlot >= 0 &&
                pixel.MasterOwnerSlot != shooterSlot)
            {
                EliminateSlot(pixel.MasterOwnerSlot);
            }

            ApplySnapshotToGameplay();
            return true;
        }

        public bool IsSlotEliminated(int slot)
        {
            return slot >= 0 &&
                slot < NetworkGameBootstrap.MaxSupportedPlayers &&
                (eliminatedMask.Value & (1 << slot)) != 0;
        }

        private void ResetServerMatch(string rosterSignature)
        {
            if (!IsServer)
            {
                return;
            }

            StopReleaseRoutines();
            NetworkGameBootstrap.Instance?.DespawnAllNetworkBullets();
            serverRosterSignature = rosterSignature;
            resettingServerState = true;

            counterValues.Clear();
            releasingSlots.Clear();
            eventVersions.Clear();

            for (int slot = 0; slot < NetworkGameBootstrap.MaxSupportedPlayers; slot++)
            {
                counterValues.Add(1);
                releasingSlots.Add(0);
                eventVersions.Add(0);
                NetworkGameBootstrap.Instance?.SetSessionPlayerEliminated(slot, false);
            }

            pixelOwners.Clear();
            GameSurface surface = GameHandler.instance?.Surface;
            if (surface != null)
            {
                foreach (Pixel pixel in surface.SpawnedPixels)
                {
                    int ownerSlot = pixel != null ? pixel.InitialOwnerSlot : -1;
                    pixelOwners.Add(ownerSlot >= 0 ? (byte)ownerSlot : NeutralSlot);
                }
            }

            eliminatedMask.Value = 0;
            winnerSlot.Value = NeutralSlot;
            System.Array.Clear(observedEventVersions, 0, observedEventVersions.Length);
            phase.Value = GameHandler.instance != null && GameHandler.instance.ActivePlayerCount > 0
                ? NetworkMatchPhase.InProgress
                : NetworkMatchPhase.WaitingForPlayers;

            resettingServerState = false;
            eventVersionsInitialized = false;
            ApplySnapshotToGameplay();
            Debug.Log(
                $"[Network Game] Match state reset for {GameHandler.instance?.ActivePlayerCount ?? 0} players and {pixelOwners.Count} pixels.",
                this);
        }

        private void EnsureSlotLists()
        {
            if (!IsServer)
            {
                return;
            }

            while (counterValues.Count < NetworkGameBootstrap.MaxSupportedPlayers)
                counterValues.Add(1);
            while (releasingSlots.Count < NetworkGameBootstrap.MaxSupportedPlayers)
                releasingSlots.Add(0);
            while (eventVersions.Count < NetworkGameBootstrap.MaxSupportedPlayers)
                eventVersions.Add(0);
        }

        private void BeginRelease(int slot)
        {
            if (!IsServer || IsSlotReleasing(slot) || releaseRoutines[slot] != null)
            {
                return;
            }

            releasingSlots[slot] = 1;
            releaseRoutines[slot] = StartCoroutine(ReleaseRoutine(slot));
        }

        private IEnumerator ReleaseRoutine(int slot)
        {
            int shotsRemaining = Mathf.Clamp(counterValues[slot], 1, MaxCounterValue);
            var delay = new WaitForSeconds(Mathf.Max(0.01f, releaseShotInterval));

            while (shotsRemaining > 0 && IsSlotGameplayActive(slot))
            {
                if (GameHandler.instance == null ||
                    !GameHandler.instance.TryGetCanonForSlot(slot, out Canon canon) ||
                    canon == null)
                {
                    Debug.LogWarning($"[Network Game] Release stopped because slot {slot} has no cannon.", this);
                    break;
                }

                canon.Fire();
                shotsRemaining--;
                counterValues[slot] = shotsRemaining;

                if (shotsRemaining > 0)
                {
                    yield return delay;
                }
            }

            counterValues[slot] = 1;
            releasingSlots[slot] = 0;
            releaseRoutines[slot] = null;
            ApplySnapshotToGameplay();
        }

        private void EliminateSlot(int slot)
        {
            if (!IsServer || !IsSlotGameplayActive(slot))
            {
                return;
            }

            eliminatedMask.Value = (byte)(eliminatedMask.Value | (1 << slot));
            NetworkGameBootstrap.Instance?.SetSessionPlayerEliminated(slot, true);

            if (releaseRoutines[slot] != null)
            {
                StopCoroutine(releaseRoutines[slot]);
                releaseRoutines[slot] = null;
                releasingSlots[slot] = 0;
                counterValues[slot] = 1;
            }

            int activePlayers = 0;
            int survivingSlot = -1;
            int rosterPlayers = GameHandler.instance?.ActivePlayerCount ?? 0;

            if (GameHandler.instance != null)
            {
                foreach (int candidate in GameHandler.instance.ActiveNetworkSlots)
                {
                    if (!IsSlotEliminated(candidate))
                    {
                        activePlayers++;
                        survivingSlot = candidate;
                    }
                }
            }

            if (rosterPlayers >= 2 && activePlayers <= 1)
            {
                winnerSlot.Value = survivingSlot >= 0 ? (byte)survivingSlot : NeutralSlot;
                phase.Value = NetworkMatchPhase.GameOver;
                StopReleaseRoutines();
                NetworkGameBootstrap.Instance?.DespawnAllNetworkBullets();
            }

            ApplySnapshotToGameplay();
        }

        private bool IsSlotGameplayActive(int slot)
        {
            return phase.Value == NetworkMatchPhase.InProgress &&
                slot >= 0 &&
                slot < NetworkGameBootstrap.MaxSupportedPlayers &&
                GameHandler.instance != null &&
                GameHandler.instance.IsNetworkSlotActive(slot) &&
                !IsSlotEliminated(slot);
        }

        private bool IsSlotReleasing(int slot)
        {
            return slot >= 0 && slot < releasingSlots.Count && releasingSlots[slot] != 0;
        }

        private void StopReleaseRoutines()
        {
            for (int slot = 0; slot < releaseRoutines.Length; slot++)
            {
                if (releaseRoutines[slot] != null)
                {
                    StopCoroutine(releaseRoutines[slot]);
                    releaseRoutines[slot] = null;
                }

                if (IsServer && slot < releasingSlots.Count)
                {
                    releasingSlots[slot] = 0;
                }
            }
        }

        private void ApplySnapshotToGameplay()
        {
            if (resettingServerState || GameHandler.instance == null)
            {
                return;
            }

            int slotCount = Mathf.Min(
                NetworkGameBootstrap.MaxSupportedPlayers,
                Mathf.Min(counterValues.Count, releasingSlots.Count));

            for (int slot = 0; slot < slotCount; slot++)
            {
                uint eventVersion = slot < eventVersions.Count ? eventVersions[slot] : 0;
                bool triggerEvent = eventVersionsInitialized &&
                    eventVersion > observedEventVersions[slot];
                observedEventVersions[slot] = eventVersion;

                GameHandler.instance.ApplyNetworkCounterState(
                    slot,
                    counterValues[slot],
                    releasingSlots[slot] != 0,
                    triggerEvent);
            }

            eventVersionsInitialized = true;

            int pixelCount = Mathf.Min(pixelOwners.Count, GameHandler.instance.PixelCount);
            for (int index = 0; index < pixelCount; index++)
            {
                int ownerSlot = pixelOwners[index] == NeutralSlot ? -1 : pixelOwners[index];
                GameHandler.instance.ApplyNetworkPixelOwner(index, ownerSlot);
            }

            GameHandler.instance.ApplyNetworkMatchOutcome(
                eliminatedMask.Value,
                WinnerSlot,
                phase.Value);
        }

        private void QueueApplySnapshot()
        {
            if (!isActiveAndEnabled || applyRoutine != null)
            {
                return;
            }

            applyRoutine = StartCoroutine(ApplyWhenGameplayReady());
        }

        private IEnumerator ApplyWhenGameplayReady()
        {
            while (IsSpawned)
            {
                if (GameHandler.instance != null && GameHandler.instance.IsGameplayReadyForNetworkState)
                {
                    applyRoutine = null;
                    ApplySnapshotToGameplay();
                    yield break;
                }

                yield return null;
            }

            applyRoutine = null;
        }

        private void HandleCounterChanged(NetworkListEvent<int> changeEvent)
        {
            if (!resettingServerState)
                QueueApplySnapshot();
        }

        private void HandleReleaseChanged(NetworkListEvent<byte> changeEvent)
        {
            if (!resettingServerState)
                QueueApplySnapshot();
        }

        private void HandleEventChanged(NetworkListEvent<uint> changeEvent)
        {
            if (!resettingServerState)
                QueueApplySnapshot();
        }

        private void HandlePixelChanged(NetworkListEvent<byte> changeEvent)
        {
            if (!resettingServerState)
                QueueApplySnapshot();
        }

        private void HandleOutcomeChanged(byte previousValue, byte newValue)
        {
            QueueApplySnapshot();
        }

        private void HandleWinnerChanged(byte previousValue, byte newValue)
        {
            QueueApplySnapshot();
        }

        private void HandlePhaseChanged(NetworkMatchPhase previousValue, NetworkMatchPhase newValue)
        {
            QueueApplySnapshot();
        }
    }
}
