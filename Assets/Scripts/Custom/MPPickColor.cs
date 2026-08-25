using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MPPickColor : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>
    /// RectTransform Node
    /// </summary>
    private RectTransform m_rectTransform;

    /// <summary>
    /// 取色UI
    /// </summary>
    private RectTransform m_pickColor;

    /// <summary>
    /// 根据颜色设置Tag位置
    /// </summary>
    private Action<Color> m_setTag;

    private List<RaycastResult> m_rayResults;

    public void Initialization(Action<Color> setTag)
    {
        m_rectTransform = transform as RectTransform;
        m_pickColor = transform.Find("PickColor") as RectTransform;

        m_rayResults = new List<RaycastResult>();

        m_setTag = setTag;
    }

    /// <summary>
    /// 射线检测
    /// 获取当前pointer下的方块
    /// </summary>
    private MPCustomBlock RayInspection(PointerEventData eventData)
    {
        if (EventSystem.current == null)
            return null;

        m_rayResults.Clear();
        EventSystem.current.RaycastAll(eventData, m_rayResults);

        foreach (var item in m_rayResults)
        {
            if (item.gameObject != null && item.gameObject.CompareTag("Block"))
            {
                return item.gameObject.GetComponent<MPCustomBlock>();
            }
        }

        return null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rectTransform, eventData.position, Camera.main, out Vector2 localPoint);
        m_pickColor.anchoredPosition = localPoint;

        m_pickColor.gameObject.SetActive(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rectTransform, eventData.position, Camera.main, out Vector2 localPoint);
        m_pickColor.anchoredPosition = localPoint;

        MPCustomBlock block = RayInspection(eventData);

        if (block == null) return;

        if (block.isColor)
        {
            m_setTag?.Invoke(block.color);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        m_pickColor.gameObject.SetActive(false);
    }
}
