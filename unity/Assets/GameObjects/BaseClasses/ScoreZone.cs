using UnityEngine;

/// <summary>
/// Тип области, в которую может попасть шарик.
/// R  — область выстрела: пушка стреляет, счётчик уменьшается.
/// X2 — область множителя: счётчик умножается на 2.
/// </summary>
public enum ScoreZoneType
{
    R,
    Multiplier
}

/// <summary>
/// Триггер-зона внизу поля Pachinko (3D). Вешается на BoxCollider с включённым
/// Is Trigger. Область может временно "отключаться" на время, пока летят пули —
/// согласно MVP-документу ("Области становятся не активными в момент выпуска пуль").
/// </summary>
[RequireComponent(typeof(Collider))]
public class ScoreZone : MonoBehaviour
{
    [SerializeField] private ScoreZoneType zoneType;
    [SerializeField] private float multiplierValue = 2f;

    private bool isActive = true;

    public ScoreZoneType ZoneType => zoneType;
    public float MultiplierValue => multiplierValue;
    public bool IsActive => isActive;

    public void SetActive(bool active)
    {
        isActive = active;
    }
}