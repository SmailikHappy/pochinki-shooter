using UnityEngine;
using Pochinki.Networking.Game;

[RequireComponent(typeof(PlayerOwnable))]
public class Pixel : MonoBehaviour
{
    [Tooltip("Child transform containing PixelCaptureZone.")]
    [SerializeField] private Transform captureZoneTransform;

    public bool IsMasterPixel { get; private set; }
    public int GridIndex { get; private set; } = -1;
    public int InitialOwnerSlot { get; private set; } = -1;
    public int MasterOwnerSlot { get; private set; } = -1;
    private Player masterOwner;
    private PlayerOwnable ownable;
    private PixelCaptureZone captureZone;

    private void Awake()
    {
        ownable = GetComponent<PlayerOwnable>();

        if (captureZoneTransform == null)
        {
            captureZone = GetComponentInChildren<PixelCaptureZone>(true);
            captureZoneTransform = captureZone != null ? captureZone.transform : null;
        }
        else
        {
            captureZone = captureZoneTransform.GetComponent<PixelCaptureZone>();
        }

        FitCaptureZoneToVisiblePixel();
    }

    public void Init(Player owner, int gridIndex = -1, int initialOwnerSlot = -1)
    {
        GridIndex = gridIndex;
        InitialOwnerSlot = initialOwnerSlot;
        ownable.SetOwner(owner);
    }

    public void SetCapturePhysicsAuthority(bool enabled)
    {
        if (captureZone == null)
            captureZone = GetComponentInChildren<PixelCaptureZone>(true);

        captureZone?.SetPhysicsAuthority(enabled);
    }

    public void ApplyNetworkOwner(Player owner)
    {
        ownable.SetOwner(owner);
    }

    private void FitCaptureZoneToVisiblePixel()
    {
        if (captureZoneTransform == null)
            return;

        Renderer visibleRenderer = GetComponent<Renderer>();
        BoxCollider captureCollider = captureZoneTransform.GetComponent<BoxCollider>();
        if (visibleRenderer == null || captureCollider == null)
            return;

        // Both bounds are expressed in the Pixel's local space. Matching them
        // here keeps the trigger glued to the rendered cube under every parent
        // and grid scale, without mixing local values with world units.
        Bounds visibleBounds = visibleRenderer.localBounds;
        Vector3 colliderSize = captureCollider.size;
        float scaleX = Mathf.Abs(colliderSize.x) > 0.0001f
            ? Mathf.Abs(visibleBounds.size.x / colliderSize.x)
            : 1f;
        float scaleZ = Mathf.Abs(colliderSize.z) > 0.0001f
            ? Mathf.Abs(visibleBounds.size.z / colliderSize.z)
            : 1f;

        Vector3 currentScale = captureZoneTransform.localScale;
        captureZoneTransform.localScale = new Vector3(scaleX, currentScale.y, scaleZ);

        Vector3 currentPosition = captureZoneTransform.localPosition;
        captureZoneTransform.localPosition = new Vector3(
            visibleBounds.center.x,
            currentPosition.y,
            visibleBounds.center.z);
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
