using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(GameStartUI))]
public class GameStartUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var view = (GameStartUI)target;
        EditorGUILayout.HelpBox("UI objects are saved in the scene. Edit RectTransform, Image and Text directly. Apply Theme only when you want to copy the theme's artwork/fonts onto those objects.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Background") && view.BackgroundImage != null)
                Selection.activeGameObject = view.BackgroundImage.gameObject;
            if (GUILayout.Button("Select Theme") && view.Theme != null) Selection.activeObject = view.Theme;
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Apply Theme To Saved UI"))
            {
                var components = view.GetComponentsInChildren<Component>(true);
                Undo.RecordObjects(components, "Apply Thereasm UI Theme");
                view.ApplyTheme();
                foreach (var component in components) if (component != null) EditorUtility.SetDirty(component);
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            EditorGUILayout.LabelField("Edit Mode preview", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Normal")) Preview(view, null);
            if (GUILayout.Button("Draft")) Preview(view, view.DraftPanel);
            if (GUILayout.Button("Rules")) Preview(view, view.RulesPanel);
            if (GUILayout.Button("Result")) Preview(view, view.ResultPanel);
            if (view.OnlinePanel != null && GUILayout.Button("Online")) Preview(view, view.OnlinePanel);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }

    private static void Preview(GameStartUI view, GameObject active)
    {
        foreach (var panel in new[] { view.DraftPanel, view.RulesPanel, view.ResultPanel, view.OnlinePanel })
        {
            if (panel == null) continue;
            Undo.RecordObject(panel, "Preview Thereasm UI");
            panel.SetActive(panel == active);
            EditorUtility.SetDirty(panel);
        }
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        SceneView.RepaintAll();
    }
}
