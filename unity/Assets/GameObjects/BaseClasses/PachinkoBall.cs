using UnityEngine;
using Pochinki.Networking.Game;

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
    private Collider[] _physicsColliders;
    private NetworkPachinkoBall _networkBall;
    private bool _usesNetworkSimulation;
    private bool _consumed;
    private bool _initialized;
    private Vector3 _spawnPosition;
    private float _spawnTime;

    private void Awake()
    {
        Pochinki.WebRuntimePerformance.OptimizeMassRenderers(gameObject);

        _rb = GetComponent<Rigidbody>();
        _physicsColliders = GetComponentsInChildren<Collider>(true);
        _networkBall = GetComponent<NetworkPachinkoBall>();
        _usesNetworkSimulation = _networkBall != null;
        _rb.useGravity = false;

        // Network prefabs must stay inert between Instantiate and NGO spawn.
        // The final owner role is applied from OnNetworkPostSpawn, after
        // NetworkRigidbody has completed its own spawn callback.
        if (_networkBall != null)
            SetNetworkPhysicsAuthority(false);
    }

    public bool IsPersistentNetworkBall => _networkBall != null && _networkBall.IsSpawned;
    public bool HasSimulationAuthority => !_usesNetworkSimulation || _networkBall.HasPhysicsAuthority;

    public void Initialize(PachinkoField ownerField, bool resetAndLaunch = true)
    {
        field = ownerField;
        _spawnPosition = field != null ? field.SpawnPosition : transform.position;
        _initialized = true;

        ApplyOwnerColor();

        NetworkGameBootstrap bootstrap = NetworkGameBootstrap.Instance;
        _usesNetworkSimulation = _networkBall != null &&
            (_networkBall.IsSpawned ||
                (bootstrap != null && bootstrap.ControlsGameplayRoster));

        if (!_usesNetworkSimulation)
            SetStandalonePhysicsActive();

        if (resetAndLaunch && HasSimulationAuthority)
        {
            ResetForNextRun();
        }
    }

    private void ApplyOwnerColor()
    {
        if (field == null || field.Owner == null || field.Owner.playerMaterial == null)
            return;

        Renderer ballRenderer = GetComponent<Renderer>();
        if (ballRenderer != null)
            ballRenderer.material = field.Owner.playerMaterial;
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
        if (!_initialized || _consumed || !HasSimulationAuthority)
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
        if (_consumed || !HasSimulationAuthority) return;

        ScoreZone zone = other.GetComponent<ScoreZone>();
        if (zone == null) return;
        if (!zone.IsActive) return; // защита от гонки состояний в момент переключения isTrigger

        _consumed = true;
        if (field != null)
            field.OnBallScored(zone, this);

        if (!IsPersistentNetworkBall)
            DestroyBall();
    }

    public void DestroyBall()
    {
        if (!IsPersistentNetworkBall)
            Destroy(gameObject);
    }

    public void ForceRemoveAndRespawn()
    {
        if (_consumed) return;
        _consumed = true;
        if (field != null)
            field.OnBallLost(this);

        if (!IsPersistentNetworkBall)
            DestroyBall();
    }

    public void ResetForNextRun()
    {
        if (!_initialized || field == null || !HasSimulationAuthority)
            return;

        _spawnPosition = field.SpawnPosition;
        _spawnTime = Time.time;
        _consumed = false;

        _rb.isKinematic = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        if (IsPersistentNetworkBall)
        {
            _networkBall.TeleportFromOwner(field.SpawnPosition, field.SpawnRotation);
        }
        else
        {
            transform.SetPositionAndRotation(field.SpawnPosition, field.SpawnRotation);
        }

        ApplyRandomLaunchForce();
    }

    public void ApplyOwnerForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (_initialized && !_consumed && HasSimulationAuthority)
        {
            _rb.AddForce(force, mode);
        }
    }

    public bool ReportNetworkZoneHit(ScoreZone zone)
    {
        return zone != null &&
            _networkBall != null &&
            _networkBall.ReportZoneHit(zone.ZoneType);
    }

    public void DetachFromField(PachinkoField expectedField = null)
    {
        if (expectedField != null && field != expectedField)
            return;

        field = null;
        _initialized = false;
    }

    public void SetNetworkPhysicsAuthority(bool ownerActive)
    {
        if (_networkBall == null)
            return;

        _usesNetworkSimulation = true;
        ApplyPhysicsState(ownerActive);
    }

    private void SetStandalonePhysicsActive()
    {
        ApplyPhysicsState(true);
    }

    private void ApplyPhysicsState(bool active)
    {
        if (!active)
        {
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }

        _rb.isKinematic = !active;
        _rb.detectCollisions = active;

        if (_physicsColliders == null)
            _physicsColliders = GetComponentsInChildren<Collider>(true);

        foreach (Collider physicsCollider in _physicsColliders)
        {
            if (physicsCollider != null)
                physicsCollider.enabled = active;
        }
    }

}
