using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Tên Scene Bàn Cờ")]
    public string gameSceneName = "MainBoard";
    public string tutorialSceneName = "TutorialBoard";

    private bool isLoading;

    void Start() => GameStartUI.CreateMenu(this);

    private void StartMatch(GameMode mode)
    {
        if (isLoading) return;
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"Không tìm thấy scene '{gameSceneName}' trong Build Settings.");
            return;
        }
        isLoading = true;
        if (OnlineSession.Instance != null) OnlineSession.Instance.Leave();
        PlayClickSound();
        GameSettings.currentMode = mode;
        GameSettings.currentLevel = GameRules.ClampLevel(GameSettings.currentLevel);
        SceneManager.LoadScene(gameSceneName);
    }

    public void PlayTutorial()
    {
        if(TutorialAcademyUI.Current!=null){TutorialAcademyUI.Current.Open();return;}
        StartTutorial(null);
    }
    public void StartTutorial(TutorialExercise exercise)
    {
        if (isLoading || !Application.CanStreamedLevelBeLoaded(tutorialSceneName)) return;
        TutorialJourney.Selected=exercise;
        isLoading = true;
        if (OnlineSession.Instance != null) OnlineSession.Instance.Leave();
        Time.timeScale = 1;
        PlayClickSound();
        SceneManager.LoadScene(tutorialSceneName);
    }

    public void PlayWithVillBot()
    {
        StartMatch(GameMode.PlayerVsVillBot);
    }

    public void PlayWithCurBot()
    {
        StartMatch(GameMode.PlayerVsCurBot);
    }

    public void QuitGame()
    {
        PlayClickSound();
        Application.Quit();
    }

    private void PlayClickSound()
    {
        if (AudioManager.instance != null && AudioManager.instance.defaultClickSFX != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.defaultClickSFX);
        }
    }
}
