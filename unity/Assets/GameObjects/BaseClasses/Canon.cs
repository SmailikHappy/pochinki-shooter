using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerOwnable))]
public class Canon : MonoBehaviour
{

    [SerializeField] private Transform firePoint;
    [SerializeField] private InputActionAsset inputActions;
    private InputAction fireAction;

    [Header("Bullet Init settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float bulletScale = 0.1f; // Time between shots

    public void Init(Player owner, Vector3 position, Quaternion rotation)
    {
        GetComponent<PlayerOwnable>().SetOwner(owner);
        
        transform.SetPositionAndRotation(position, rotation);
    }

    private void OnEnable()
    {
        inputActions.FindAction("Fire").Enable();
    }

    private void OnDisable()
    {
        inputActions.FindAction("Fire").Disable();
    }

    private void Awake()
    {
        fireAction = inputActions.FindAction("Fire");
        fireAction.performed += Shoot;
    }

    private void Shoot(InputAction.CallbackContext context)
    {
        Fire();
    }

    /// <summary>
    /// Публичный выстрел без InputAction — вызывается PachinkoField
    /// через PachinkoCounter.OnBulletRequested во время серии Release.
    /// </summary>
    public void Fire()
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        Player owner = GetComponent<PlayerOwnable>().GetOwner();
        bulletComponent.Init(owner, firePoint.right, bulletSpeed, bulletScale);
    }
}