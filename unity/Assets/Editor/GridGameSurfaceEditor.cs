using UnityEditor;
using UnityEngine;

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

        if (GUILayout.Button("Clear Grid"))
        {
            ClearChildren(spawner);
        }
    }

    void SpawnGrid(GameSurface spawner)
    {
        spawner.SpawnGrid();
    }

    void ClearChildren(GameSurface spawner)
    {
        spawner.ClearChildren();
    }
}