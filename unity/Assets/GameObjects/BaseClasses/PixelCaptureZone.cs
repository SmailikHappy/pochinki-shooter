using UnityEngine;
using Pochinki.Networking.Game;

[RequireComponent(typeof(Collider))]
public class PixelCaptureZone : MonoBehaviour
{
    [SerializeField] private PlayerOwnable pixelOwnable;
    private Pixel pixel;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (pixelOwnable == null)
            pixelOwnable = GetComponentInParent<PlayerOwnable>();

        pixel = GetComponentInParent<Pixel>();
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkGameBootstrap bootstrap = NetworkGameBootstrap.Instance;
        if (bootstrap != null && bootstrap.ControlsGameplayRoster)
        {
            if (!bootstrap.IsServer || pixel == null)
                return;

            NetworkBullet networkBullet = other.GetComponent<NetworkBullet>();
            if (networkBullet == null)
                return;

            if (NetworkMatchState.Instance != null &&
                NetworkMatchState.Instance.TryCapturePixel(pixel.GridIndex, networkBullet.PlayerSlot))
            {
                other.GetComponent<Bullet>()?.DestroyBullet();
            }

            return;
        }

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
