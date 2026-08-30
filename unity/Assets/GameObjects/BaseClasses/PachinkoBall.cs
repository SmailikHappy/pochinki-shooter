using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PachinkoBall : MonoBehaviour
{
    [SerializeField] private PachinkoField field;

    [Header("Случайный старт")]
    [SerializeField] private float randomSidewaysForce = 1.5f;
    [SerializeField] private float randomDepthForce = 0.5f;

    [Header("Safety")]
    [SerializeField, Min(1f)] private float maxLifetimeSeconds = 20f;
    [SerializeField, Min(1f)] private float maxDistanceFromSpawn = 10f;

    private Rigidbody _rb;
    private bool _consumed;
    private bool _initialized;
    private Vector3 _spawnPosition;
    private float _spawnTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
    }

    public void Initialize(PachinkoField ownerField)
    {
        field = ownerField;
        _spawnPosition = transform.position;
        _spawnTime = Time.time;
        _initialized = true;
    }

    private void Start()
    {
        ApplyRandomLaunchForce();
    }

    private void ApplyRandomLaunchForce()
    {
        float x = Random.Range(-randomSidewaysForce, randomSidewaysForce);
        float down = Random.Range(-randomDepthForce, randomDepthForce);

        Vector3 sidewaysDirection = field != null ? field.transform.right : transform.right;
        Vector3 gravityDirection = field != null && field.GravityAcceleration.sqrMagnitude > 0.0001f
            ? field.GravityAcceleration.normalized
            : -transform.up;

        _rb.AddForce(sidewaysDirection * x + gravityDirection * down, ForceMode.Impulse);
    }

    private void FixedUpdate()
    {
        if (!_initialized || _consumed)
            return;

        if (field == null)
        {
            ForceRemoveAndRespawn();
            return;
        }

        _rb.AddForce(field.GravityAcceleration, ForceMode.Acceleration);

        float maxDistance = Mathf.Max(1f, maxDistanceFromSpawn);
        bool isTooFar = (transform.position - _spawnPosition).sqrMagnitude > maxDistance * maxDistance;
        bool hasTimedOut = Time.time - _spawnTime > Mathf.Max(1f, maxLifetimeSeconds);

        if (isTooFar || hasTimedOut)
            ForceRemoveAndRespawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;

        ScoreZone zone = other.GetComponent<ScoreZone>();
        if (zone == null) return;
        if (!zone.IsActive) return; // защита от гонки состояний в момент переключения isTrigger

        _consumed = true;
        if (field != null)
            field.OnBallScored(zone, this);
        DestroyBall();
    }

    public void DestroyBall()
    {
        Destroy(gameObject);
    }

    public void ForceRemoveAndRespawn()
    {
        if (_consumed) return;
        _consumed = true;
        if (field != null)
            field.OnBallLost(this);
        DestroyBall();
    }

}
