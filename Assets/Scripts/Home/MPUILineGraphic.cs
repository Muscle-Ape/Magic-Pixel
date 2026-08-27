using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 使用 CanvasRenderer 绘制的 UGUI 折线。
/// 与 Image、Text 等 Graphic 共用 Canvas 绘制队列，层级由兄弟节点顺序决定。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MPUILineGraphic : MaskableGraphic
{
    [SerializeField, Min(1f)]
    private float m_Thickness = 46f;

    [SerializeField, Min(1f)]
    private float m_MiterLimit = 2f;

    private readonly List<Vector2> m_Points = new List<Vector2>();

    public float Thickness
    {
        get => m_Thickness;
        set
        {
            float thickness = Mathf.Max(1f, value);
            if (Mathf.Approximately(m_Thickness, thickness))
                return;

            m_Thickness = thickness;
            SetVerticesDirty();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetPoints(IList<Vector2> points)
    {
        m_Points.Clear();
        if (points != null)
        {
            for (int i = 0; i < points.Count; i++)
                m_Points.Add(points[i]);
        }

        SetVerticesDirty();
    }

    public void ClearPoints()
    {
        if (m_Points.Count == 0)
            return;

        m_Points.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (m_Points.Count < 2 || m_Thickness <= 0f)
            return;

        float halfThickness = m_Thickness * 0.5f;
        float maxMiterLength = halfThickness * Mathf.Max(1f, m_MiterLimit);
        Color32 vertexColor = color;

        for (int i = 0; i < m_Points.Count; i++)
        {
            Vector2 previousDirection = GetPreviousDirection(i);
            Vector2 nextDirection = GetNextDirection(i);
            Vector2 previousNormal = Perpendicular(previousDirection);
            Vector2 nextNormal = Perpendicular(nextDirection);
            Vector2 miter = previousNormal + nextNormal;

            if (miter.sqrMagnitude <= Mathf.Epsilon)
                miter = nextNormal;
            else
                miter.Normalize();

            float denominator = Vector2.Dot(miter, nextNormal);
            float miterLength = Mathf.Abs(denominator) <= 0.001f
                ? halfThickness
                : halfThickness / denominator;
            miterLength = Mathf.Clamp(
                miterLength,
                -maxMiterLength,
                maxMiterLength);

            Vector2 offset = miter * miterLength;
            float verticalUv = m_Points.Count <= 1
                ? 0f
                : i / (float)(m_Points.Count - 1);
            AddVertex(
                vertexHelper,
                m_Points[i] + offset,
                vertexColor,
                new Vector2(0f, verticalUv));
            AddVertex(
                vertexHelper,
                m_Points[i] - offset,
                vertexColor,
                new Vector2(1f, verticalUv));
        }

        for (int i = 0; i < m_Points.Count - 1; i++)
        {
            int vertexIndex = i * 2;
            vertexHelper.AddTriangle(
                vertexIndex,
                vertexIndex + 1,
                vertexIndex + 2);
            vertexHelper.AddTriangle(
                vertexIndex + 2,
                vertexIndex + 1,
                vertexIndex + 3);
        }
    }

    private Vector2 GetPreviousDirection(int pointIndex)
    {
        if (pointIndex <= 0)
            return GetDirection(0, 1);

        Vector2 direction = GetDirection(pointIndex - 1, pointIndex);
        return direction.sqrMagnitude > Mathf.Epsilon
            ? direction
            : GetNextDirection(pointIndex);
    }

    private Vector2 GetNextDirection(int pointIndex)
    {
        if (pointIndex >= m_Points.Count - 1)
            return GetDirection(m_Points.Count - 2, m_Points.Count - 1);

        Vector2 direction = GetDirection(pointIndex, pointIndex + 1);
        return direction.sqrMagnitude > Mathf.Epsilon
            ? direction
            : GetPreviousDirection(pointIndex);
    }

    private Vector2 GetDirection(int startIndex, int endIndex)
    {
        Vector2 direction = m_Points[endIndex] - m_Points[startIndex];
        return direction.sqrMagnitude <= Mathf.Epsilon
            ? Vector2.up
            : direction.normalized;
    }

    private static Vector2 Perpendicular(Vector2 direction)
    {
        return new Vector2(-direction.y, direction.x);
    }

    private static void AddVertex(
        VertexHelper vertexHelper,
        Vector2 position,
        Color32 vertexColor,
        Vector2 uv)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = vertexColor;
        vertex.uv0 = uv;
        vertexHelper.AddVert(vertex);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        m_Thickness = Mathf.Max(1f, m_Thickness);
        m_MiterLimit = Mathf.Max(1f, m_MiterLimit);
        SetVerticesDirty();
    }
#endif
}
