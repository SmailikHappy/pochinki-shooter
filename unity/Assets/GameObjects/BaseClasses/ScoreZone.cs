using UnityEngine;

public enum ScoreZoneType { R, Multiplier }

[RequireComponent(typeof(Collider))]
public class ScoreZone : MonoBehaviour
{
    [SerializeField] private ScoreZoneType zoneType;
    [SerializeField] private float multiplierValue = 2f;

    private Collider _collider;
    private bool isActive = true;

    public ScoreZoneType ZoneType => zoneType;
    public float MultiplierValue => multiplierValue;
    public bool IsActive => isActive;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    public void SetActive(bool active)
    {
        isActive = active;

        // Активна — Trigger: шарик засчитывается и уничтожается при касании, без физики.
        // Заморожена — солид-коллайдер: шарик физически отскакивает, счётчик не трогается.
        _collider.isTrigger = active;
    }
}