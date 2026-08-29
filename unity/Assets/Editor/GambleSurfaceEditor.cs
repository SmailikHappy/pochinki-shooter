using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(GambleSurface))]
public sealed class GambleSurfaceEditor : Editor
{
    private const string SandboxPath = "Assets/Scenes/GambleSandbox.unity";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10f);

        var surface = (GambleSurface)target;

        if (GUILayout.Button("Generate Board", GUILayout.Height(32f)))
        {
            Undo.RegisterFullObjectHierarchyUndo(surface.gameObject, "Generate Gamble Board");
            surface.RebuildBoard();
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        }

        if (GUILayout.Button("Clear Board", GUILayout.Height(26f)))
        {
            Undo.RegisterFullObjectHierarchyUndo(surface.gameObject, "Clear Gamble Board");
            surface.ClearBoard();
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Drop Ball (Play Mode)", GUILayout.Height(30f)))
            {
                surface.DropBall();
            }
        }

        EditorGUILayout.HelpBox(
            "Generate Board creates an editor-visible preview. Play Mode rebuilds a clean runtime board automatically.",
            MessageType.Info
        );
    }

    [MenuItem("Pochinki/Gamble Board/Open Sandbox")]
    private static void OpenSandbox()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(SandboxPath);
        }
    }
}
