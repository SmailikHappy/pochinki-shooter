using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class Ball : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxLifetime = 20f;

    private GambleSurface _owner;
    private PhysicsMaterial _runtimePhysicsMaterial;
    private float _despawnY = -10f;
    private float _spawnedAt;

    public bool IsResolved { get; private set; }
    public GambleSurface Owner => _owner;

    private void Awake()
    {
        ConfigureBody();
    }

    private void OnEnable()
    {
        IsResolved = false;
        _spawnedAt = Time.time;
    }

    private void Update()
    {
        if (IsResolved || !Application.isPlaying)
        {
            return;
        }

        if (transform.position.y > _despawnY && Time.time - _spawnedAt < maxLifetime)
        {
            return;
        }

        if (_owner != null)
        {
            _owner.HandleMissedBall(this);
            return;
        }

        IsResolved = true;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_runtimePhysicsMaterial != null)
        {
            Destroy(_runtimePhysicsMaterial);
        }
    }

    public void Configure(GambleSurface owner, float despawnY)
    {
        _owner = owner;
        _despawnY = despawnY;
        _spawnedAt = Time.time;
        IsResolved = false;
        ConfigureBody();
    }

    public bool TryResolve()
    {
        if (IsResolved)
        {
            return false;
        }

        IsResolved = true;
        return true;
    }

    private void ConfigureBody()
    {
        var body = GetComponent<Rigidbody>();

        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezePositionZ;

        var sphereCollider = GetComponent<SphereCollider>();

        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();
        }

        if (sphereCollider.sharedMaterial == null && Application.isPlaying)
        {
            _runtimePhysicsMaterial = new PhysicsMaterial("Gamble Ball Physics")
            {
                bounciness = 0.68f,
                dynamicFriction = 0.04f,
                staticFriction = 0.04f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
            sphereCollider.sharedMaterial = _runtimePhysicsMaterial;
        }
    }
}
