using UnityEngine;
using UnityEngine.UI;

public class TutorialAcademyUI : MonoBehaviour
{
    public static TutorialAcademyUI Current { get; private set; }
    public MainMenu menu;
    public TutorialCourse course;
    public GameObject welcomePanel,academyPanel,chapterGrid;
    public GameObject[] lessonPages;
    public Text welcomeTitle,welcomeBody,newLabel,returningLabel,academyTitle,academyHint,backLabel;
    public TutorialChapterTile[] chapterTiles;
    public TutorialLessonTile[] lessonTiles;
    int chapter=-1;
    public bool IsOpen => welcomePanel!=null&&welcomePanel.activeSelf||academyPanel!=null&&academyPanel.activeSelf;
    public static string L(string english,string vietnamese)=>Localizer.Vietnamese?vietnamese:english;
    void Awake(){Current=this;welcomePanel.SetActive(false);academyPanel.SetActive(false);}
    void Start()
    {
        if(!PlayerPrefs.HasKey(TutorialJourney.WelcomeKey))
        {
            // Persist when displayed, so force-quitting cannot trigger the first-launch dialog again.
            PlayerPrefs.SetInt(TutorialJourney.WelcomeKey,1);PlayerPrefs.Save();welcomePanel.SetActive(true);
        }
        else if(TutorialJourney.OpenOnReturn){TutorialJourney.OpenOnReturn=false;Open();}
    }
    void OnDestroy(){if(Current==this)Current=null;}
    public void NewPlayer()
    {
        welcomePanel.SetActive(false);
        if(course!=null&&course.chapters.Length>0&&course.chapters[0].lessons.Length>0)Launch(course.chapters[0].lessons[0]);
        else Open();
    }
    public void ReturningPlayer(){welcomePanel.SetActive(false);}
    public void Open(){welcomePanel.SetActive(false);academyPanel.SetActive(true);ShowChapter(-1);}
    public void ShowChapter(int index)
    {
        chapter=index;chapterGrid.SetActive(index<0);
        for(int i=0;i<lessonPages.Length;i++)
        {lessonPages[i].SetActive(i==index);if(i==index)lessonPages[i].GetComponent<ScrollRect>().verticalNormalizedPosition=1;}
    }
    public void Back(){if(chapter>=0)ShowChapter(-1);else academyPanel.SetActive(false);}
    public bool HandleEscape()
    {
        if(welcomePanel.activeSelf)return true;
        if(!academyPanel.activeSelf)return false;
        Back();return true;
    }
    public void Launch(TutorialExercise exercise)
    {
        if(exercise==null)return;
        TutorialJourney.Selected=exercise;TutorialJourney.OpenOnReturn=false;
        menu.StartTutorial(exercise);
    }
    void LateUpdate()
    {
        if(!IsOpen)return;
        welcomeTitle.text=L("Your first night in the village?","Đây là đêm đầu tiên bạn đến làng?");
        welcomeBody.text=L("A few short lessons will help you find your feet. Learn by playing, one small mystery at a time. You can revisit every lesson from the menu.",
            "Để tôi dẫn bạn qua vài tình huống nhỏ nhé. Tự thử từng nước đi, rồi bạn sẽ thấy ngôi làng bớt bí ẩn hơn. Bạn luôn có thể mở lại các bài học từ menu.");
        newLabel.text=L("I'm new · Show me around","Tôi mới chơi · Dẫn tôi làm quen");
        returningLabel.text=L("I've played before · Open menu","Tôi đã biết chơi · Vào menu");
        academyTitle.text=chapter<0?L("THE VILLAGE FIELD GUIDE","CẨM NANG ĐÊM ĐẦU"):course.chapters[chapter].title.Text;
        academyHint.text=chapter<0?L("Choose a subject. Each lesson is a small, playable situation.","Chọn điều bạn muốn tìm hiểu. Mỗi bài là một tình huống nhỏ để tự thử."):course.chapters[chapter].summary.Text;
        if(chapter>=0&&course.chapters[chapter].lessons.Length>4)academyHint.text+=L(" Scroll to see every lesson."," Cuộn để xem đủ các bài.");
        backLabel.text=chapter<0?L("Back to menu","Về menu"):L("All subjects","Tất cả chủ đề");
        foreach(var tile in chapterTiles)tile.Refresh();
        foreach(var tile in lessonTiles)if(tile.gameObject.activeInHierarchy)tile.Refresh();
    }
}
