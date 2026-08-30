using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerOwnable))]
public class Canon : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private InputActionAsset inputActions;

    [Header("Bullet Init settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float bulletScale = 0.1f;

    [Header("Fire Rate")]
    [Tooltip("Минимальный интервал между выстрелами, в секундах. 0 — без ограничения.")]
    [SerializeField, Min(0f)] private float fireCooldown = 0.1f;

    private PlayerOwnable playerOwnable;
    private InputActionAsset runtimeInputActions;
    private InputAction fireAction;
    private bool initialized;
    private bool inputActive;
    private float lastFireTime = float.NegativeInfinity;

    private void Awake()
    {
        playerOwnable = GetComponent<PlayerOwnable>();

        if (inputActions == null)
        {
            Debug.LogWarning("Canon: inputActions is not assigned.", this);
            return;
        }

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
        bool shouldReceiveInput =
            isActiveAndEnabled &&
            initialized &&
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
        TryFire();
    }

    /// <summary>
    /// Пытается выстрелить прямо сейчас. Возвращает false без побочных эффектов,
    /// если пушка ещё на кулдауне, нет владельца или не настроена — PachinkoCounter
    /// во время Release опрашивает этот метод каждый кадр, пока не получит true.
    /// </summary>
    public bool TryFire()
    {
        if (fireCooldown > 0f && Time.time - lastFireTime < fireCooldown)
            return false;

        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("Canon: bulletPrefab or firePoint is not assigned.", this);
            return false;
        }

        Player owner = playerOwnable.GetOwner();
        if (owner == null)
        {
            Debug.LogWarning("Canon: cannot fire without an owner.", this);
            return false;
        }

        lastFireTime = Time.time;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bulletComponent = bullet.GetComponent<Bullet>();

        if (bulletComponent == null)
        {
            Debug.LogWarning("Canon: bulletPrefab has no Bullet component.", bullet);
            Destroy(bullet);
            return false;
        }

        bulletComponent.Init(owner, firePoint.right, bulletSpeed, bulletScale);
        return true;
    }

    /// <summary>Совместимость с прежними вызовами — результат не важен вызывающему.</summary>
    public void Fire()
    {
        TryFire();
    }
}