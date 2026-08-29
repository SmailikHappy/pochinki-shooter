using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class GambleSlot : MonoBehaviour
{
    [SerializeField] private int slotIndex;
    [SerializeField] private GambleReward reward = new(GambleRewardType.Ammo, 1);

    private GambleSurface _surface;

    public int SlotIndex => slotIndex;
    public GambleReward Reward => reward;

    private void Awake()
    {
        var trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
    }

    public void Configure(GambleSurface surface, int index, GambleReward slotReward)
    {
        _surface = surface;
        slotIndex = index;
        reward = slotReward;
    }

    private void OnTriggerEnter(Collider other)
    {
        var ball = other.GetComponent<Ball>() ?? other.GetComponentInParent<Ball>();

        if (ball == null || !ball.TryResolve())
        {
            return;
        }

        var targetSurface = _surface != null ? _surface : ball.Owner;

        if (targetSurface == null)
        {
            Debug.LogWarning($"[Gamble] Slot {slotIndex + 1} has no GambleSurface.", this);
            Destroy(ball.gameObject);
            return;
        }

        targetSurface.ResolveBall(ball, reward, slotIndex);
    }
}
