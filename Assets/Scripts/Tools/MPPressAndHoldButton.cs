using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 轻量长按组件：未达阈值松手按单击处理，达到阈值后按固定间隔重复。
/// 不改变 Button.interactable，数值是否可变由业务回调决定。
/// </summary>
[DisallowMultipleComponent]
public sealed class MPPressAndHoldButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private Func<bool> m_action;
    private Coroutine m_holdCoroutine;
    private float m_holdThreshold;
    private float m_repeatInterval;
    private bool m_pointerDown;
    private bool m_longPressTriggered;

    public void Initialize(Func<bool> action, float holdThreshold, float repeatInterval)
    {
        StopPress();
        m_action = action;
        m_holdThreshold = Mathf.Max(0.01f, holdThreshold);
        m_repeatInterval = Mathf.Max(0.01f, repeatInterval);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || m_action == null)
            return;

        StopPress();
        m_pointerDown = true;
        m_holdCoroutine = StartCoroutine(HoldRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !m_pointerDown)
            return;

        bool invokeClick = !m_longPressTriggered;
        StopPress();
        if (invokeClick)
            m_action?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 手指/鼠标离开按钮即取消，不补发单击。
        StopPress();
    }

    private IEnumerator HoldRoutine()
    {
        float elapsed = 0f;
        while (m_pointerDown && elapsed < m_holdThreshold)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!m_pointerDown)
            yield break;

        m_longPressTriggered = true;
        while (m_pointerDown)
        {
            if (m_action == null || !m_action())
                yield break;

            elapsed = 0f;
            while (m_pointerDown && elapsed < m_repeatInterval)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    public void Release()
    {
        StopPress();
        m_action = null;
    }

    private void StopPress()
    {
        m_pointerDown = false;
        m_longPressTriggered = false;
        if (m_holdCoroutine != null)
        {
            StopCoroutine(m_holdCoroutine);
            m_holdCoroutine = null;
        }
    }

    private void OnDisable()
    {
        StopPress();
    }

    private void OnDestroy()
    {
        Release();
    }
}
