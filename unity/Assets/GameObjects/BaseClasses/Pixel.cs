using UnityEngine;
using Pochinki.Networking.Game;

[RequireComponent(typeof(PlayerOwnable))]
public class Pixel : MonoBehaviour
{
    public bool IsMasterPixel { get; private set; }
    public int GridIndex { get; private set; } = -1;
    public int InitialOwnerSlot { get; private set; } = -1;
    public int MasterOwnerSlot { get; private set; } = -1;
    private Player masterOwner;
    private PlayerOwnable ownable;

    private void Awake()
    {
        ownable = GetComponent<PlayerOwnable>();
    }

    public void Init(Player owner, int gridIndex = -1, int initialOwnerSlot = -1)
    {
        GridIndex = gridIndex;
        InitialOwnerSlot = initialOwnerSlot;
        ownable.SetOwner(owner);
    }

    public void ApplyNetworkOwner(Player owner)
    {
        ownable.SetOwner(owner);
    }

    /// <summary>
    /// Помечает этот пиксель мастер-пикселем конкретного игрока — вызывается
    /// GameSurface сразу после спавна пушки в том же углу. Потеря этого
    /// пикселя (захват другим владельцем) = игрок выбывает.
    /// </summary>
    public void MarkAsMasterPixel(Player owner, int ownerSlot = -1)
    {
        IsMasterPixel = true;
        masterOwner = owner;
        MasterOwnerSlot = ownerSlot;
        ownable.OnOwnerChanged += HandleOwnerChanged;
    }

    private void HandleOwnerChanged(Player newOwner)
    {
        NetworkGameBootstrap bootstrap = NetworkGameBootstrap.Instance;
        if (bootstrap != null && bootstrap.ControlsGameplayRoster)
            return;

        if (newOwner != masterOwner)
        {
            GameHandler.instance?.NotifyMasterPixelCaptured(masterOwner);
        }
    }

    private void OnDestroy()
    {
        if (IsMasterPixel)
            ownable.OnOwnerChanged -= HandleOwnerChanged;
    }
}
