using HQ.UIManager;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEngine.EventSystems.EventTrigger;

public partial class MPLargeImageGameView
{

    /// <summary>
    /// 数字栏拖拽移动一格需要累计的屏幕距离。
    /// </summary>
    private const float NUMBER_FRAME_DRAG_STEP_DISTANCE = 60f;

    /// <summary>
    /// 注册大图模式专属的数字栏拖拽事件，用于移动完整大图的可视窗口。
    /// </summary>
    protected override void RegisterModeSpecificUI()
    {
        RegisterNumberFrameMove(m_numberVertical, true);
        RegisterNumberFrameMove(m_numberHorizontal, false);
    }

    /// <summary>
    /// 注册数字栏拖拽移动回调。
    /// </summary>
    /// <param name="target">需要监听拖拽的数字栏节点。</param>
    /// <param name="isVertical">是否为左侧竖向数字栏。</param>
    private void RegisterNumberFrameMove(RectTransform target, bool isVertical)
    {
        if (target == null)
            return;

        EventTrigger et = target.GetComponent<EventTrigger>();
        if (et == null)
        {
            et = target.gameObject.AddComponent<EventTrigger>();
        }

        AddNumberFrameMoveEvent(et, EventTriggerType.BeginDrag, data => OnNumberFrameBeginDrag(data as PointerEventData));
        AddNumberFrameMoveEvent(et, EventTriggerType.Drag, data => OnNumberFrameDrag(data as PointerEventData, isVertical));
        AddNumberFrameMoveEvent(et, EventTriggerType.EndDrag, data => OnNumberFrameEndDrag(data as PointerEventData));
        AddNumberFrameMoveEvent(et, EventTriggerType.PointerUp, data => OnNumberFrameEndDrag(data as PointerEventData));
    }

    /// <summary>
    /// 添加数字栏拖拽事件。
    /// </summary>
    /// <param name="eventTrigger">事件触发器。</param>
    /// <param name="eventID">事件类型。</param>
    /// <param name="callback">事件回调。</param>
    private void AddNumberFrameMoveEvent(EventTrigger eventTrigger, EventTriggerType eventID, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        Entry entry = new Entry();
        entry.eventID = eventID;
        entry.callback.AddListener(callback);
        eventTrigger.triggers.Add(entry);
    }

    /// <summary>
    /// 数字栏开始拖拽。
    /// </summary>
    /// <param name="pointerEvent">指针事件数据。</param>
    private void OnNumberFrameBeginDrag(PointerEventData pointerEvent)
    {
        m_numberFrameDragOffset = Vector2.zero;
    }

    /// <summary>
    /// 数字栏拖拽时根据累计距离移动中心区域展示范围。
    /// </summary>
    /// <param name="pointerEvent">指针事件数据。</param>
    /// <param name="isVertical">是否为左侧竖向数字栏。</param>
    private void OnNumberFrameDrag(PointerEventData pointerEvent, bool isVertical)
    {
        if (pointerEvent == null)
            return;

        m_numberFrameDragOffset += pointerEvent.delta;

        if (isVertical)
        {
            MoveByNumberFrameDrag(true, m_numberFrameDragOffset.y);
        }
        else
        {
            MoveByNumberFrameDrag(false, m_numberFrameDragOffset.x);
        }
    }

    /// <summary>
    /// 数字栏结束拖拽。
    /// </summary>
    /// <param name="pointerEvent">指针事件数据。</param>
    private void OnNumberFrameEndDrag(PointerEventData pointerEvent)
    {
        m_numberFrameDragOffset = Vector2.zero;
    }

    /// <summary>
    /// 根据数字栏拖拽累计距离移动中心区域。
    /// </summary>
    /// <param name="isVertical">是否为竖向移动。</param>
    /// <param name="distance">当前方向累计拖拽距离。</param>
    private void MoveByNumberFrameDrag(bool isVertical, float distance)
    {
        while (Mathf.Abs(distance) >= NUMBER_FRAME_DRAG_STEP_DISTANCE)
        {
            int sign = distance > 0 ? 1 : -1;
            Vector2Int dir = isVertical ? new Vector2Int(sign, 0) : new Vector2Int(0, -sign);
            if (!TryMoveContent(dir))
            {
                m_numberFrameDragOffset = Vector2.zero;
                return;
            }

            if (isVertical)
            {
                m_numberFrameDragOffset.y -= sign * NUMBER_FRAME_DRAG_STEP_DISTANCE;
                distance = m_numberFrameDragOffset.y;
            }
            else
            {
                m_numberFrameDragOffset.x -= sign * NUMBER_FRAME_DRAG_STEP_DISTANCE;
                distance = m_numberFrameDragOffset.x;
            }
        }
    }

