using UnityEngine;
using UnityEngine.UI;

public class TutorialLessonTile : MonoBehaviour
{
    public TutorialAcademyUI academy;
    public TutorialExercise exercise;
    public Text title,summary,status;
    public void Play()=>academy.Launch(exercise);
    public void Refresh()
    {
        title.text=exercise.title.Text;summary.text=exercise.summary.Text;
        status.text=TutorialJourney.Completed(exercise)?TutorialAcademyUI.L("Completed · Replay","Đã học · Chơi lại"):TutorialAcademyUI.L("Play lesson","Vào bài học");
    }
}
