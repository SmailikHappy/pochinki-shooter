using System.Collections;
using Pochinki.Networking.Game;
using UnityEngine;

public class PachinkoField : MonoBehaviour
{
    [Header("Ball")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float respawnDelay = 0.3f;

    [Header("Local Gravity")]
    [Tooltip("Gravity direction in this field's local space. Vector3.down becomes world -Z when the field root is rotated X=90 degrees.")]
    [SerializeField] private Vector3 localGravityDirection = Vector3.down;
    [SerializeField, Min(0f)] private float gravityAcceleration = 5f;

    [Header("Zones")]
    [SerializeField] private ScoreZone zoneR;
    [SerializeField] private ScoreZone zoneMultiplier;
    [SerializeField] private ScoreZone zoneEvent;
    [SerializeField] private PachinkoCounter counter;

    public Player Owner { get; private set; }
    private Canon linkedCanon;
    private bool _initialized;
    private PachinkoBall _activeBall;
    private PachinkoBall _ballPendingReset;
    private Coroutine _respawnRoutine;

    public int PlayerSlot { get; private set; } = -1;
    public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    public Quaternion SpawnRotation => spawnPoint != null ? spawnPoint.rotation : transform.rotation;
    public PachinkoCounter Counter => counter;
    public int MultiplierValue => zoneMultiplier != null
        ? Mathf.Max(1, Mathf.RoundToInt(zoneMultiplier.MultiplierValue))
        : 2;

    public Vector3 GravityAcceleration
    {
        get
        {
            Vector3 localDirection = localGravityDirection.sqrMagnitude > 0.0001f
                ? localGravityDirection.normalized
                : Vector3.down;
            return transform.TransformDirection(localDirection) * Mathf.Max(0f, gravityAcceleration);
        }
    }

    public void Initialize(Player owner, Canon canon, int playerSlot = -1)
    {
        if (counter != null && linkedCanon != null)
            counter.OnBulletRequested.RemoveListener(linkedCanon.Fire);

        Owner = owner;
        linkedCanon = canon;
        PlayerSlot = playerSlot;

        NetworkGameBootstrap networkBootstrap = NetworkGameBootstrap.Instance;
        bool networkControlled = networkBootstrap != null && networkBootstrap.ControlsGameplayRoster;
        counter?.ConfigureNetworkMode(networkControlled);

        if (!networkControlled && counter != null && linkedCanon != null)
            counter.OnBulletRequested.AddListener(linkedCanon.Fire);

        _initialized = true;
        SpawnBall();
    }

    private void OnDestroy()
    {
        if (counter != null && linkedCanon != null)
            counter.OnBulletRequested.RemoveListener(linkedCanon.Fire);

        if (_activeBall != null && _activeBall.IsPersistentNetworkBall)
            _activeBall.DetachFromField(this);
    }

    private void SpawnBall()
    {
        if (!_initialized) return;

        if (_activeBall != null)
            return;

        if (ballPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("PachinkoField: не задан ballPrefab или spawnPoint.", this);
            return;
        }

        NetworkGameBootstrap networkBootstrap = NetworkGameBootstrap.Instance;
        if (networkBootstrap != null && networkBootstrap.ControlsGameplayRoster)
        {
            if (PlayerSlot < 0)
            {
                Debug.LogWarning("PachinkoField: network player slot is not assigned.", this);
                return;
            }

            if (networkBootstrap.TryGetPachinkoBallForSlot(PlayerSlot, out NetworkPachinkoBall existingBall))
            {
                existingBall.BindToGameplayField();
                return;
            }

            networkBootstrap.TrySpawnPachinkoBall(this, PlayerSlot);
            return;
        }

        GameObject ballObject = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
        PachinkoBall ball = ballObject.GetComponent<PachinkoBall>();

        if (ball != null)
        {
            _activeBall = ball;
            ball.Initialize(this);
        }
        else
        {
            Debug.LogWarning("На Ball Prefab нет PachinkoBall.", ballObject);
            Destroy(ballObject);
        }
    }

    public void SetZonesActive(bool active)
    {
        if (zoneR != null) zoneR.SetActive(active);
        if (zoneMultiplier != null) zoneMultiplier.SetActive(active);
        if (zoneEvent != null) zoneEvent.SetActive(active);
    }

    public void OnBallScored(ScoreZone zone, PachinkoBall ball)
    {
        if (ball == null || ball != _activeBall)
            return;

        bool persistentNetworkBall = ball.IsPersistentNetworkBall;

        if (persistentNetworkBall &&
            NetworkGameBootstrap.Instance != null &&
            NetworkGameBootstrap.Instance.ControlsGameplayRoster)
        {
            if (ball.ReportNetworkZoneHit(zone))
                ScheduleRespawn(ball);

            return;
        }

        if (!persistentNetworkBall)
            _activeBall = null;

        if (counter == null)
        {
            Debug.LogError("PachinkoField: не назначен PachinkoCounter!", this);
            ScheduleRespawn(persistentNetworkBall ? ball : null);
            return;
        }

        switch (zone.ZoneType)
        {
            case ScoreZoneType.R:
                counter.Release();
                break;

            case ScoreZoneType.Multiplier:
                counter.Multiply(Mathf.RoundToInt(zone.MultiplierValue));
                break;

            case ScoreZoneType.Event:
                counter.TriggerEvent();
                break;
        }

        ScheduleRespawn(persistentNetworkBall ? ball : null);
    }

    public void OnBallLost(PachinkoBall ball)
    {
        if (ball == null || ball != _activeBall)
            return;

        bool persistentNetworkBall = ball.IsPersistentNetworkBall;
        if (!persistentNetworkBall)
            _activeBall = null;

        ScheduleRespawn(persistentNetworkBall ? ball : null);
    }

    public void AttachNetworkBall(PachinkoBall ball)
    {
        if (ball == null)
            return;

        _activeBall = ball;
    }

    private void ScheduleRespawn(PachinkoBall persistentBall = null)
    {
        if (_respawnRoutine != null)
            StopCoroutine(_respawnRoutine);

        _ballPendingReset = persistentBall;
        _respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));
        _respawnRoutine = null;

        PachinkoBall ballToReset = _ballPendingReset;
        _ballPendingReset = null;

        if (ballToReset != null && ballToReset.IsPersistentNetworkBall)
        {
            ballToReset.ResetForNextRun();
        }
        else
        {
            SpawnBall();
        }
    }
}
