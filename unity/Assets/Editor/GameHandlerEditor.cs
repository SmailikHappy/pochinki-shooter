using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameHandler))]
public class GameHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GameHandler gameHandler = (GameHandler)target;

        if (GUILayout.Button("Start Game"))
        {
            gameHandler.StartGame();
        }
    }
}
