using UnityEngine;
using UnityEngine.UI;

public class TutorialChapterTile : MonoBehaviour
{
    public TutorialAcademyUI academy;
    public int chapter;
    public Text title,summary,progress;
    public Image icon;
    public void Open()=>academy.ShowChapter(chapter);
    public void Refresh()
    {
        var data=academy.course.chapters[chapter];title.text=data.title.Text;summary.text=data.summary.Text;icon.sprite=data.icon;
        int completed=0;foreach(var lesson in data.lessons)if(TutorialJourney.Completed(lesson))completed++;
        progress.text=TutorialAcademyUI.L($"{completed}/{data.lessons.Length} lessons completed",$"Đã học {completed}/{data.lessons.Length} bài");
    }
}
