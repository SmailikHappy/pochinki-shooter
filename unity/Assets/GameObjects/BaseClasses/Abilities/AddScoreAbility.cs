using UnityEngine;

[CreateAssetMenu(menuName = "Pachinko/Abilities/Add Score", fileName = "AddScoreAbility")]
public class AddScoreAbility : PachinkoAbility
{
    [SerializeField, Min(1)] private int amount = 100;

    public override void Apply(PachinkoField field)
    {
        field?.Counter?.AddValue(amount);
    }
}