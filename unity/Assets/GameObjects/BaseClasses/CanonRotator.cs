using UnityEngine;

/// <summary>
/// Качает пушку в диапазоне [0, rotationRange] градусов вокруг rotationAxis.
/// Вешается на тот же GameObject, что и Canon. firePoint (дочерний transform Canon)
/// поворачивается вместе с объектом автоматически, отдельно трогать Canon.cs не нужно.
/// </summary>
public class CanonRotator : MonoBehaviour
{
    [Header("Диапазон и скорость")]
    [Tooltip("Ось качания в локальных координатах.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [Tooltip("Диапазон качания в градусах: 0..rotationRange.")]
    [SerializeField] private float rotationRange = 90f;
    [Tooltip("Скорость вращения, градусов в секунду.")]
    [SerializeField] private float rotationSpeed = 60f;

    [Header("Фаза (расстановка пушек)")]
    [Tooltip("Сдвиг относительно других пушек на поле, в градусах. " +
             "Для 4 пушек с диапазоном 90: 0, 22.5, 45, 67.5.")]
    [SerializeField] private float phaseOffsetDegrees = 0f;

    private Quaternion _baseRotation;

    private void Awake()
    {
        _baseRotation = transform.localRotation;
    }

    private void Update()
    {
        float phaseTime = phaseOffsetDegrees / rotationSpeed;
        float t = (Time.time + phaseTime) * rotationSpeed;
        float angle = Mathf.PingPong(t, rotationRange);

        transform.localRotation = _baseRotation * Quaternion.AngleAxis(angle, rotationAxis);
    }

    /// <summary>
    /// Опционально: авторасстановка фазы по индексу пушки среди total,
    /// вместо ручного ввода phaseOffsetDegrees в инспекторе.
    /// index=0 -> 0, index=1 -> range/4, index=2 -> range/2, index=3 -> range*3/4 (при total=4).
    /// </summary>
    public void SetPhaseByIndex(int index, int total)
    {
        if (total <= 0) return;
        phaseOffsetDegrees = rotationRange * index / total;
    }
}