using System.Collections;
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
    [SerializeField] private PachinkoCounter counter;

    public Player Owner { get; private set; }
    private Canon linkedCanon;
    private bool _initialized;
    private PachinkoBall _activeBall;
    private Coroutine _respawnRoutine;

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

    public void Initialize(Player owner, Canon canon)
    {
        if (counter != null && linkedCanon != null)
            counter.OnBulletRequested.RemoveListener(linkedCanon.Fire);

        Owner = owner;
        linkedCanon = canon;

        if (counter != null && linkedCanon != null)
            counter.OnBulletRequested.AddListener(linkedCanon.Fire);

        _initialized = true;
        SpawnBall();
    }

    private void OnDestroy()
    {
        if (counter != null && linkedCanon != null)
            counter.OnBulletRequested.RemoveListener(linkedCanon.Fire);
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
    }

    public void OnBallScored(ScoreZone zone, PachinkoBall ball)
    {
        if (ball == null || ball != _activeBall)
            return;

        _activeBall = null;

        if (counter == null)
        {
            Debug.LogError("PachinkoField: не назначен PachinkoCounter!", this);
            ScheduleRespawn();
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
        }

        ScheduleRespawn();
    }

    public void OnBallLost(PachinkoBall ball)
    {
        if (ball == null || ball != _activeBall)
            return;

        _activeBall = null;
        ScheduleRespawn();
    }

    private void ScheduleRespawn()
    {
        if (_respawnRoutine != null)
            StopCoroutine(_respawnRoutine);

        _respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));
        _respawnRoutine = null;
        SpawnBall();
    }
}
