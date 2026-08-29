using UnityEngine;

/// <summary>
/// Шарик Pachinko (3D). Падает под гравитацией, отскакивает от стен/пегов
/// (за счёт Rigidbody + Physics Material с Bounciness), и при попадании
/// в одну из областей внизу (ScoreZone) сообщает об этом полю и уничтожается.
/// Поле (PachinkoField) само заспавнит следующий шарик в точке спавна.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PachinkoBall : MonoBehaviour
{
    [Tooltip("Ссылка на поле, которое управляет спавном и счётчиком. " +
             "Если не задано вручную — найдётся автоматически при спавне.")]
    [SerializeField] private PachinkoField field;

    [Header("Случайный старт")]
    [Tooltip("Максимальная случайная сила вбок при спавне (по X), чтобы шарик каждый " +
             "раз падал по-разному, а не всегда по одной и той же симметричной траектории.")]
    [SerializeField] private float randomSidewaysForce = 1.5f;

    [Tooltip("Небольшой случайный разброс и по глубине (Z) — на случай если у пегов " +
             "есть объём в этом направлении и шарик может уходить вбок и туда тоже.")]
    [SerializeField] private float randomDepthForce = 0.5f;

    private Rigidbody _rb;
    private bool _consumed; // защита от двойного срабатывания (например, если задело сразу два триггера)

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

    /// <summary>
    /// Небольшой случайный импульс вбок сразу при спавне, чтобы одинаковая
    /// сетка пегов не давала каждый раз идентичную (одну и ту же) траекторию.
    /// </summary>
    private void ApplyRandomLaunchForce()
    {
        float x = Random.Range(-randomSidewaysForce, randomSidewaysForce);
        float z = Random.Range(-randomDepthForce, randomDepthForce);

        _rb.AddForce(new Vector3(x, 0f, z), ForceMode.Impulse);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;

        var zone = other.GetComponent<ScoreZone>();
        if (zone == null) return;

        // Пока идёт "выстрел" пулями — области неактивны (см. ScoreZone.IsActive),
        // шарик в этот момент должен просто продолжать падать/отскакивать дальше.
        if (!zone.IsActive) return;

        _consumed = true;
        field?.OnBallScored(zone, this);
        Destroy(gameObject);
    }

    /// <summary>
    /// На случай если шарик вылетел за пределы игрового поля (например, мимо всех
    /// областей через щель) — подчищаем его через триггер-зону "Killzone" снизу поля.
    /// </summary>
    public void ForceRemoveAndRespawn()
    {
        if (_consumed) return;
        _consumed = true;
        field?.OnBallLost(this);
        Destroy(gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Killzone"))
        {
            ForceRemoveAndRespawn();
        }
    }
}
