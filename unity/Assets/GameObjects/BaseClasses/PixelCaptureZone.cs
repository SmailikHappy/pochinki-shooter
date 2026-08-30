using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PixelCaptureZone : MonoBehaviour
{
    [SerializeField] private PlayerOwnable pixelOwnable;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (pixelOwnable == null)
            pixelOwnable = GetComponentInParent<PlayerOwnable>();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerOwnable bulletOwnable = other.GetComponent<PlayerOwnable>();
        if (bulletOwnable == null)
            return;

        Player shooter = bulletOwnable.GetOwner();
        if (shooter == null)
            return;

        if (pixelOwnable == null)
            return;

        Player currentOwner = pixelOwnable.GetOwner();
        if (currentOwner == shooter)
            return; // своя клетка — пуля проходит сквозь, не убивается и не перекрашивает

        pixelOwnable.SetOwner(shooter);
        Destroy(other.gameObject);
    }
}