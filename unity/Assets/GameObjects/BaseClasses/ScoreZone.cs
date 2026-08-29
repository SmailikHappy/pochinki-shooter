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

    [Tooltip("Множитель для типа Multiplier (обычно 2). Для типа R не используется.")]
    [SerializeField] private float multiplierValue = 2f;

    [Tooltip("Визуально показывать неактивное состояние (например, затемнять материал).")]
    [SerializeField] private Renderer visual;

    public ScoreZoneType ZoneType => zoneType;
    public float MultiplierValue => multiplierValue;
    public bool IsActive { get; private set; } = true;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        if (visual != null && visual.material.HasProperty("_Color"))
        {
            var c = visual.material.color;
            c.a = active ? 1f : 0.4f;
            visual.material.color = c;
        }
    }
}
