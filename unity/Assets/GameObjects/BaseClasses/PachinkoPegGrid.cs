using UnityEngine;

/// <summary>
/// Расставляет пеги (цилиндры-преграды) сеткой в плоскости этого объекта,
/// со сдвигом через ряд — классический паттерн Pachinko, как на референсе.
///
/// Работает в локальных осях X (вправо) / Y (вверх) самого объекта, на котором
/// висит скрипт, так что сетка автоматически подстроится под поворот поля.
///
/// pegPrefab должен уже иметь коллайдер (CapsuleCollider у стандартного
/// Cylinder есть по умолчанию) и Physics Material с отскоком (BallBounce),
/// назначенный заранее на сам префаб.
/// </summary>
public class PachinkoPegGrid : MonoBehaviour
{
    private const string GeneratedContainerName = "GeneratedPegs";

    [Header("Префаб пега")]
    [SerializeField] private GameObject pegPrefab;
    [SerializeField] private Transform pegsParent;

    [Header("Настройки сетки")]
    [SerializeField] private int rows = 4;
    [SerializeField] private int columnsPerRow = 4;
    [SerializeField] private float horizontalSpacing = 1f;
    [SerializeField] private float verticalSpacing = 1f;

    [Tooltip("Сдвигать ли каждый второй ряд в сторону — как на референсе Pachinko.")]
    [SerializeField] private bool staggerRows = true;

    [Tooltip("Насколько сдвигать чётные ряды, в долях horizontalSpacing (0.5 = на полшага).")]
    [Range(0f, 1f)]
    [SerializeField] private float staggerOffset = 0.5f;

    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool clearBeforeSpawn = true;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnGrid();
        }
    }

    [ContextMenu("Spawn Grid")]
    public void SpawnGrid()
    {
        if (pegPrefab == null)
        {
            Debug.LogWarning("PachinkoPegGrid: не задан pegPrefab.", this);
            return;
        }

        Transform host = pegsParent != null ? pegsParent : transform;

        if (HasAuthoredPegs(host))
        {
            Debug.LogWarning(
                "PachinkoPegGrid: authored pegs already exist, so runtime generation was skipped to avoid duplicates.",
                this);
            return;
        }

        Transform existingContainer = host.Find(GeneratedContainerName);
        if (existingContainer != null && !clearBeforeSpawn)
        {
            Debug.LogWarning(
                "PachinkoPegGrid: generated pegs already exist and clearBeforeSpawn is disabled; generation was skipped.",
                this);
            return;
        }

        ClearGrid();

        GameObject generatedContainer = new(GeneratedContainerName);
        generatedContainer.transform.SetParent(host, false);
        Transform parent = generatedContainer.transform;

        // Центрируем сетку относительно позиции этого объекта.
        float gridWidth = (columnsPerRow - 1) * horizontalSpacing;
        float gridHeight = (rows - 1) * verticalSpacing;

        for (int row = 0; row < rows; row++)
        {
            float rowOffsetX = (staggerRows && row % 2 == 1)
                ? staggerOffset * horizontalSpacing
                : 0f;

            for (int col = 0; col < columnsPerRow; col++)
            {
                float localX = -gridWidth / 2f + col * horizontalSpacing + rowOffsetX;
                float localY = gridHeight / 2f - row * verticalSpacing;

                Vector3 worldPos = transform.TransformPoint(new Vector3(localX, localY, 0f));
                Quaternion worldRot = transform.rotation * pegPrefab.transform.rotation;

                Instantiate(pegPrefab, worldPos, worldRot, parent);
            }
        }
    }

    /// <summary>Удаляет ранее заспавненные пеги — удобно вызывать перед повторным SpawnGrid().</summary>
    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        Transform host = pegsParent != null ? pegsParent : transform;
        Transform generatedContainer = host.Find(GeneratedContainerName);
        if (generatedContainer == null)
            return;

        generatedContainer.gameObject.SetActive(false);

        if (Application.isPlaying)
            Destroy(generatedContainer.gameObject);
        else
            DestroyImmediate(generatedContainer.gameObject);
    }

    private static bool HasAuthoredPegs(Transform host)
    {
        for (int i = 0; i < host.childCount; i++)
        {
            Transform child = host.GetChild(i);
            if (child.name == GeneratedContainerName)
                continue;

            if (child.GetComponent<Collider>() != null)
                return true;
        }

        return false;
    }
}