    /// <summary>
    /// 判断当前展示范围内是否还有未完成的格子。
    /// </summary>
    /// <returns>当前展示范围内存在未完成格子返回true，否则返回false。</returns>
    protected override bool HasHintTarget()
    {
        return GetVisibleHintBlock() != null;
    }

    /// <summary>
    /// 在当前展示范围内自动完成一个格子，并同步触发行列完成检查。
    /// </summary>
    protected override void CompleteHintTarget()
    {
        MPLargeImageGameBlock block = GetVisibleHintBlock();
        if (block == null)
            return;

        if (block.isFill)
        {
            block.Fill();
        }
        else
        {
            block.Blank();
        }

        block.Disable();
        block.PlayHintAnimation();
        Check(block);
    }

    /// <summary>
    /// 获取当前展示范围内提示道具本次要自动完成的格子，优先选择需要填充的未完成格子。
    /// </summary>
    /// <returns>当前展示范围内可自动完成的格子，没有可用格子时返回null。</returns>
    private MPLargeImageGameBlock GetVisibleHintBlock()
    {
        if (m_blockGrid2Array == null)
            return null;

        for (int i = 0; i < FIXED_SIZE; i++)
        {
            for (int j = 0; j < FIXED_SIZE; j++)
            {
                MPLargeImageGameBlock block = m_blockGrid2Array[i][j];
                if (!block.completed && block.isFill)
                {
                    return block;
                }
            }
        }

        for (int i = 0; i < FIXED_SIZE; i++)
        {
            for (int j = 0; j < FIXED_SIZE; j++)
            {
                MPLargeImageGameBlock block = m_blockGrid2Array[i][j];
                if (!block.completed)
                {
                    return block;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 尝试按指定方向移动中心区域展示范围。
    /// </summary>
    /// <param name="dir">移动方向。</param>
    /// <returns>成功移动返回true，已经到达边界返回false。</returns>
    private bool TryMoveContent(Vector2Int dir)
    {
        Vector2Int startPos = m_blockStatueHead + dir;
        Vector2Int endPos = m_blockStatueHead + dir * FIXED_SIZE;
        if (startPos.x < 0 || startPos.y < 0 || endPos.x >= m_size || endPos.y >= m_size)
        {
            return false;
        }

        m_blockStatueHead += dir;
        RefreshContent();
        return true;
    }

    /// <summary>
    /// 根据可视窗口坐标刷新 10×10 格子、数字提示和行列完成状态。
    /// </summary>
    private void RefreshContent()
    {
        // 更新中心区域
        for (int i = 0; i < FIXED_SIZE; i++)
        {
            for (int j = 0; j < FIXED_SIZE; j++)
            {
                Vector2Int pos = m_blockStatueHead + new Vector2Int(i, j);
                BlockStatue blockStatue = m_blockStatues[pos.x][pos.y];
                MPLargeImageGameBlock block = m_blockGrid2Array[i][j];
                int index = pos.x * m_size + pos.y;
                bool isFill = m_blockInfo.Block.Contains(index);
                if (blockStatue == BlockStatue.Empty)
                {
                    block.Refresh(isFill, false, m_isFill);
                    block.Empty(true);
                }
                else if (blockStatue == BlockStatue.Fill)
                {
                    block.Refresh(isFill, true, m_isFill);
                    block.Fill(true);
                }
                else if (blockStatue == BlockStatue.Blank)
                {
                    block.Refresh(isFill, true, m_isFill);
                    block.Blank(true);
                }
            }
        }

        for (int i = 0; i < FIXED_SIZE; i++)
        {
            // 更新左侧数字
            MPLargeImageGameNumberFrameVertical nv = m_numberVerticalList[i];
            List<int> numbers = new List<int>();
            List<int> checkNumbers = new List<int>();
            int count = 0;
            int checkCount = 0;

            for (int j = 0; j < FIXED_SIZE; j++)
            {
                if (m_blockGrid2Array[i][j].isFill)
                {
                    count++;
                }
                else if (count != 0)
                {
                    numbers.Add(count);
                    count = 0;
                }

                if (m_blockGrid2Array[i][j].fillCompleted)
                {
                    checkCount++;
                }
                else if (checkCount != 0)
                {
                    checkNumbers.Add(checkCount);
                    checkCount = 0;
                }
            }

            if (count != 0)
            {
                numbers.Add(count);
            }
            if (checkCount != 0)
            {
                checkNumbers.Add(checkCount);
            }
            if (numbers.Count == 0)
                numbers.Add(0);
            if (checkNumbers.Count == 0)
                checkNumbers.Add(0);

            nv.Refresh(numbers);
            nv.CheckNumber(checkNumbers);

            // 更新上侧数字
            MPLargeImageGameNumberFrameHorizontal nh = m_numberHorizontalList[i];
            List<int> numbers1 = new List<int>();
            List<int> checkNumbers1 = new List<int>();
            int count1 = 0;
            int checkCount1 = 0;

            for (int j = 0; j < FIXED_SIZE; j++)
            {
                if (m_blockGrid2Array[j][i].isFill)
                {
                    count1++;
                }
                else if (count1 != 0)
                {
                    numbers1.Add(count1);
                    count1 = 0;
                }

                if (m_blockGrid2Array[j][i].fillCompleted)
                {
                    checkCount1++;
                }
                else if (checkCount1 != 0)
                {
                    checkNumbers1.Add(checkCount1);
                    checkCount1 = 0;
                }
            }

            if (count1 != 0)
            {
                numbers1.Add(count1);
            }
            if (checkCount1 != 0)
            {
                checkNumbers1.Add(checkCount1);
            }
            if (numbers1.Count == 0)
                numbers1.Add(0);
            if (checkNumbers1.Count == 0)
                checkNumbers1.Add(0);

            nh.Refresh(numbers1);
            nh.CheckNumber(checkNumbers1);

            // 修改数字框的透明度
            bool finish = true;
            bool finish1 = true;
            for (int j = 0; j < m_size; j++)
            {
                if (finish && m_blockStatues[i + m_blockStatueHead.x][j] == BlockStatue.Empty)
                {
                    finish = false;
                }
                if (finish1 && m_blockStatues[j][i + m_blockStatueHead.y] == BlockStatue.Empty)
                {
                    finish1 = false;
                }
            }

            if (finish)
            {
                nv.DOCgFade(0.5f);
                nv.SetCompleted(true);
            }
            else
            {
                nv.DOCgFade(1f);
                nv.SetCompleted(false);
            }

            if (finish1)
            {
                nh.DOCgFade(0.5f);
                nh.SetCompleted(true);
            }
            else
            {
                nh.DOCgFade(1f);
                nh.SetCompleted(false);
            }
        }
    }

    /// <summary>切换大图模式的填充/标记状态。</summary>
    protected override void ToggleInputMode()
    {
        m_isFill = !m_isFill;
    }

    /// <summary>把当前输入模式同步到可视区域的全部格子。</summary>
    protected override void ApplyInputModeToBlocks()
    {
        if (m_blocks == null)
            return;

        for (int i = 0; i < m_blocks.Count; i++)
        {
            m_blocks[i].SetBlankHit(!m_isFill);
        }
    }

    /// <summary>使用当前大图关卡数据重新打开本关。</summary>
    protected override void RestartLevel()
    {
        MPLargeImageGameViewUIMsgData data = new MPLargeImageGameViewUIMsgData()
        {
            blockInfo = m_blockInfo,
            index = m_index,
            refresh = m_refreshAction,
        };

        MPTransitionView.Play(() =>
        {
            DestroyWindow();
            UIManager.Inst.ShowWindow<MPLargeImageGameView>(data, true);
        });
    }

    /// <summary>返回后刷新大图关卡列表。</summary>
    protected override void OnReturnedToLevelList()
    {
        m_refreshAction?.Invoke();
    }
}
