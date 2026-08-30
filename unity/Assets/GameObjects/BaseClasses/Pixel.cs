using UnityEngine;

[RequireComponent(typeof(PlayerOwnable))]
public class Pixel : MonoBehaviour
{
    public bool IsMasterPixel { get; private set; }
    private Player masterOwner;
    private PlayerOwnable ownable;

    private void Awake()
    {
        ownable = GetComponent<PlayerOwnable>();
    }

    public void Init(Player owner)
    {
        ownable.SetOwner(owner);
    }

    /// <summary>
    /// Помечает этот пиксель мастер-пикселем конкретного игрока — вызывается
    /// GameSurface сразу после спавна пушки в том же углу. Потеря этого
    /// пикселя (захват другим владельцем) = игрок выбывает.
    /// </summary>
    public void MarkAsMasterPixel(Player owner)
    {
        IsMasterPixel = true;
        masterOwner = owner;
        ownable.OnOwnerChanged += HandleOwnerChanged;
    }

    private void HandleOwnerChanged(Player newOwner)
    {
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