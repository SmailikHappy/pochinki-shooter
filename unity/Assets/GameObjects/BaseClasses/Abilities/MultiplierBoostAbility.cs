using UnityEngine;

[CreateAssetMenu(menuName = "Pachinko/Abilities/Multiplier Boost", fileName = "MultiplierBoostAbility")]
public class MultiplierBoostAbility : PachinkoAbility
{
    [SerializeField] private float boostedMultiplierValue = 4f;
    [SerializeField, Min(0f)] private float durationSeconds = 30f;

    public override void Apply(PachinkoField field)
    {
        field?.MultiplierZone?.SetTemporaryMultiplier(boostedMultiplierValue, durationSeconds);
    }
}