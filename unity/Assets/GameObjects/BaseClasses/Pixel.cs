using UnityEngine;

[RequireComponent(typeof(PlayerOwnable))]
public class Pixel : MonoBehaviour
{
    [Tooltip("Дочерний объект зоны захвата пули (PixelCaptureZone) — назначить в префабе.")]
    [SerializeField] private Transform captureZoneTransform;

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
    /// Задаёт зоне захвата мировой размер по X/Z, независимый от того, во сколько
    /// раз масштабирован сам пиксель — компенсирует умножение масштаба через
    /// иерархию (Unity умножает localScale ребёнка на localScale родителя).
    /// Вызывать после того, как у Pixel уже выставлен transform.localScale.
    /// </summary>
    public void SetCaptureZoneWorldSize(float worldSizeX, float worldSizeZ)
    {
        if (captureZoneTransform == null)
            return;

        Vector3 parentScale = transform.localScale;
        float childScaleX = parentScale.x > 0.0001f ? worldSizeX / parentScale.x : worldSizeX;
        float childScaleZ = parentScale.z > 0.0001f ? worldSizeZ / parentScale.z : worldSizeZ;

        Vector3 currentChildScale = captureZoneTransform.localScale;
        captureZoneTransform.localScale = new Vector3(childScaleX, currentChildScale.y, childScaleZ);
    }

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