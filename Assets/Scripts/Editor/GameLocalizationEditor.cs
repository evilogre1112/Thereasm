using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameLocalization))]
public class GameLocalizationEditor : Editor
{
    private string search = "";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultLanguage"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("readableFont"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("readableTmpFont"));
        search = EditorGUILayout.TextField("Search text / key", search);

        var entries = serializedObject.FindProperty("entries");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(entries.arraySize + " bilingual entries", EditorStyles.boldLabel);
        if (GUILayout.Button("Add New Translation", GUILayout.Width(170)))
        {
            int index = entries.arraySize;
            entries.arraySize++;

            // Unity can copy the last element when an array is extended.
            // Clear the contents so this is a genuinely new translation.
            var added = entries.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("key").stringValue = "new.translation." + (index + 1);
            added.FindPropertyRelative("source").stringValue = "";
            added.FindPropertyRelative("english").stringValue = "";
            added.FindPropertyRelative("vietnamese").stringValue = "";
            added.isExpanded = true;

            // Reveal the newly added item even if a search was active.
            search = "";
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            string key = entry.FindPropertyRelative("key").stringValue;
            string source = entry.FindPropertyRelative("source").stringValue;
            string english = entry.FindPropertyRelative("english").stringValue;
            string vietnamese = entry.FindPropertyRelative("vietnamese").stringValue;

            if (search.Length > 0 &&
                !(key + source + english + vietnamese).ToLowerInvariant()
                    .Contains(search.ToLowerInvariant()))
                continue;

            EditorGUILayout.PropertyField(
                entry,
                new GUIContent(string.IsNullOrWhiteSpace(key) ? "Entry " + (i + 1) : key),
                true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
