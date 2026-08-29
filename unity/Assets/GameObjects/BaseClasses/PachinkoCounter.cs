using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PachinkoCounter : MonoBehaviour
{
    [Header("Counter")]
    [SerializeField] private int startValue = 1;

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
        currentValue = Mathf.Max(1, startValue);

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

        currentValue *= multiplier;

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

    private IEnumerator ReleaseRoutine()
    {
        isReleasing = true;

        LastReleaseAmount = currentValue;

        // Отключаем R и X2
        if (field != null)
            field.SetZonesActive(false);

        while (currentValue > 0)
        {
            // Запрос одной пули
            OnBulletRequested?.Invoke();

            currentValue--;
            NotifyCounterChanged();

            if (currentValue > 0)
                yield return new WaitForSeconds(shotInterval);
        }

        // После 0 возвращаем счётчик к 1
        currentValue = 1;
        NotifyCounterChanged();

        // Снова включаем R и X2
        if (field != null)
            field.SetZonesActive(true);

        isReleasing = false;
    }


    private void NotifyCounterChanged()
    {
        OnCounterChanged?.Invoke(currentValue);
    }
}