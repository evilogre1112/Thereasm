using UnityEngine;
using UnityEditor;
using TMPro;

public class FontUpdater : EditorWindow
{
    public TMP_FontAsset newFont;

    [MenuItem("Tools/Update All Fonts")]
    public static void ShowWindow() => GetWindow<FontUpdater>("Font Updater");

    void OnGUI()
    {
        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("New Font SDF", newFont, typeof(TMP_FontAsset), false);

        if (GUILayout.Button("Update All Text in Scene"))
        {
            var texts = Object.FindObjectsByType<TextMeshProUGUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (var t in texts)
            {
                Undo.RecordObject(t, "Update Font");
                t.font = newFont;
                EditorUtility.SetDirty(t);
            }

            Debug.Log($"Đã cập nhật font cho {texts.Length} đối tượng!");
        }
    }
}