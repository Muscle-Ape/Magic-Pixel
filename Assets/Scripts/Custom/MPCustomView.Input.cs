using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class MPCustomView
{
    /// <summary>
    /// 注册控制输入的节点
    /// </summary>
    private void RegisterInput()
    {
        EventTrigger trigger = m_input.GetOrAddComponent<EventTrigger>();
        // 存放要注册的事件
        Dictionary<EventTriggerType, Action<PointerEventData>> inputDic = new Dictionary<EventTriggerType, Action<PointerEventData>>();
        inputDic.Add(EventTriggerType.PointerDown, PointerDown);
        inputDic.Add(EventTriggerType.PointerUp, PointerUp);
        inputDic.Add(EventTriggerType.Drag, Drag);

        // 开始注册
        foreach (var key in inputDic.Keys)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry();

            entry.eventID = key;

            Action<PointerEventData> handler = inputDic[key];
            entry.callback.AddListener((data) =>
            {
                handler?.Invoke(data as PointerEventData);
            });

            trigger.triggers.Add(entry);
        }
    }

    /// <summary>
    /// 射线检测
    /// 获取当前pointer下的方块
    /// </summary>
    private MPCustomBlock RayInspection(PointerEventData eventData)
    {
        EventSystem.current.RaycastAll(eventData, m_rayResults);

        foreach (var item in m_rayResults)
        {
            if (item.gameObject.tag.Equals("Block"))
            {
                return item.gameObject.GetComponent<MPCustomBlock>();
            }
        }

        return null;
    }

    /// <summary>
    /// 按下
    /// </summary>
    /// <param name="pointer"></param>
    private void PointerDown(PointerEventData pointer)
    {
        MPCustomBlock block = RayInspection(pointer);

        if (block == null)
            return;

        BeginNewPublishDraft();

        // 根据当前模式对方块进行操作
        if (m_isFillMode)
        {
            m_isClear = block.isFill;
        }
        else
        {
            m_isClear = block.ColorIsSame(m_currentColor);
        }

        ApplyCurrentEdit(block);
        m_currentDragBlocks.Add(block);
    }

    /// <summary>
    /// 拖拽中
    /// </summary>
    /// <param name="pointer"></param>
    private void Drag(PointerEventData pointer)
    {
        MPCustomBlock block = RayInspection(pointer);

        if (block == null || m_currentDragBlocks.Contains(block))
            return;

        ApplyCurrentEdit(block);
        m_currentDragBlocks.Add(block);
    }

    /// <summary>
    /// 应用当前编辑状态。仅在格子状态真正变化时触发震动。
    /// </summary>
    private void ApplyCurrentEdit(MPCustomBlock block)
    {
        if (m_isFillMode)
        {
            bool targetFill = !m_isClear;
            if (block.isFill == targetFill)
                return;

            block.Fill(targetFill);
            MPVibrationManager.Instance.PlayMediumImpact();
            return;
        }

        if (m_isClear)
        {
            if (!block.isColor)
                return;

            block.ClearColor();
        }
        else
        {
            if (block.ColorIsSame(m_currentColor))
                return;

            block.SetColor(m_currentColor);
        }

        MPVibrationManager.Instance.PlaySelection();
    }

    /// <summary>
    /// 抬起
    /// </summary>
    /// <param name="pointer"></param>
    private void PointerUp(PointerEventData pointer)
    {
        m_currentDragBlocks.Clear();
        m_isClear = false;
    }
}
