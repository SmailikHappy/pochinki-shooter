using Pochinki.Networking.Game;
using UnityEngine;

[RequireComponent(typeof(PlayerOwnable))]
[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    private const string BulletLayerName = "Bullet";

    [SerializeField] private LayerMask wallLayer;

    [Header("Lifetime")]
    [SerializeField, Min(0.1f)] private float maxLifetimeSeconds = 20f;
    [SerializeField, Min(1f)] private float maxDistanceFromSpawn = 50f;

    private Vector3 direction;
    private float speed;
    private Rigidbody _rb;
    private Collider _collider;
    private NetworkBullet _networkBullet;
    private Vector3 _spawnPosition;
    private Vector3 _lastPhysicsVelocity;
    private float _destroyAt;
    private bool _simulationActive;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _networkBullet = GetComponent<NetworkBullet>();

        _rb.useGravity = false;
        _rb.isKinematic = _networkBullet != null;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        _collider.isTrigger = false;

        int bulletLayer = LayerMask.NameToLayer(BulletLayerName);
        if (bulletLayer >= 0 && gameObject.layer == bulletLayer &&
            !Physics.GetIgnoreLayerCollision(bulletLayer, bulletLayer))
        {
            Physics.IgnoreLayerCollision(bulletLayer, bulletLayer, true);
        }

        _spawnPosition = transform.position;
    }

    public void Init(Player owner, Vector3 direction, float speed, float scale)
    {
        BindNetworkOwner(owner);
        this.direction = direction.sqrMagnitude > 0f ? direction.normalized : transform.right;
        this.speed = Mathf.Max(0f, speed);
        transform.localScale = new Vector3(scale, scale, scale);

        _spawnPosition = transform.position;
        _destroyAt = Time.time + Mathf.Max(0.1f, maxLifetimeSeconds);
        _simulationActive = true;
        _rb.isKinematic = false;
        _rb.linearVelocity = this.direction * this.speed;
        _lastPhysicsVelocity = _rb.linearVelocity;
    }

    public void PrepareNetworkSpawn(Player owner, float scale)
    {
        BindNetworkOwner(owner);
        transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        _spawnPosition = transform.position;
    }

    public void BeginServerNetworkSimulation(Vector3 launchDirection, float launchSpeed)
    {
        direction = launchDirection.sqrMagnitude > 0f ? launchDirection.normalized : transform.right;
        speed = Mathf.Max(0f, launchSpeed);
        _spawnPosition = transform.position;
        _destroyAt = Time.time + Mathf.Max(0.1f, maxLifetimeSeconds);
        _simulationActive = true;
        _rb.isKinematic = false;
        _rb.linearVelocity = direction * speed;
        _lastPhysicsVelocity = _rb.linearVelocity;
    }

    public void BindNetworkOwner(Player owner)
    {
        GetComponent<PlayerOwnable>().SetOwner(owner);

        if (owner != null && owner.playerMaterial != null)
            GetComponent<MeshRenderer>().sharedMaterial = owner.playerMaterial;
    }

    private void Update()
    {
        if (!_simulationActive)
            return;

        if (_networkBullet != null && (!_networkBullet.IsSpawned || !_networkBullet.IsServer))
            return;

        float maxDistance = Mathf.Max(1f, maxDistanceFromSpawn);
        if ((transform.position - _spawnPosition).sqrMagnitude > maxDistance * maxDistance ||
            Time.time >= _destroyAt)
            DestroyBullet();
    }

    private void FixedUpdate()
    {
        if (!_simulationActive ||
            (_networkBullet != null && (!_networkBullet.IsSpawned || !_networkBullet.IsServer)))
            return;

        _lastPhysicsVelocity = _rb.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_simulationActive ||
            (_networkBullet != null && (!_networkBullet.IsSpawned || !_networkBullet.IsServer)))
            return;

        if (((1 << collision.gameObject.layer) & wallLayer) == 0)
            return;

        if (collision.contactCount == 0)
            return;

        Vector3 incomingVelocity = _lastPhysicsVelocity.sqrMagnitude > 0.0001f
            ? _lastPhysicsVelocity
            : _rb.linearVelocity;
        Vector3 normal = collision.GetContact(0).normal;
        Vector3 reflected = Vector3.Reflect(incomingVelocity, normal);

        reflected.y = 0f; // прижимаем пулю к горизонтальной плоскости после отскока
        if (reflected.sqrMagnitude <= 0.0001f)
            return;

        direction = reflected.normalized;

        _rb.linearVelocity = direction * speed;
        _lastPhysicsVelocity = _rb.linearVelocity;
    }

    public void DestroyBullet()
    {
        _simulationActive = false;

        if (_networkBullet != null && _networkBullet.IsSpawned)
        {
            _networkBullet.DespawnOnServer();
            return;
        }

        Destroy(gameObject);
    }
}
