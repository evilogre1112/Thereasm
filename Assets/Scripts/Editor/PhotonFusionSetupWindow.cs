using System.IO;
using UnityEditor;
using UnityEngine;

public class PhotonFusionSetupWindow : EditorWindow
{
    const string DownloadPage = "https://doc.photonengine.com/fusion/v2/getting-started/sdk-download";
    [MenuItem("Tools/Thereasm/Photon Fusion Setup")]
    public static void ShowSetup() { GetWindow<PhotonFusionSetupWindow>("Photon Fusion 2"); }
    void OnGUI()
    {
        EditorGUILayout.LabelField("Thereasm · Photon Fusion 2", EditorStyles.boldLabel);
        var config = AssetDatabase.LoadAssetAtPath<PhotonOnlineConfig>("Assets/Networking/PhotonOnlineConfig.asset");
        EditorGUILayout.ObjectField("Saved config", config, typeof(PhotonOnlineConfig), false);
        EditorGUILayout.HelpBox("Import the official Fusion 2 SDK, then apply the saved App ID and region. Downloading the SDK requires a Photon sign-in. Use Fusion 2.0.13 with this Unity 6.0 project.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("1 · Open official SDK download")) Application.OpenURL(DownloadPage);
            if (GUILayout.Button("2 · Import downloaded Fusion 2 .unitypackage"))
            {
                var initial = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");
                string path = EditorUtility.OpenFilePanel("Choose the official Photon Fusion 2 SDK", initial, "unitypackage");
                if (!string.IsNullOrEmpty(path)) AssetDatabase.ImportPackage(path, true);
            }
            if (GUILayout.Button("3 · Detect SDK and apply saved settings")) PhotonFusionSetup.Configure();
            if (config != null && GUILayout.Button("Select editable PhotonOnlineConfig")) Selection.activeObject = config;
        }
    }
}
