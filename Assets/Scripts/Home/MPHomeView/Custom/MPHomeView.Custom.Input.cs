using System;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class MPHomeView
{
    private void RegisterCustomInput()
    {
        UnregisterCustomInput();
        if (m_customInput == null)
            return;

        m_customInputEventTrigger = m_customInput.GetComponent<EventTrigger>();
        if (m_customInputEventTrigger == null)
            m_customInputEventTrigger = m_customInput.gameObject.AddComponent<EventTrigger>();
        if (m_customInputEventTrigger.triggers == null)
            m_customInputEventTrigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

        AddCustomInputEntry(EventTriggerType.PointerDown, OnCustomPointerDown);
        AddCustomInputEntry(EventTriggerType.PointerUp, OnCustomPointerUp);
        AddCustomInputEntry(EventTriggerType.Drag, OnCustomPointerDrag);
    }

    private void AddCustomInputEntry(
        EventTriggerType eventType,
        Action<PointerEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(data => callback?.Invoke(data as PointerEventData));
        m_customInputEventTrigger.triggers.Add(entry);
        m_customInputEventEntries.Add(entry);
    }

    private void UnregisterCustomInput()
    {
        if (m_customInputEventTrigger != null && m_customInputEventTrigger.triggers != null)
        {
            for (int i = 0; i < m_customInputEventEntries.Count; i++)
                m_customInputEventTrigger.triggers.Remove(m_customInputEventEntries[i]);
        }

        m_customInputEventEntries.Clear();
        m_customInputEventTrigger = null;
        m_customDragBlocks.Clear();
        m_customIsClear = false;
    }

    private MPCustomBlock GetCustomBlockUnderPointer(PointerEventData eventData)
    {
        if (eventData == null || EventSystem.current == null)
            return null;

        m_customRayResults.Clear();
        EventSystem.current.RaycastAll(eventData, m_customRayResults);
        for (int i = 0; i < m_customRayResults.Count; i++)
        {
            GameObject target = m_customRayResults[i].gameObject;
            if (target != null && target.CompareTag("Block"))
                return target.GetComponent<MPCustomBlock>();
        }

        return null;
    }

    private void OnCustomPointerDown(PointerEventData eventData)
    {
        if (!m_customInitialized
            || m_selectedTabIndex != CUSTOM_TAB_INDEX
            || m_customPaletteOpen)
        {
            return;
        }

        MPCustomBlock block = GetCustomBlockUnderPointer(eventData);
        if (block == null)
            return;

        BeginNewCustomPublishDraft();
        m_customDragBlocks.Clear();
        if (m_customIsFillMode)
        {
            m_customIsClear = block.isFill;
            block.Fill(!m_customIsClear);
        }
        else
        {
            m_customIsClear = block.ColorIsSame(m_customCurrentColor);
            if (m_customIsClear)
                block.ClearColor();
            else
                block.SetColor(m_customCurrentColor);
        }

        m_customDragBlocks.Add(block);
    }

    private void OnCustomPointerDrag(PointerEventData eventData)
    {
        if (!m_customInitialized
            || m_selectedTabIndex != CUSTOM_TAB_INDEX
            || m_customPaletteOpen)
        {
            return;
        }

        MPCustomBlock block = GetCustomBlockUnderPointer(eventData);
        if (block == null || m_customDragBlocks.Contains(block))
            return;

        if (m_customIsFillMode)
        {
            block.Fill(!m_customIsClear);
        }
        else if (m_customIsClear)
        {
            block.ClearColor();
        }
        else
        {
            block.SetColor(m_customCurrentColor);
        }

        m_customDragBlocks.Add(block);
    }

    private void OnCustomPointerUp(PointerEventData _)
    {
        m_customDragBlocks.Clear();
        m_customIsClear = false;
    }
}
