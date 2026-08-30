using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PachinkoCounter : MonoBehaviour
{
    [Header("Counter")]
    [SerializeField] private int startValue = 1;
    [SerializeField, Min(1)] private int maxValue = 2147483647;

    [Tooltip("Задержка между каждым выпускаемым выстрелом.")]
    [SerializeField] private float shotInterval = 0.15f;

    [Header("Pachinko Field")]
    [SerializeField] private PachinkoField field;
    [Header("Events")]
    public UnityEvent<int> OnCounterChanged;

    /// <summary>
    /// Вызывается ОДИН раз на каждую пулю.
    /// На это событие потом может подписаться система стрельбы.
    /// </summary>
    public UnityEvent OnBulletRequested;

    private int currentValue;
    private bool isReleasing;

    /// <summary>
    /// Текущее значение счётчика.
    /// Доступно другим скриптам.
    /// </summary>
    public int CurrentValue => currentValue;

    /// <summary>
    /// Сколько пуль было заказано в последней серии.
    /// Например, если счётчик был 8 и попали в R — значение будет 8.
    /// </summary>
    public int LastReleaseAmount { get; private set; }

    public bool IsReleasing => isReleasing;

    private void Awake()
    {
        maxValue = Mathf.Max(1, maxValue);
        currentValue = Mathf.Clamp(startValue, 1, maxValue);

    }

    private void Start()
    {
        NotifyCounterChanged();
    }

    /// <summary>
    /// Попадание шарика в X2.
    /// </summary>
    public void Multiply(int multiplier = 2)
    {
        if (isReleasing)
            return;

        multiplier = Mathf.Max(1, multiplier);
        long multipliedValue = (long)currentValue * multiplier;
        currentValue = (int)Math.Min(multipliedValue, maxValue);

        NotifyCounterChanged();
    }

    /// <summary>
    /// Попадание шарика в R.
    /// </summary>
    public void Release()
    {
        if (isReleasing)
            return;

        StartCoroutine(ReleaseRoutine());
    }

    public UnityEvent OnReleaseFinished;

    private IEnumerator ReleaseRoutine()
    {
        isReleasing = true;
        LastReleaseAmount = currentValue;

        if (field != null)
            field.SetZonesActive(false);

        while (currentValue > 0)
        {
            OnBulletRequested?.Invoke();
            currentValue--;
            NotifyCounterChanged();

            if (currentValue > 0)
                yield return new WaitForSeconds(shotInterval);
        }

        currentValue = 1;
        NotifyCounterChanged();

        if (field != null)
            field.SetZonesActive(true);

        isReleasing = false;

        OnReleaseFinished?.Invoke();
    }

    public UnityEvent OnEventTriggered;

    /// <summary>
    /// Попадание шарика в Event-зону. Не меняет currentValue — только сигнал
    /// для UI показать временную надпись поверх текущего числа.
    /// </summary>
    public void TriggerEvent()
    {
        if (isReleasing)
            return;

        OnEventTriggered?.Invoke();
    }

    private void NotifyCounterChanged()
    {
        OnCounterChanged?.Invoke(currentValue);
    }
}
