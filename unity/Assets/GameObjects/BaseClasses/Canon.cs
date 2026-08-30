using UnityEngine;
using UnityEngine.InputSystem;
using Pochinki.Networking.Game;

[RequireComponent(typeof(PlayerOwnable))]
public class Canon : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private InputActionAsset inputActions;

    [Header("Bullet Init settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float bulletScale = 0.1f;

    private PlayerOwnable playerOwnable;
    private InputActionAsset runtimeInputActions;
    private InputAction fireAction;
    private bool initialized;
    private bool inputActive;

    public Player Owner => playerOwnable != null ? playerOwnable.GetOwner() : null;
    public Vector3 FirePosition => firePoint != null ? firePoint.position : transform.position;
    public Quaternion FireRotation => firePoint != null ? firePoint.rotation : transform.rotation;
    public Vector3 FireDirection => firePoint != null ? firePoint.right : transform.right;
    public float BulletSpeed => bulletSpeed;
    public float BulletScale => bulletScale;

    private void Awake()
    {
        playerOwnable = GetComponent<PlayerOwnable>();

        if (inputActions == null)
        {
            Debug.LogWarning("Canon: inputActions is not assigned.", this);
            return;
        }

        // Every canon gets its own runtime action instance. Otherwise disabling
        // a remote canon could disable the shared Fire action for the local one.
        runtimeInputActions = Instantiate(inputActions);
        fireAction = runtimeInputActions.FindAction("Fire", throwIfNotFound: false);

        if (fireAction == null)
        {
            Debug.LogWarning("Canon: Fire action was not found.", this);
        }
    }

    public void Init(Player owner, Vector3 position, Quaternion rotation)
    {
        playerOwnable.SetOwner(owner);
        transform.SetPositionAndRotation(position, rotation);
        initialized = true;
        RefreshInputOwnership();
    }

    private void OnEnable()
    {
        RefreshInputOwnership();
    }

    private void OnDisable()
    {
        SetInputActive(false);
    }

    private void OnDestroy()
    {
        SetInputActive(false);

        if (runtimeInputActions != null)
        {
            Destroy(runtimeInputActions);
        }
    }

    public void RefreshInputOwnership()
    {
        NetworkGameBootstrap bootstrap = NetworkGameBootstrap.Instance;
        bool shouldReceiveInput =
            isActiveAndEnabled &&
            initialized &&
            (bootstrap == null || !bootstrap.ControlsGameplayRoster) &&
            IsOwnedByLocalUser();

        SetInputActive(shouldReceiveInput);
    }

    private bool IsOwnedByLocalUser()
    {
        Player owner = playerOwnable != null ? playerOwnable.GetOwner() : null;
        string ownerId = owner?.user?.UniqueId;
        string localUserId = DiscordHandler.Instance?.LocalUserId;

        return !string.IsNullOrEmpty(ownerId) &&
            string.Equals(ownerId, localUserId, System.StringComparison.Ordinal);
    }

    private void SetInputActive(bool active)
    {
        if (fireAction == null || inputActive == active)
        {
            return;
        }

        inputActive = active;

        if (active)
        {
            fireAction.performed += Shoot;
            fireAction.Enable();
        }
        else
        {
            fireAction.performed -= Shoot;
            fireAction.Disable();
        }
    }

    private void Shoot(InputAction.CallbackContext context)
    {
        Fire();
    }

    /// <summary>
    /// Public fire entry point used by PachinkoCounter during a Release series.
    /// Input ownership is checked at the InputAction boundary, so this method
    /// remains usable by the gameplay mechanic for every player's canon.
    /// </summary>
    public void Fire()
    {
        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("Canon: bulletPrefab or firePoint is not assigned.", this);
            return;
        }

        Player owner = playerOwnable.GetOwner();
        if (owner == null)
        {
            Debug.LogWarning("Canon: cannot fire without an owner.", this);
            return;
        }

        NetworkGameBootstrap bootstrap = NetworkGameBootstrap.Instance;
        if (bootstrap != null && bootstrap.ControlsGameplayRoster)
        {
            if (bootstrap.IsServer)
            {
                bootstrap.TrySpawnNetworkBullet(this);
            }

            return;
        }

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bulletComponent = bullet.GetComponent<Bullet>();

        if (bulletComponent == null)
        {
            Debug.LogWarning("Canon: bulletPrefab has no Bullet component.", bullet);
            Destroy(bullet);
            return;
        }

        bulletComponent.Init(owner, firePoint.right, bulletSpeed, bulletScale);
    }
}
