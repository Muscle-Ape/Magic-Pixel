using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 用户控制输入
/// </summary>
public partial class MPGameView
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
        //inputDic.Add(EventTriggerType.BeginDrag, BeginDrag);
        inputDic.Add(EventTriggerType.Drag, Drag);
        //inputDic.Add(EventTriggerType.EndDrag, EndDrag);

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
    private MPGameBlock RayInspection(PointerEventData eventData)
    {
        EventSystem.current.RaycastAll(eventData, m_rayResults);

        foreach (var item in m_rayResults)
        {
            if (item.gameObject.tag.Equals("Block"))
            {
                return item.gameObject.GetComponent<MPGameBlock>();
            }
        }

        return null;
    }

    private bool BlockControl(MPGameBlock block)
    {
        if (block.isDisable)
            return true;

        // 判断是否正确
        bool correct = !(block.isFill ^ m_isFill);

        // 修改方块
        if (block.isFill)
        {
            block.Fill();
        }
        else
        {
            block.Blank();
        }

        // 错误提示
        if (!correct)
        {
            block.Wrong();
        }

        block.Disable();

        return correct;
    }

    /// <summary>
    /// 固定拖拽方向
    /// </summary>
    /// <param name="block"></param>
    private void DragDirControl(MPGameBlock block)
    {
        if (m_fixedDragDir != Vector2.zero)
            return;

        if (m_dragFirstBlock == null)
        {
            m_dragFirstBlock = block;
        }
        else if (m_dragSecondBlock == null)
        {
            m_dragSecondBlock = block;

            if (Math.Abs(m_dragFirstBlock.transform.position.x - m_dragSecondBlock.transform.position.x) > Math.Abs(m_dragFirstBlock.transform.position.y - m_dragSecondBlock.transform.position.y))
            {
                m_fixedDragDir = Vector2.right;
            }
            else
            {
                m_fixedDragDir = Vector2.up;
            }
        }
    }

    /// <summary>
    /// 停止用户继续拖拽
    /// </summary>
    private void CannotContinueDragging()
    {
        m_fixedDragDir = Vector2.zero;
        m_dragFirstBlock = m_dragSecondBlock = null;
        m_canDragContinue = false;
    }

    /// <summary>
    /// 按下
    /// </summary>
    /// <param name="pointer"></param>
    private void PointerDown(PointerEventData pointer)
    {
        MPGameBlock block = RayInspection(pointer);
        if (block != null)
        {
            if (BlockControl(block))
            {
                DragDirControl(block);
                m_canDragContinue = true;
                m_dragFirstBlock = block;

                // 更新最后一次点的位置
                m_pointerLastPosition = pointer.position;
            }
            else
            {
                CannotContinueDragging();
            }
        }
    }

    /// <summary>
    /// 拖拽中
    /// </summary>
    /// <param name="pointer"></param>
    private void Drag(PointerEventData pointer)
    {
        if (!m_canDragContinue)
            return;

        Vector2 currentPosinterPosition = pointer.position;

        // 1、限制拖拽检查的最大距离，需要分成几段进行检查
        Vector2 distance = pointer.position - m_pointerLastPosition;
        if (m_fixedDragDir != Vector2.zero)
        {
            distance *= m_fixedDragDir;
        }
        int count = Mathf.FloorToInt(distance.magnitude / m_detectionInterval);
        Vector2 dir = distance.normalized * m_detectionInterval;

        MPGameBlock block = null;
        // 2、遍历检查
        for (int i = 1; i <= count; i++)
        {
            Vector2 pos = m_pointerLastPosition + dir * (m_fixedDragDir == Vector2.zero ? Vector2.one : m_fixedDragDir) * i;
            pointer.position = pos;

            block = RayInspection(pointer);
            if (block != null)
            {
                if (!BlockControl(block))
                {
                    CannotContinueDragging();
                    return;
                }

                if (block != m_dragFirstBlock)
                {
                    DragDirControl(block);
                }
            }
        }

        // 3、检查最后一个点
        if (m_fixedDragDir == Vector2.right)
        {
            currentPosinterPosition = new Vector2(currentPosinterPosition.x, m_pointerLastPosition.y);
        }
        else if (m_fixedDragDir == Vector2.up)
        {
            currentPosinterPosition = new Vector2(m_pointerLastPosition.x, currentPosinterPosition.y);
        }
        pointer.position = currentPosinterPosition;
        m_pointerLastPosition = currentPosinterPosition;
        block = RayInspection(pointer);
        if (block != null)
        {
            if (!BlockControl(block))
            {
                CannotContinueDragging();
                return;
            }

            if (block != m_dragFirstBlock)
            {
                DragDirControl(block);
            }
        }
    }

    /// <summary>
    /// 抬起
    /// </summary>
    /// <param name="pointer"></param>
    private void PointerUp(PointerEventData pointer)
    {
        CannotContinueDragging();
    }
}
