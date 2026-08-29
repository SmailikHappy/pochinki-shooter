using UnityEngine;
using UnityEngine.InputSystem;

public class Canon : MonoBehaviour
{
    public Player owner { get; private set; }

    [SerializeField] private Transform firePoint;
    [SerializeField] private InputActionAsset inputActions;
    private InputAction fireAction;

    [Header("Bullet Init settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float bulletScale = 0.1f;

    public void Init(Player player, Vector3 position, Quaternion rotation)
    {
        owner = player;
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
        bulletComponent.Init(owner, firePoint.right, bulletSpeed, bulletScale);
    }
}