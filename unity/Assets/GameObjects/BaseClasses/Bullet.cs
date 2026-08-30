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

    [Header("Bounce Randomness")]
    [Tooltip("Случайное отклонение угла отскока, в градусах (в обе стороны от идеального отражения).")]
    [SerializeField, Range(0f, 5f)] private float bounceRandomAngleDegrees = 1f;

    private Vector3 direction;
    private float speed;
    private Rigidbody _rb;
    private Collider _collider;
    private Vector3 _spawnPosition;
    private Vector3 _lastPhysicsVelocity;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();

        _rb.useGravity = false;
        _rb.isKinematic = false;
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
        Destroy(gameObject, Mathf.Max(0.1f, maxLifetimeSeconds));
    }

    public void Init(Player owner, Vector3 direction, float speed, float scale)
    {
        GetComponent<PlayerOwnable>().SetOwner(owner);
        this.direction = direction.sqrMagnitude > 0f ? direction.normalized : transform.right;
        this.speed = Mathf.Max(0f, speed);
        transform.localScale = new Vector3(scale, scale, scale);

        _spawnPosition = transform.position;
        _rb.linearVelocity = this.direction * this.speed;
        _lastPhysicsVelocity = _rb.linearVelocity;

        GetComponent<MeshRenderer>().material = owner.playerMaterial;
    }

    private void Update()
    {
        float maxDistance = Mathf.Max(1f, maxDistanceFromSpawn);
        if ((transform.position - _spawnPosition).sqrMagnitude > maxDistance * maxDistance)
            Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        _lastPhysicsVelocity = _rb.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & wallLayer) == 0)
            return;

        if (collision.contactCount == 0)
            return;

        Vector3 incomingVelocity = _lastPhysicsVelocity.sqrMagnitude > 0.0001f
            ? _lastPhysicsVelocity
            : _rb.linearVelocity;
        Vector3 normal = collision.GetContact(0).normal;
        Vector3 reflected = Vector3.Reflect(incomingVelocity, normal);

        reflected.y = 0f;
        if (reflected.sqrMagnitude <= 0.0001f)
            return;

        float randomAngle = Random.Range(-bounceRandomAngleDegrees, bounceRandomAngleDegrees);
        reflected = Quaternion.AngleAxis(randomAngle, Vector3.up) * reflected;

        direction = reflected.normalized;

        _rb.linearVelocity = direction * speed;
        _lastPhysicsVelocity = _rb.linearVelocity;
    }
}
