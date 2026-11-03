using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(SkillFxGraphic)),CanEditMultipleObjects]
public class SkillFxGraphicEditor : ImageEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.Space();EditorGUILayout.LabelField("Skill artwork",EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("traderSprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("jesterSprite"));
        using(new EditorGUI.DisabledScope(true))EditorGUILayout.PropertyField(serializedObject.FindProperty("glyph"));
        serializedObject.ApplyModifiedProperties();
    }
}
