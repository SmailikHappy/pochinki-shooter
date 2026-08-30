using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum ScoreZoneType { R, Multiplier, Event }

[RequireComponent(typeof(Collider))]
public class ScoreZone : MonoBehaviour
{
    [SerializeField] private ScoreZoneType zoneType;
    [SerializeField] private float multiplierValue = 2f;

    [Header("Временный буст (опционально)")]
    [SerializeField] private Image boostTimerFill;

    private Collider _collider;
    private bool isActive = true;
    private float baseMultiplierValue;
    private Coroutine boostRoutine;

    public ScoreZoneType ZoneType => zoneType;
    public float MultiplierValue => multiplierValue;
    public bool IsActive => isActive;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        baseMultiplierValue = multiplierValue;

        if (boostTimerFill != null)
            boostTimerFill.gameObject.SetActive(false);
    }

    public void SetActive(bool active)
    {
        isActive = active;
        _collider.isTrigger = active;
    }

    /// <summary>
    /// Временно подменяет multiplierValue (например, 2 → 4 на 30 секунд из
    /// кнопки X4), затем возвращает исходное значение. Повторный вызов во
    /// время уже идущего буста перезапускает таймер заново.
    /// </summary>
    public void SetTemporaryMultiplier(float tempValue, float durationSeconds)
    {
        if (boostRoutine != null)
            StopCoroutine(boostRoutine);

        boostRoutine = StartCoroutine(TemporaryMultiplierRoutine(tempValue, durationSeconds));
    }

    private IEnumerator TemporaryMultiplierRoutine(float tempValue, float durationSeconds)
    {
        multiplierValue = tempValue;

        if (boostTimerFill != null)
        {
            boostTimerFill.gameObject.SetActive(true);
            boostTimerFill.fillAmount = 1f;
        }

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.deltaTime;

            if (boostTimerFill != null)
                boostTimerFill.fillAmount = 1f - Mathf.Clamp01(elapsed / durationSeconds);

            yield return null;
        }

        multiplierValue = baseMultiplierValue;

        if (boostTimerFill != null)
            boostTimerFill.gameObject.SetActive(false);

        boostRoutine = null;
    }
}