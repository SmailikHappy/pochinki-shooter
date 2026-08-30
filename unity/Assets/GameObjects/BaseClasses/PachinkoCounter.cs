using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PachinkoCounter : MonoBehaviour
{
    [Header("Counter")]
    [SerializeField] private int startValue = 1;
    [SerializeField, Min(1)] private int maxValue = 64;

    [Header("Pachinko Field")]
    [SerializeField] private PachinkoField field;
    [Header("Events")]
    public UnityEvent<int> OnCounterChanged;
    public UnityEvent OnBulletRequested;
    public UnityEvent OnReleaseFinished;
    public UnityEvent OnEventTriggered;

    private int currentValue;
    private bool isReleasing;
    private Canon linkedCanonForFire;

    public int CurrentValue => currentValue;
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

    public void SetFireHandler(Canon canon)
    {
        linkedCanonForFire = canon;
    }

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
    /// Добавляет к текущему значению счётчика произвольное количество (бонус
    /// +100 из UI). Игнорируется во время Release — currentValue в этот момент
    /// отражает оставшиеся выстрелы серии, менять его на лету нельзя.
    /// </summary>
    public void AddValue(int amount)
    {
        if (isReleasing)
            return;

        long newValue = (long)currentValue + amount;
        currentValue = (int)Mathf.Clamp(newValue, 1, maxValue);

        NotifyCounterChanged();
    }

    public void TriggerEvent()
    {
        if (isReleasing)
            return;

        OnEventTriggered?.Invoke();
    }

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

        if (field != null)
            field.SetZonesActive(false);

        while (currentValue > 0)
        {
            yield return WaitForConfirmedFire();

            OnBulletRequested?.Invoke();
            currentValue--;
            NotifyCounterChanged();
        }

        currentValue = 1;
        NotifyCounterChanged();

        if (field != null)
            field.SetZonesActive(true);

        isReleasing = false;

        OnReleaseFinished?.Invoke();
    }

    private IEnumerator WaitForConfirmedFire()
    {
        if (linkedCanonForFire == null)
            yield break;

        while (linkedCanonForFire != null && !linkedCanonForFire.TryFire())
            yield return null;
    }

    private void NotifyCounterChanged()
    {
        OnCounterChanged?.Invoke(currentValue);
    }
}