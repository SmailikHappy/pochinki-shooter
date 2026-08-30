using UnityEngine;

/// <summary>
/// Базовый класс способности/карточки, применяемой к конкретному полю Pachinko
/// игрока. Конкретные способности — отдельные ScriptableObject-ассеты
/// (Assets → Create → Pachinko → Abilities → ...), каждая — свой файл,
/// без изменений в PachinkoField/PachinkoCounter/ScoreZone/GameUI.
/// </summary>
public abstract class PachinkoAbility : ScriptableObject
{
    [Tooltip("Название способности для UI/логов.")]
    [SerializeField] private string abilityName = "Ability";
    public string AbilityName => abilityName;

    /// <summary>Применяет эффект способности к конкретному полю игрока.</summary>
    public abstract void Apply(PachinkoField field);
}