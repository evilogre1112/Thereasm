using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// No SDK types referenced here: importing the official SDK enables the adapter automatically.
[InitializeOnLoad]
public static class PhotonFusionSetup
{
    static PhotonFusionSetup() { EditorApplication.delayCall += Configure; }
    private static Type FindType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        { var type = assembly.GetType(name, false); if (type != null) return type; }
        return null;
    }
    [MenuItem("Tools/Thereasm/Check Photon Fusion Setup")]
    public static void Configure()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var runner = FindType("Fusion.NetworkRunner");
        var callbacks = FindType("Fusion.INetworkRunnerCallbacks");
        var method = callbacks != null ? callbacks.GetMethod("OnReliableDataReceived") : null;
        bool installed = runner != null && method != null && method.GetParameters().Length == 4;
        bool version21 = method != null && method.GetParameters().Length == 4 &&
            method.GetParameters()[3].ParameterType.Name.StartsWith("ReadOnlySpan", StringComparison.Ordinal);
        var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(s =>
            !string.IsNullOrWhiteSpace(s) && s != "THEREASM_FUSION" && s != "THEREASM_FUSION_21").ToList();
        if (installed) symbols.Add("THEREASM_FUSION");
        if (installed && version21) symbols.Add("THEREASM_FUSION_21");
        string updated = string.Join(";", symbols);
        if (PlayerSettings.GetScriptingDefineSymbols(target) != updated) PlayerSettings.SetScriptingDefineSymbols(target, updated);
        if (!installed) Debug.Log("Thereasm: import the official Photon Fusion 2 SDK. The saved PhotonOnlineConfig already contains your Fusion App ID; offline modes remain available.");
        else
        {
            // Fusion requires unsafe code. Set it before the first Play Mode so its
            // deferred Editor hook does not recompile and shut down a connecting runner.
            if (!PlayerSettings.allowUnsafeCode) PlayerSettings.allowUnsafeCode = true;
            if (!PlayerSettings.runInBackground) PlayerSettings.runInBackground = true;
            EnsureSdkSettingsAssets();
            ApplySavedAppSettings();
            Debug.Log("Thereasm: Photon Fusion " + (version21 ? "2.1+" : "2.0") + " adapter enabled. Open MenuScenes > Chơi online, then test with a standalone build and Editor.");
        }
    }

    private static void EnsureSdkSettingsAssets()
    {
        var factory = FindType("Fusion.Editor.FusionGlobalScriptableObjectUtils");
        var ensure = factory != null ? factory.GetMethod("EnsureAssetExists", new[] { typeof(Type) }) : null;
        if (ensure == null) return;
        foreach (string name in new[] { "Fusion.Photon.Realtime.PhotonAppSettings", "Fusion.NetworkProjectConfigAsset" })
        {
            var type = FindType(name);
            if (type != null) ensure.Invoke(null, new object[] { type });
        }
    }

    [MenuItem("Tools/Thereasm/Apply Saved Photon Settings")]
    public static void ApplySavedAppSettings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var config = AssetDatabase.LoadAssetAtPath<PhotonOnlineConfig>("Assets/Networking/PhotonOnlineConfig.asset");
        if (config == null || !Guid.TryParse(config.appIdFusion, out _))
        { Debug.LogError("Thereasm: select a valid Fusion App ID in PhotonOnlineConfig."); return; }
        var candidates = AssetDatabase.FindAssets("t:PhotonAppSettings");
        if (candidates.Length != 1)
        {
            Debug.LogWarning("Thereasm: expected one SDK PhotonAppSettings asset; import Fusion 2 completely before applying settings.");
            return;
        }
        var path = AssetDatabase.GUIDToAssetPath(candidates[0]);
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        var serialized = new SerializedObject(asset);
        var appId = serialized.FindProperty("AppSettings.AppIdFusion");
        var version = serialized.FindProperty("AppSettings.AppVersion");
        var region = serialized.FindProperty("AppSettings.FixedRegion");
        if (appId == null || version == null || region == null)
        { Debug.LogError("Thereasm: unsupported PhotonAppSettings serialization. Keep the official Fusion 2 SDK intact."); return; }
        appId.stringValue = config.appIdFusion.Trim();
        version.stringValue = config.appVersion;
        region.stringValue = (config.region ?? "").Trim().ToLowerInvariant();
        var nameServer = serialized.FindProperty("AppSettings.UseNameServer");
        if (nameServer != null) nameServer.boolValue = true;
        if (serialized.ApplyModifiedPropertiesWithoutUndo())
        { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets(); }
        Debug.Log("Thereasm: saved Fusion App ID, app version and region to " + path);
    }
}
