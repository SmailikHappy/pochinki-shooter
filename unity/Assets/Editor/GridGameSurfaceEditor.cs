using System.Collections.Generic;
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
        Debug.Log("Spawning debug grid...");

        List<Player> players = new()
        {
            new GameObject("DebugPlayer1").AddComponent<Player>(),
            new GameObject("DebugPlayer2").AddComponent<Player>(),
            new GameObject("DebugPlayer3").AddComponent<Player>(),
            new GameObject("DebugPlayer4").AddComponent<Player>()
        };
        spawner.SpawnGrid(players);
    }

    void ClearChildren(GameSurface spawner)
    {
        spawner.ClearChildren();
    }
}