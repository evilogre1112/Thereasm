using System;
using UnityEngine;

[Serializable]
public class TutorialStep
{
    public TutorialStage stage;
    public string title;
    [TextArea(2, 6)] public string instruction;
    [TextArea(2, 5)] public string purpose;
    public string success;
}

[CreateAssetMenu(fileName = "FirstMatchLesson", menuName = "Thereasm/Tutorial Lesson")]
public class TutorialLesson : ScriptableObject
{
    public TutorialStep[] steps;
    [Tooltip("Fixed seven-card exercise: 1, 2, 3, 9, 10, 11, 12. Direct CardData references.")]
    public CardData[] cards;
    [TextArea(3, 8)] public string inspectDescription = "DÂN LÀNG · SỐ 10\n\nĐây là một lá thường, không có kỹ năng chủ động.\n\nĐặt từ nhóm của phe đang chơi: 2 tim. Nhóm đối phương hoặc trung lập: 5 tim.\n\nChuột phải lần nữa để đóng.";
    public string practiceScene = "MainBoard";
    public string menuScene = "MenuScenes";
    [Header("Heart workshop — editable bilingual source text")]
    [TextArea(6, 16)] public string heartOverview;
    [TextArea(8, 20)] public string movementCosts;
    [TextArea(8, 20)] public string swapCosts;
    [TextArea(6, 16)] public string skillCosts;
}
