using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GameSurface))]
public class GameSurfaceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GameSurface spawner = (GameSurface)target;

        if (GUILayout.Button("Spawn Grid"))
        {
            SpawnGrid(spawner);
        }

        if (GUILayout.Button("Clear Children"))
        {
            ClearChildren(spawner);
        }
    }

    void SpawnGrid(GameSurface spawner)
    {
        if (spawner.prefab == null) return;

        Undo.SetCurrentGroupName("Spawn Prefab Grid");
        int group = Undo.GetCurrentGroup();

        float totalWidth = (spawner.columns - 1) * spawner.spacingX;
        float totalDepth = (spawner.rows - 1) * spawner.spacingZ;
        Vector3 halfOffset = new Vector3(totalWidth / 2f, 0, totalDepth / 2f);

        for (int x = 0; x < spawner.columns; x++)
        {
            for (int z = 0; z < spawner.rows; z++)
            {
                Vector3 localPos = new Vector3(x * spawner.spacingX, 0, z * spawner.spacingZ) - halfOffset;

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(spawner.prefab, spawner.transform);
                instance.transform.localPosition = localPos;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(instance, "Spawn Prefab");
            }
        }

        Undo.CollapseUndoOperations(group);
    }

    void ClearChildren(GameSurface spawner)
    {
        Undo.SetCurrentGroupName("Clear Grid");
        int group = Undo.GetCurrentGroup();

        for (int i = spawner.transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(spawner.transform.GetChild(i).gameObject);
        }

        Undo.CollapseUndoOperations(group);
    }
}