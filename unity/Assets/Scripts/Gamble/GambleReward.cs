using System;
using UnityEngine;

public enum GambleRewardType
{
    Ammo,
    ExtraBall,
    AbilityCharge,
    Multiplier,
}

[Serializable]
public struct GambleReward
{
    [SerializeField] private GambleRewardType type;
    [SerializeField, Min(0)] private int value;

    public GambleRewardType Type => type;
    public int Value => value;
    public string Label => type switch
    {
        GambleRewardType.Ammo => $"+{value} ammo",
        GambleRewardType.ExtraBall => $"+{value} ball",
        GambleRewardType.AbilityCharge => $"+{value} charge",
        GambleRewardType.Multiplier => $"x{value}",
        _ => value.ToString(),
    };

    public GambleReward(GambleRewardType type, int value)
    {
        this.type = type;
        this.value = Mathf.Max(0, value);
    }
}
