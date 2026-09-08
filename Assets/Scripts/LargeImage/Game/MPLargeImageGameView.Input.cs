using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 用户控制输入
/// </summary>
public partial class MPLargeImageGameView
{
    /// <summary>
    /// 注册控制输入的节点
    /// </summary>
    protected override void RegisterInput()
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
    private MPLargeImageGameBlock RayInspection(PointerEventData eventData)
    {
        EventSystem.current.RaycastAll(eventData, m_rayResults);

        foreach (var item in m_rayResults)
        {
            if (item.gameObject.tag.Equals("Block"))
            {
                return item.gameObject.GetComponent<MPLargeImageGameBlock>();
            }
        }

        return null;
    }

    private bool BlockControl(MPLargeImageGameBlock block)
    {
        if (block.completed)
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
            MPVibrationManager.Instance.PlayFailure();
        }
        else if (block.isFill)
        {
            MPVibrationManager.Instance.PlayMediumImpact();
        }
        else
        {
            // 叉号使用短促、清晰的刚性反馈，与填充方块的柔和反馈区分。
            MPVibrationManager.Instance.Play(MPVibrationType.RigidImpact);
        }

        block.Disable();

        return correct;
    }

    /// <summary>
    /// 固定拖拽方向
    /// </summary>
    /// <param name="block"></param>
    private void DragDirControl(MPLargeImageGameBlock block)
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

            if (Mathf.Abs(m_dragFirstBlock.transform.position.x - m_dragSecondBlock.transform.position.x) > Mathf.Abs(m_dragFirstBlock.transform.position.y - m_dragSecondBlock.transform.position.y))
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


    private void Check(MPLargeImageGameBlock block, bool allowLineCompleteAnimation = true)
    {
        if (block == null || m_blockGrid2Array == null || m_blockStatues == null)
            return;

        Vector2Int pos = new Vector2Int(block.index / FIXED_SIZE, block.index % FIXED_SIZE);
        Vector2Int statuePos = m_blockStatueHead + pos;
        if (statuePos.x < 0 || statuePos.y < 0 || statuePos.x >= m_size || statuePos.y >= m_size)
            return;

        // 先将本次操作写入完整大图状态，再根据所有目标色块的完成情况自动补叉。
        if (m_blockStatues[statuePos.x][statuePos.y] == BlockStatue.Empty)
        {
            m_blockStatues[statuePos.x][statuePos.y] = block.isFill
                ? BlockStatue.Fill
                : BlockStatue.Blank;
        }
        AutoCompleteLargeImageBlankBlocks(
            playVisibleAnimation: !m_isRestoringProgress,
            updateVisibleBlocks: true);

        MPGameNumberFrameBase nh = m_numberHorizontalList[pos.y];
        MPGameNumberFrameBase nv = m_numberVerticalList[pos.x];
        List<int> horNum = new List<int>();
        List<int> verNum = new List<int>();
        int horCount = 0;
        int verCount = 0;

        for (int i = 0; i < FIXED_SIZE; i++)
        {
            if (!nh.completed)
            {
                if (m_blockGrid2Array[i][pos.y].fillCompleted)
                {
                    horCount++;
                }
                else if (horCount != 0)
                {
                    horNum.Add(horCount);
                    horCount = 0;
                }
            }

            if (!nv.completed)
            {
                if (m_blockGrid2Array[pos.x][i].fillCompleted)
                {
                    verCount++;
                }
                else if (verCount != 0)
                {
                    verNum.Add(verCount);
                    verCount = 0;
                }
            }
        }

        if (horCount != 0)
        {
            horNum.Add(horCount);
        }
        if (verCount != 0)
        {
            verNum.Add(verCount);
        }

        if (!nh.completed)
            nh.CheckNumber(horNum);
        if (!nv.completed)
            nv.CheckNumber(verNum);

        CompleteVisibleLargeImageNumberFrames(
            pos,
            out bool completedColumn,
            out bool completedRow);
        RecalculateCompletedCount();

        if (m_hvCompleted >= m_size * 2)
        {
            StopLineCompleteAnimations();
            if (!m_isRestoringProgress && !m_hasCompleted)
            {
                UpdateData();
                StartCoroutine(PlayCompletedAnimation());
            }
        }
        else if (allowLineCompleteAnimation && !m_isRestoringProgress &&
            (completedColumn || completedRow))
        {
            PlayLineCompleteAnimation(
                block.transform as RectTransform,
                pos.x,
                pos.y,
                completedColumn,
                completedRow);
        }
    }

    /// <summary>
    /// 扫描完整大图；某行或列的目标色块全部完成后，将其余未操作格写为叉号。
    /// </summary>
    private void AutoCompleteLargeImageBlankBlocks(bool playVisibleAnimation, bool updateVisibleBlocks)
    {
        for (int line = 0; line < m_size; line++)
        {
            if (AreAllLargeImageColumnFillBlocksCompleted(line))
            {
                for (int row = 0; row < m_size; row++)
                    CompleteLargeImageBlankBlock(row, line, playVisibleAnimation, updateVisibleBlocks);
            }

            if (AreAllLargeImageRowFillBlocksCompleted(line))
            {
                for (int column = 0; column < m_size; column++)
                    CompleteLargeImageBlankBlock(line, column, playVisibleAnimation, updateVisibleBlocks);
            }
        }
    }

    private bool AreAllLargeImageColumnFillBlocksCompleted(int column)
    {
        for (int row = 0; row < m_size; row++)
        {
            if (IsLargeImageFillBlock(row, column) &&
                m_blockStatues[row][column] != BlockStatue.Fill)
                return false;
        }

        return true;
    }

    private bool AreAllLargeImageRowFillBlocksCompleted(int row)
    {
        for (int column = 0; column < m_size; column++)
        {
            if (IsLargeImageFillBlock(row, column) &&
                m_blockStatues[row][column] != BlockStatue.Fill)
                return false;
        }

        return true;
    }

    private bool IsLargeImageFillBlock(int row, int column)
    {
        int index = row * m_size + column;
        return m_fillBlockIndices != null
            ? m_fillBlockIndices.Contains(index)
            : m_blockInfo.Block.Contains(index);
    }

    private void CompleteLargeImageBlankBlock(
        int row,
        int column,
        bool playVisibleAnimation,
        bool updateVisibleBlock)
    {
        if (m_blockStatues[row][column] != BlockStatue.Empty ||
            IsLargeImageFillBlock(row, column))
            return;

        m_blockStatues[row][column] = BlockStatue.Blank;
        if (!updateVisibleBlock || !TryGetVisibleLargeImageBlock(row, column, out MPLargeImageGameBlock block))
            return;

        block.Refresh(false, false, m_isFill);
        block.Blank(!playVisibleAnimation);
        block.Disable();
    }

    private bool TryGetVisibleLargeImageBlock(
        int row,
        int column,
        out MPLargeImageGameBlock block)
    {
        int visibleRow = row - m_blockStatueHead.x;
        int visibleColumn = column - m_blockStatueHead.y;
        if (visibleRow < 0 || visibleRow >= FIXED_SIZE ||
            visibleColumn < 0 || visibleColumn >= FIXED_SIZE)
        {
            block = null;
            return false;
        }

        block = m_blockGrid2Array[visibleRow][visibleColumn];
        return block != null;
    }

    /// <summary>刷新当前视口中所有已完成行列，并返回本次操作对应的动画方向。</summary>
    private void CompleteVisibleLargeImageNumberFrames(
        Vector2Int origin,
        out bool completedColumn,
        out bool completedRow)
    {
        completedColumn = false;
        completedRow = false;

        for (int line = 0; line < FIXED_SIZE; line++)
        {
            MPGameNumberFrameBase horizontal = m_numberHorizontalList[line];
            int globalColumn = m_blockStatueHead.y + line;
            if (!horizontal.completed && IsLargeImageColumnCompleted(globalColumn))
            {
                horizontal.Completed();
                if (line == origin.y)
                    completedColumn = true;
            }

            MPGameNumberFrameBase vertical = m_numberVerticalList[line];
            int globalRow = m_blockStatueHead.x + line;
            if (!vertical.completed && IsLargeImageRowCompleted(globalRow))
            {
                vertical.Completed();
                if (line == origin.x)
                    completedRow = true;
            }
        }
    }

    private bool IsLargeImageColumnCompleted(int column)
    {
        for (int row = 0; row < m_size; row++)
        {
            if (m_blockStatues[row][column] == BlockStatue.Empty)
                return false;
        }

        return true;
    }

    private bool IsLargeImageRowCompleted(int row)
    {
        for (int column = 0; column < m_size; column++)
        {
            if (m_blockStatues[row][column] == BlockStatue.Empty)
                return false;
        }

        return true;
    }

    #region EventSystem
    /// <summary>
    /// 按下
    /// </summary>
    /// <param name="pointer"></param>
    private void PointerDown(PointerEventData pointer)
    {
        MPLargeImageGameBlock block = RayInspection(pointer);
        if (block != null)
        {
            bool beforeCompleted = block.completed;
            bool correct = BlockControl(block);

            if (!beforeCompleted)
            {
                Check(block, correct);

                // 音效
                MPAudioManager.Instance.PlaySound(MPSound.MPSoundFill, replay: true);
            }

            if (correct)
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
                SubLoves();

                // 错误提示音效
                
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

        MPLargeImageGameBlock block = null;
        // 2、遍历检查
        for (int i = 1; i <= count; i++)
        {
            Vector2 pos = m_pointerLastPosition + dir * (m_fixedDragDir == Vector2.zero ? Vector2.one : m_fixedDragDir) * i;
            pointer.position = pos;

            block = RayInspection(pointer);
            if (block == m_lastBlock)
            {
                continue;
            }

            if (block != null)
            {
                m_lastBlock = block;
                bool beforeCompleted = block.completed;
                bool correct = BlockControl(block);

                if (!beforeCompleted)
                {
                    Check(block, correct);

                    // 音效
                    MPAudioManager.Instance.PlaySound(MPSound.MPSoundFill, replay: true);
                }

                if (!correct)
                {
                    CannotContinueDragging();
                    SubLoves();

                    // 错误提示音效
                    
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
        if (block == m_lastBlock)
        {
            return;
        }

        if (block != null)
        {
            m_lastBlock = block;
            bool beforeCompleted = block.completed;
            bool correct = BlockControl(block);

            if (!beforeCompleted)
            {
                Check(block, correct);

                // 音效
                MPAudioManager.Instance.PlaySound(MPSound.MPSoundFill, replay: true);
            }

            if (!correct)
            {
                CannotContinueDragging();
                SubLoves();

                // 错误提示音效
                
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
    #endregion
}
