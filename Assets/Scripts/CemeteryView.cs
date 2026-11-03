using UnityEngine;
using UnityEngine.UI;

// Scene-authored modal; keep short rows centered and fit a full cemetery inside the frame.
[ExecuteAlways]
public class CemeteryView : MonoBehaviour
{
    public RectTransform content;
    public GridLayoutGroup grid;
    public Text emptyLabel;
    [Range(1, 10)] public int maxColumns = 8;
    public Vector2 maximumCell = new Vector2(152, 232);
    private int previousCount = -1;
    private Vector2 previousSize;
    void OnEnable() { previousCount = -1; Refresh(); }
    void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if (content == null || grid == null) return;
        int count = 0;
        foreach (Transform child in content) if (child.GetComponent<CardInstance>() != null) count++;
        if (emptyLabel != null) emptyLabel.gameObject.SetActive(count == 0);
        if (previousCount == count && previousSize == content.rect.size) return;
        previousCount = count; previousSize = content.rect.size;
        int columns = Mathf.Clamp(count, 1, maxColumns);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        float width = (content.rect.width - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns;
        float height = (content.rect.height - grid.padding.vertical - grid.spacing.y * (rows - 1)) / rows;
        float scale = Mathf.Clamp01(Mathf.Min(width / maximumCell.x, height / maximumCell.y));
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.cellSize = maximumCell * Mathf.Max(.1f, scale);
        LayoutRebuilder.MarkLayoutForRebuild(content);
    }
}
