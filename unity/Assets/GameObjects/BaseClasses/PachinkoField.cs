using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Управляет одним полем Pachinko одного игрока:
/// - спавнит шарики в точке спавна;
/// - хранит текущее значение счётчика (базовое значение — 1);
/// - обрабатывает попадание шарика в область R (выстрел) или X2 (умножение);
/// - на время "выстрела" отключает области, чтобы новые попадания не мешали.
///
/// Сама стрельба пушкой/пулями реализуется в отдельном скрипте (Cannon),
/// который подписывается на событие OnFireRequested.
/// </summary>
public class PachinkoField : MonoBehaviour
{
    [Header("Спавн шариков")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float respawnDelay = 0.3f;

    [Header("Области")]
    [SerializeField] private ScoreZone zoneR;
    [SerializeField] private ScoreZone zoneMultiplier;

    [Header("Счётчик")]
    [SerializeField] private int baseCounterValue = 1;

    [Header("События")]
    [Tooltip("Вызывается каждый раз, когда пушка должна произвести один выстрел. " +
             "Cannon.cs подписывается сюда.")]
    public UnityEvent OnFireRequested;

    [Tooltip("Вызывается, когда счётчик изменился — удобно для обновления UI.")]
    public UnityEvent<int> OnCounterChanged;

    private int _counter;
    private bool _isFiring;

    private void Start()
    {
        ResetCounter();
        SpawnBall();
    }

    private void ResetCounter()
    {
        _counter = baseCounterValue;
        OnCounterChanged?.Invoke(_counter);
    }

    private void SpawnBall()
    {
        if (ballPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("PachinkoField: не задан ballPrefab или spawnPoint.", this);
            return;
        }

        GameObject ballObject = Instantiate(
            ballPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        PachinkoBall ball = ballObject.GetComponent<PachinkoBall>();

        if (ball != null)
        {
            ball.Initialize(this);
        }
        else
        {
            Debug.LogWarning(
                "PachinkoField: на ballPrefab нет компонента PachinkoBall.",
                ballObject
            );
        }
    }

    /// <summary>Вызывается шариком (PachinkoBall) при попадании в активную область.</summary>
    public void OnBallScored(ScoreZone zone, PachinkoBall ball)
    {
        switch (zone.ZoneType)
        {
            case ScoreZoneType.R:
                if (!_isFiring)
                {
                    StartCoroutine(FireSequence());
                }
                break;

            case ScoreZoneType.Multiplier:
                _counter = Mathf.RoundToInt(_counter * zone.MultiplierValue);
                OnCounterChanged?.Invoke(_counter);
                break;
        }

        StartCoroutine(RespawnAfterDelay());
    }

    /// <summary>Вызывается шариком, если он потерян (улетел мимо всех областей).</summary>
    public void OnBallLost(PachinkoBall ball)
    {
        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnBall();
    }

    /// <summary>
    /// Пока идёт серия выстрелов — области неактивны (по MVP-документу),
    /// после каждого выстрела счётчик уменьшается до 0, затем сбрасывается к базовому.
    /// </summary>
    private IEnumerator FireSequence()
    {
        _isFiring = true;
        SetZonesActive(false);

        while (_counter > 0)
        {
            OnFireRequested?.Invoke();
            _counter--;
            OnCounterChanged?.Invoke(_counter);
            yield return new WaitForSeconds(0.15f); // темп стрельбы — подстройте под геймплей
        }

        ResetCounter();
        SetZonesActive(true);
        _isFiring = false;
    }

    private void SetZonesActive(bool active)
    {
        if (zoneR != null) zoneR.SetActive(active);
        if (zoneMultiplier != null) zoneMultiplier.SetActive(active);
    }
}
