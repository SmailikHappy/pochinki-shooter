using UnityEngine;
using Unity.Netcode;
using Pochinki.Networking.Game;

public class CanonRotator : MonoBehaviour
{
    [Header("Диапазон и скорость")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationRange = 90f;
    [SerializeField] private float rotationSpeed = 60f;

    [Header("Фаза (расстановка пушек)")]
    [SerializeField] private float phaseOffsetDegrees = 0f;

    private Quaternion _baseRotation;

    private void Awake()
    {
        _baseRotation = transform.localRotation;
    }

    private void Update()
    {
        float phaseTime = phaseOffsetDegrees / rotationSpeed;
        NetworkGameBootstrap bootstrap = NetworkGameBootstrap.Instance;
        double clock = bootstrap != null && bootstrap.ControlsGameplayRoster &&
            bootstrap.IsListening && NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ServerTime.Time
                : Time.timeAsDouble;
        float t = (float)((clock + phaseTime) * rotationSpeed);
        float angle = Mathf.PingPong(t, rotationRange);

        transform.localRotation = _baseRotation * Quaternion.AngleAxis(angle, rotationAxis);
    }

    /// <summary>
    /// Расстановка фазы по индексу пушки среди total (0, range/total, 2*range/total, ...).
    /// </summary>
    public void SetPhaseByIndex(int index, int total)
    {
        if (total <= 0) return;
        phaseOffsetDegrees = rotationRange * index / total;
    }

    /// <summary>
    /// Задаёт направление, куда смотрит пушка в середине своего сектора качания
    /// (angle = rotationRange/2). Весь диапазон [0, rotationRange] разворачивается
    /// симметрично вокруг facingDirection. Перезаписывает базовое вращение,
    /// захваченное в Awake — вызывать после спавна, до первого Update.
    /// </summary>
    public void SetFacingDirection(Vector3 facingDirection)
    {
        float angleFromRight = Vector3.SignedAngle(Vector3.right, facingDirection, rotationAxis);
        _baseRotation = Quaternion.AngleAxis(angleFromRight - rotationRange * 0.5f, rotationAxis);
    }
}
