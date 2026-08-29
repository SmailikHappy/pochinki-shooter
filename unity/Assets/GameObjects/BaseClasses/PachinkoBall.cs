using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PachinkoBall : MonoBehaviour
{
    [SerializeField] private PachinkoField field;

    [Header("Случайный старт")]
    [SerializeField] private float randomSidewaysForce = 1.5f;
    [SerializeField] private float randomDepthForce = 0.5f;

    private Rigidbody _rb;
    private bool _consumed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Initialize(PachinkoField ownerField)
    {
        field = ownerField;
    }

    private void Start()
    {
        ApplyRandomLaunchForce();
    }

    private void ApplyRandomLaunchForce()
    {
        float x = Random.Range(-randomSidewaysForce, randomSidewaysForce);
        float z = Random.Range(-randomDepthForce, randomDepthForce);
        _rb.AddForce(new Vector3(x, 0f, z), ForceMode.Impulse);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;

        ScoreZone zone = other.GetComponent<ScoreZone>();
        if (zone == null) return;
        if (!zone.IsActive) return; // защита от гонки состояний в момент переключения isTrigger

        _consumed = true;
        field?.OnBallScored(zone, this);
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
        field?.OnBallLost(this);
        DestroyBall();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Killzone"))
            ForceRemoveAndRespawn();
    }
}