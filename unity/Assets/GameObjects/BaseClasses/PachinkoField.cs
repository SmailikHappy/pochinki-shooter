using System.Collections;
using UnityEngine;

public class PachinkoField : MonoBehaviour
{
    [Header("Спавн шариков")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float respawnDelay = 0.3f;
    [Header("Зоны")]
    [SerializeField] private ScoreZone zoneR;
    [SerializeField] private ScoreZone zoneMultiplier;
    [Header("Счётчик")]
    [SerializeField] private PachinkoCounter counter;

    private void Start()
    {
        SpawnBall();
    }

    private void SpawnBall()
    {
        if (ballPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning(
                "PachinkoField: не задан ballPrefab или spawnPoint.",
                this
            );
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
                "На Ball Prefab нет PachinkoBall.",
                ballObject
            );
        }
    }
    public void SetZonesActive(bool active)
    {
        if (zoneR != null)
            zoneR.SetActive(active);

        if (zoneMultiplier != null)
            zoneMultiplier.SetActive(active);
    }
    public void OnBallScored(ScoreZone zone, PachinkoBall ball)
    {
        if (counter == null)
        {
            Debug.LogError(
                "PachinkoField: не назначен PachinkoCounter!",
                this
            );

            StartCoroutine(RespawnAfterDelay());
            return;
        }

        switch (zone.ZoneType)
        {
            case ScoreZoneType.R:
                counter.Release();
                break;

            case ScoreZoneType.Multiplier:
                counter.Multiply(
                    Mathf.RoundToInt(zone.MultiplierValue)
                );
                break;
        }

        StartCoroutine(RespawnAfterDelay());
    }

    public void OnBallLost(PachinkoBall ball)
    {
        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnBall();
    }
}