using System;
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
        if (block.completed)
            return true;

        // 判断是否正确
        bool correct = !(block.isFill ^ m_isFillMode);

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


    private void Check(MPGameBlock block, bool allowLineCompleteAnimation = true)
    {
        if (block == null || m_blockGrid2Array == null)
            return;

        // 1、转成V2
        Vector2Int pos = new Vector2Int(block.index / m_size, block.index % m_size);

        // 2、当本次操作所在的行或列已经填完全部目标色块时，自动补齐剩余叉号。
        // 恢复缓存时直接显示最终状态，避免重新进入关卡时重复播放叉号动画。
        AutoCompleteBlankBlocks();

        // 3、得到对应的行列Number
        MPGameNumberFrameBase nh = m_numberHorizontalList[pos.y];
        MPGameNumberFrameBase nv = m_numberVerticalList[pos.x];

        // 4、计算对应行列的填充情况
        List<int> horNum = new List<int>();
        List<int> verNum = new List<int>();
        int horCount = 0;
        int verCount = 0;

        for (int i = 0; i < m_size; i++)
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
        {
            nh.CheckNumber(horNum);
        }
        if (!nv.completed)
        {
            nv.CheckNumber(verNum);
        }

        // 5、自动补叉可能同时完成其他交叉行列，需要统一收敛数字栏状态。
        CompleteFinishedNumberFrames(pos, out bool completedColumn, out bool completedRow);

        // 6、判断是否全部完成
        bool allCompleted = m_hvCompleted >= m_size * 2;
        if (allCompleted)
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
    /// 检查所有行列；目标色块全部完成后，为剩余空白格自动补叉。
    /// 单次操作只进行固定次数的网格扫描，不依赖 Update，也不会产生集合分配。
    /// </summary>
    private void AutoCompleteBlankBlocks()
    {
        for (int line = 0; line < m_size; line++)
        {
            if (AreAllColumnFillBlocksCompleted(line))
            {
                for (int row = 0; row < m_size; row++)
                {
                    CompleteBlankBlock(m_blockGrid2Array[row][line]);
                }
            }

            if (AreAllRowFillBlocksCompleted(line))
            {
                for (int column = 0; column < m_size; column++)
                {
                    CompleteBlankBlock(m_blockGrid2Array[line][column]);
                }
            }
        }
    }

    /// <summary>
    /// 标记所有格子均已完成的数字栏，同时覆盖自动补叉带来的交叉行列完成。
    /// </summary>
    private void CompleteFinishedNumberFrames(
        Vector2Int origin,
        out bool completedColumn,
        out bool completedRow)
    {
        completedColumn = false;
        completedRow = false;

        for (int line = 0; line < m_size; line++)
        {
            MPGameNumberFrameBase horizontal = m_numberHorizontalList[line];
            if (!horizontal.completed && AreAllColumnBlocksCompleted(line))
            {
                horizontal.Completed();
                m_hvCompleted++;
                if (line == origin.y)
                    completedColumn = true;
            }

            MPGameNumberFrameBase vertical = m_numberVerticalList[line];
            if (!vertical.completed && AreAllRowBlocksCompleted(line))
            {
                vertical.Completed();
                m_hvCompleted++;
                if (line == origin.x)
                    completedRow = true;
            }
        }
    }

    /// <summary>判断顶部数字提示对应列中的所有目标色块是否均已完成。</summary>
    private bool AreAllColumnFillBlocksCompleted(int column)
    {
        for (int row = 0; row < m_size; row++)
        {
            MPGameBlock lineBlock = m_blockGrid2Array[row][column];
            if (lineBlock == null || (lineBlock.isFill && !lineBlock.completed))
                return false;
        }

        return true;
    }

    /// <summary>判断左侧数字提示对应行中的所有目标色块是否均已完成。</summary>
    private bool AreAllRowFillBlocksCompleted(int row)
    {
        for (int column = 0; column < m_size; column++)
        {
            MPGameBlock lineBlock = m_blockGrid2Array[row][column];
            if (lineBlock == null || (lineBlock.isFill && !lineBlock.completed))
                return false;
        }

        return true;
    }

    private bool AreAllColumnBlocksCompleted(int column)
    {
        for (int row = 0; row < m_size; row++)
        {
            if (m_blockGrid2Array[row][column] == null ||
                !m_blockGrid2Array[row][column].completed)
                return false;
        }

        return true;
    }

    private bool AreAllRowBlocksCompleted(int row)
    {
        for (int column = 0; column < m_size; column++)
        {
            if (m_blockGrid2Array[row][column] == null ||
                !m_blockGrid2Array[row][column].completed)
                return false;
        }

        return true;
    }

    /// <summary>自动补齐一个尚未操作的空白格，不播放音效、震动或错误反馈。</summary>
    private void CompleteBlankBlock(MPGameBlock block)
    {
        if (block == null || block.completed || block.isFill)
            return;

        block.Blank(!m_isRestoringProgress);
        block.Disable();
    }

    #region EventSystem
    /// <summary>
    /// 按下
    /// </summary>
    /// <param name="pointer"></param>
    private void PointerDown(PointerEventData pointer)
    {
        MPGameBlock block = RayInspection(pointer);
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
                MPAudioManager.Instance.PlaySound(MPSound.MPSoundWrong);
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
                    MPAudioManager.Instance.PlaySound(MPSound.MPSoundWrong);
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
                MPAudioManager.Instance.PlaySound(MPSound.MPSoundWrong);
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
