using UnityEngine;

public class GameSurface : MonoBehaviour
{
    public GameObject prefab;
    [SerializeField] private int rows = 5;
    [SerializeField] private int columns = 5;
    [SerializeField] private float spacingX = 2f;
    [SerializeField] private float spacingZ = 2f;

    public bool SpawnGrid()
    {
        if (prefab == null)
        {
            Debug.LogWarning("GameSurface: prefab is not assigned.", this);
            return false;
        }

        ClearChildren();

        float totalWidth = (columns - 1) * spacingX;
        float totalDepth = (rows - 1) * spacingZ;
        Vector3 halfOffset = new Vector3(totalWidth / 2f, 0, totalDepth / 2f);

        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                Vector3 localPos = new Vector3(x * spacingX, 0, z * spacingZ) - halfOffset;
                GameObject instance = Instantiate(prefab, transform);
                instance.transform.localPosition = localPos;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
            }
        }

        return true;
    }

    public void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}