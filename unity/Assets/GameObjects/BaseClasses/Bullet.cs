using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Player Owner { get; private set; }
    public User OwnerUser => Owner != null ? Owner.User : null;

    [SerializeField] private LayerMask wallLayer;

    private Vector3 direction;
    private float speed;
    private Rigidbody _rb;
    private Collider _collider;

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
    }

    public void Init(Player owner, Vector3 direction, float speed, float scale)
    {
        this.Owner = owner;
        this.direction = direction.normalized;
        this.speed = speed;
        transform.localScale = new Vector3(scale, scale, scale);

        _rb.linearVelocity = this.direction * this.speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & wallLayer) == 0)
            return;

        if (collision.contactCount == 0)
            return;

        Vector3 normal = collision.GetContact(0).normal;
        Vector3 reflected = Vector3.Reflect(direction, normal);

        reflected.y = 0f; // прижимаем пулю к горизонтальной плоскости после отскока
        direction = reflected.normalized;

        _rb.linearVelocity = direction * speed;
    }
}