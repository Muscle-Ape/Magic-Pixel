using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>教学网格的单指输入。复用主玩法的直线拖拽习惯，快速滑动时补采样，步骤切换后须重新按下。</summary>
public sealed class MPGuideInput : MonoBehaviour, IPointerDownHandler, IDragHandler,
    IPointerUpHandler, IInitializePotentialDragHandler
{
    private RectTransform m_rect;
    private Action<int> m_onCell;
    private int m_rows;
    private int m_pointerId;
    private bool m_pressed;
    private Vector2 m_origin;
    private Vector2 m_last;
    private int m_axis;
    private int m_lastIndex = -1;

    public void Initialize(int rows, Action<int> onCell)
    {
        m_rect = (RectTransform)transform;
        m_rows = rows;
        m_onCell = onCell;
        CancelGesture();
    }

    public void CancelGesture() { m_pressed = false; m_lastIndex = -1; m_axis = 0; }
    public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = false; }

    public void OnPointerDown(PointerEventData data)
    {
        if (m_pressed || data.button != PointerEventData.InputButton.Left || m_onCell == null) return;
        if (!ToLocal(data, out m_origin)) return;
        m_pressed = true;
        m_pointerId = data.pointerId;
        m_last = m_origin;
        m_axis = 0;
        m_lastIndex = -1;
        Visit(m_origin);
    }

    public void OnDrag(PointerEventData data)
    {
        if (!m_pressed || data.pointerId != m_pointerId || !ToLocal(data, out Vector2 local)) return;
        Vector2 delta = local - m_origin;
        float cell = m_rect.rect.width / MPGuideLesson.Size;
        if (m_axis == 0 && delta.magnitude > cell * 0.35f)
            m_axis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? 1 : 2;
        if (m_axis == 1) local.y = m_origin.y;
        if (m_axis == 2) local.x = m_origin.x;
        int samples = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(m_last, local) / (cell * 0.25f)), 1, 100);
        for (int i = 1; i <= samples && m_pressed; i++) Visit(Vector2.Lerp(m_last, local, i / (float)samples));
        m_last = local;
    }

    public void OnPointerUp(PointerEventData data) { if (data.pointerId == m_pointerId) CancelGesture(); }
    private bool ToLocal(PointerEventData data, out Vector2 local) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rect, data.position, data.pressEventCamera, out local);

    private void Visit(Vector2 local)
    {
        Rect rect = m_rect.rect;
        if (!rect.Contains(local)) return;
        int column = Mathf.FloorToInt((local.x - rect.xMin) / rect.width * MPGuideLesson.Size);
        int row = Mathf.FloorToInt((rect.yMax - local.y) / rect.height * m_rows);
        if (column < 0 || column >= MPGuideLesson.Size || row < 0 || row >= m_rows) return;
        int index = row * MPGuideLesson.Size + column;
        if (index == m_lastIndex) return;
        m_lastIndex = index;
        m_onCell?.Invoke(index);
    }

    private void OnDisable() { CancelGesture(); }
    private void OnDestroy() { m_onCell = null; }
}
