using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Clips custom card shaders that do not support RectMask2D, preserving their materials.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public sealed class BoardArtworkClip : BaseMeshEffect
{
    private struct ClipVertex
    {
        public UIVertex ui;
        public Vector2 point;
    }

    private RectTransform viewport;
    private bool cachedMembership;
    private Matrix4x4 cachedArtWorld;
    private Matrix4x4 cachedViewportWorld;
    private Rect cachedViewportRect;
    private bool initialized;

    private readonly List<UIVertex> source = new List<UIVertex>(12);
    private readonly List<UIVertex> result = new List<UIVertex>(24);
    private readonly List<ClipVertex> polygon = new List<ClipVertex>(12);
    private readonly List<ClipVertex> scratch = new List<ClipVertex>(12);

    private bool ShouldClip
    {
        get
        {
            if (viewport == null || !transform.IsChildOf(viewport)) return false;
            var inspected = CardInteraction.currentlyInspectedCard;
            // Inspection has its own foreground Canvas and may extend beyond
            // the board. Keep custom artwork complete just like its UI labels.
            return inspected == null || !transform.IsChildOf(inspected.transform);
        }
    }

    public void SetViewport(RectTransform newViewport)
    {
        if (viewport == newViewport) return;
        viewport = newViewport;
        initialized = false;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        initialized = false;
    }

    private void LateUpdate()
    {
        if (graphic == null || viewport == null) return;
        bool membership = ShouldClip;
        Matrix4x4 artWorld = transform.localToWorldMatrix;
        Matrix4x4 viewportWorld = viewport.localToWorldMatrix;
        Rect bounds = viewport.rect;
        if (!initialized || membership != cachedMembership ||
            artWorld != cachedArtWorld || viewportWorld != cachedViewportWorld ||
            bounds != cachedViewportRect)
        {
            initialized = true;
            cachedMembership = membership;
            cachedArtWorld = artWorld;
            cachedViewportWorld = viewportWorld;
            cachedViewportRect = bounds;
            graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || !ShouldClip ||
            vh.currentVertCount == 0) return;

        Matrix4x4 toViewport = viewport.worldToLocalMatrix * transform.localToWorldMatrix;
        Rect clip = viewport.rect;
        source.Clear();
        result.Clear();
        vh.GetUIVertexStream(source);
        for (int tri = 0; tri + 2 < source.Count; tri += 3)
        {
            polygon.Clear();
            for (int i = 0; i < 3; i++)
            {
                UIVertex ui = source[tri + i];
                Vector3 p = toViewport.MultiplyPoint3x4(ui.position);
                polygon.Add(new ClipVertex { ui = ui, point = new Vector2(p.x, p.y) });
            }

            // Sutherland-Hodgman polygon clipping, preserving UV/color when
            // a card partially crosses any of the four viewport edges.
            for (int edge = 0; edge < 4 && polygon.Count > 0; edge++)
            {
                scratch.Clear();
                ClipVertex before = polygon[polygon.Count - 1];
                bool beforeInside = Inside(before.point, edge, clip);
                for (int j = 0; j < polygon.Count; j++)
                {
                    ClipVertex current = polygon[j];
                    bool currentInside = Inside(current.point, edge, clip);
                    if (currentInside != beforeInside)
                        scratch.Add(Intersect(before, current, edge, clip));
                    if (currentInside)
                        scratch.Add(current);
                    before = current;
                    beforeInside = currentInside;
                }
                polygon.Clear();
                polygon.AddRange(scratch);
            }
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                result.Add(polygon[0].ui);
                result.Add(polygon[i].ui);
                result.Add(polygon[i + 1].ui);
            }
        }
        vh.Clear();
        if (result.Count > 0) vh.AddUIVertexTriangleStream(result);
    }

    private static bool Inside(Vector2 p, int edge, Rect rect)
    {
        switch (edge)
        {
            case 0: return p.x >= rect.xMin;
            case 1: return p.x <= rect.xMax;
            case 2: return p.y >= rect.yMin;
            default: return p.y <= rect.yMax;
        }
    }

    private static ClipVertex Intersect(ClipVertex a, ClipVertex b, int edge, Rect rect)
    {
        bool horizontal = edge < 2;
        float boundary = edge == 0 ? rect.xMin : edge == 1 ? rect.xMax :
                         edge == 2 ? rect.yMin : rect.yMax;
        float from = horizontal ? a.point.x : a.point.y;
        float to = horizontal ? b.point.x : b.point.y;
        float delta = to - from;
        float t = Mathf.Abs(delta) > 0.000001f ? Mathf.Clamp01((boundary - from) / delta) : 0f;

        UIVertex v = a.ui;
        v.position = Vector3.LerpUnclamped(a.ui.position, b.ui.position, t);
        v.normal = Vector3.LerpUnclamped(a.ui.normal, b.ui.normal, t);
        v.tangent = Vector4.LerpUnclamped(a.ui.tangent, b.ui.tangent, t);
        v.color = (Color32)Color.LerpUnclamped(a.ui.color, b.ui.color, t);
        v.uv0 = Vector4.LerpUnclamped(a.ui.uv0, b.ui.uv0, t);
        v.uv1 = Vector4.LerpUnclamped(a.ui.uv1, b.ui.uv1, t);
        v.uv2 = Vector4.LerpUnclamped(a.ui.uv2, b.ui.uv2, t);
        v.uv3 = Vector4.LerpUnclamped(a.ui.uv3, b.ui.uv3, t);
        return new ClipVertex { ui = v, point = Vector2.LerpUnclamped(a.point, b.point, t) };
    }
}
