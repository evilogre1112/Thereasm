// Documentation captures only. Run in the isolated validation project.
// Scenes and gameplay assets are never saved by this tool.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ReadmeCapture
{
    const string Request = "Thereasm.ReadmeCapture";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int phase;
    static double due;
    static TurnManager turns;
    static DeckManager deck;
    static ReadmeCapture()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Request, false))
            { due = EditorApplication.timeSinceStartup + 1.5; EditorApplication.update += Tick; }
        };
    }
    public static void Run()
    {
        phase = 0;
        SessionState.SetBool(Request, true);
        SessionState.SetBool(Request + "WelcomeExists", PlayerPrefs.HasKey(TutorialJourney.WelcomeKey));
        SessionState.SetInt(Request + "Welcome", PlayerPrefs.GetInt(TutorialJourney.WelcomeKey));
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScenes.unity");
        EditorApplication.isPlaying = true;
    }
    static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    static CardInstance Card(int number) => Object.FindObjectsByType<CardInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Single(c => c.cardData != null && c.cardData.cardNumber == number && !c.transform.IsChildOf(deck.draftPanel.transform));
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < due) return;
        try
        {
            switch (phase)
            {
                case 0:
                    Localizer.SetLanguage(GameLanguage.English, false);
                    TutorialAcademyUI.Current.ReturningPlayer();
                    TutorialAcademyUI.Current.academyPanel.SetActive(false);
                    phase++; due = EditorApplication.timeSinceStartup + .8; return;
                case 1:
                    Capture("menu");
                    TutorialAcademyUI.Current.Open();
                    phase++; due = EditorApplication.timeSinceStartup + .4; return;
                case 2:
                    Capture("academy");
                    SceneManager.LoadScene("MainBoard");
                    phase++; due = EditorApplication.timeSinceStartup + 1.5; return;
                case 3:
                    turns = TurnManager.instance; deck = DeckManager.instance;
                    if (turns == null || deck == null) return;
                    turns.enabled = false; deck.enabled = false;
                    if (turns.curBot != null) turns.curBot.gameObject.SetActive(false);
                    if (turns.villagerBot != null) turns.villagerBot.gameObject.SetActive(false);
                    GameSettings.currentMode = GameMode.PvP;
                    Localizer.SetLanguage(GameLanguage.English, false);
                    var names = Enumerable.Range(1, 16).Select(n => "Villager" + n).Concat(new[] {
                        "Elder17", "Preacher18", "Blind19", "Trader20", "Master21", "Snooper22", "Witch23", "Gambler24" });
                    deck.allCards = names.Select(name => {
                        var data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CardData>("Assets/ScriptableObjects/" + name + ".asset"));
                        data.name = name;
                        if (data.cardSkill != null) data.cardSkill = Object.Instantiate(data.cardSkill);
                        return data;
                    }).ToList();
                    Call(deck, "FinishSetupAndGenerateDeck");
                    phase++; due = EditorApplication.timeSinceStartup + .6; return;
                case 4:
                    turns.allCurs.Clear(); turns.allCurs.Add(Card(1)); turns.allCurs.Add(Card(12));
                    turns.currentTurn = TurnState.Player2Turn; turns.currentVotingPlayer = DefaultOwner.Player2;
                    var seats = new[] { 1, 10, 9, 20, 22, 24, 11 };
                    for (int i = 0; i < seats.Length; i++) Card(seats[i]).GetComponent<CardInteraction>().ExecutePlace(turns.allSeats[i].cardPos);
                    turns.p1Hearts = turns.p2Hearts = 5;
                    Call(turns, "UpdateTurnUI");
                    turns.UpdateCurVisuals();
                    GameStartUI.Current.ShowVillageHand();
                    GameBoardLayout.Active.ResetView();
                    CardDialogue.HideAllBubbles();
                    phase++; due = EditorApplication.timeSinceStartup + .8; return;
                case 5:
                    Capture("gameplay");
                    turns.ToggleNote();
                    phase++; due = EditorApplication.timeSinceStartup + .4; return;
                case 6:
                    Capture("notebook");
                    Finish(0); return;
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    static void Capture(string name)
    {
        foreach (var label in Object.FindObjectsByType<LocalizedLabel>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) label.Refresh();
        foreach (var cursor in Object.FindObjectsByType<CustomCusor>(FindObjectsSortMode.None)) cursor.gameObject.SetActive(false);
        var cameraObject = new GameObject("Documentation capture camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -1000);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
        var target = new RenderTexture(1600, 900, 24); camera.targetTexture = target;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.isActiveAndEnabled).ToArray();
        var modes = canvases.Select(c => c.renderMode).ToArray(); var cameras = canvases.Select(c => c.worldCamera).ToArray();
        var distances = canvases.Select(c => c.planeDistance).ToArray();
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera; canvases[i].planeDistance = 100; }
        Canvas.ForceUpdateCanvases();
        // Discard Editor-cached meshes belonging to hidden legacy labels.
        foreach (var renderer in Object.FindObjectsByType<CanvasRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!renderer.gameObject.activeInHierarchy) renderer.Clear();
        camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ReadmePreview");
        Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
        RenderTexture.active = null;
        for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
        Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
        Canvas.ForceUpdateCanvases();
    }
    static void Finish(int code)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Request, false);
        if (SessionState.GetBool(Request + "WelcomeExists", false)) PlayerPrefs.SetInt(TutorialJourney.WelcomeKey, SessionState.GetInt(Request + "Welcome", 0));
        else PlayerPrefs.DeleteKey(TutorialJourney.WelcomeKey);
        PlayerPrefs.Save(); EditorApplication.Exit(code);
    }
}
