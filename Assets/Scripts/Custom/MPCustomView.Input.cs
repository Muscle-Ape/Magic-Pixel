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

        // 根据当前模式对方块进行操作
        if (m_isFillMode)
        {
            // 判断是填充还是清空，并进行标记
            if (block.isFill)
            {
                block.Fill(false);
                m_isClear = true;
            }
            else
            {
                block.Fill(true);
                m_isClear = false;
            }
        }
        else
        {
            // 判断是上色还是清空，并进行标记
            if (block.ColorIsSame(m_currentColor))
            {
                block.ClearColor();
                m_isClear = true;
            }
            else
            {
                block.SetColor(m_currentColor);
                m_isClear = false;
            }
        }

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

        // 根据模式进行判断，对并根据是否清空对格子进行填充上色或者清除
        if (m_isFillMode)
        {
            if (m_isClear)
            {
                block.Fill(false);
            }
            else
            {
                block.Fill(true);
            }
        }
        else
        {
            if (m_isClear)
            {
                block.ClearColor();
            }
            else
            {
                block.SetColor(m_currentColor);
            }
        }

        m_currentDragBlocks.Add(block);
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
