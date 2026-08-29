using UnityEngine;
using UnityEngine.InputSystem;

public class Canon : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private InputActionAsset inputActions;
    private InputAction fireAction;

    [Header("Bullet Init settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float bulletScale = 0.1f; // Time between shots

    void OnEnable()
    {
        inputActions.FindAction("Fire").Enable();
    }

    void OnDisable()
    {
        inputActions.FindAction("Fire").Disable();
    }

    void Awake()
    {
        fireAction = inputActions.FindAction("Fire");
        fireAction.performed += Shoot;
    }

    private void Shoot(InputAction.CallbackContext context)
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        bulletComponent.Init(firePoint.right, bulletSpeed, bulletScale);
    }
}
