using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.EventTrigger;

public partial class MPLargeImageGameView
{

    /// <summary>
    /// 切换模式移动的距离
    /// </summary>
    private float m_modeSwitchDistance;

    /// <summary>
    /// 数字栏拖拽移动一格需要累计的屏幕距离。
    /// </summary>
    private const float NUMBER_FRAME_DRAG_STEP_DISTANCE = 60f;

    private void RegisterUI()
    {

        m_modeSwitchDistance = (m_modeSwitchFrame.transform as RectTransform).rect.width / 4;

        m_modeSwitchFrame.onClick.AddListener(OnModeSwitchClick);

        HideMoveButtons();
        RegisterNumberFrameMove(m_numberVertical, true);
        RegisterNumberFrameMove(m_numberHorizontal, false);

        m_backBtn.onClick.AddListener(OnBackClick);

        if (m_hintPropBtn != null)
        {
            m_hintPropBtn.onClick.AddListener(OnHintPropClick);
        }

        if (m_loveRecoverPropBtn != null)
        {
            m_loveRecoverPropBtn.onClick.AddListener(OnLoveRecoverPropClick);
        }

        RefreshPropButtons();

        RefreshUI();

        m_titleText.text = "Big Level " + (m_index + 1).ToString();
    }

    private void RefreshUI()
    {
        m_coinText.text = MPUser.instance.GetCoins().ToString();
        m_diamondText.text = MPUser.instance.GetDiamond().ToString();
    }

    /// <summary>
    /// 刷新提示道具和生命恢复道具的数量显示。
    /// </summary>
    private void RefreshPropButtons()
    {
        if (m_hintPropBtn == null || m_hintPropCountText == null || m_loveRecoverPropBtn == null || m_loveRecoverPropCountText == null)
            return;

        m_hintPropCountText.text = MPUser.instance.GetHintProps().ToString();
        m_loveRecoverPropCountText.text = MPUser.instance.GetLoveRecoverProps().ToString();
    }

    
    /// <summary>
    /// 隐藏旧的方向移动按钮。
    /// </summary>
    private void HideMoveButtons()
    {
        if (m_moveUp != null && m_moveUp.parent != null)
        {
            m_moveUp.parent.gameObject.SetActive(false);
        }
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
        StopMoveCoroutine();
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
        StopMoveCoroutine();
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
    /// 注册移动按钮的回调
    /// </summary>
    /// <param name="target">注册对象</param>
    /// <param name="pointerDown">按下</param>
    /// <param name="pointerUp">抬起</param>
    /// </summary>
    private void RegisterMove(RectTransform target, Action<PointerEventData> pointerDown, Action<PointerEventData> pointerUp)
    {
        target.GetComponent<Image>().alphaHitTestMinimumThreshold = 0.1f;

        EventTrigger et = target.AddComponent<EventTrigger>();

        Entry down = new Entry();
        down.eventID = EventTriggerType.PointerDown;
        down.callback.AddListener(data =>
        {
            pointerDown.Invoke(data as PointerEventData);
        });

        Entry up = new Entry();
        up.eventID = EventTriggerType.PointerUp;
        up.callback.AddListener(data =>
        {
            pointerUp.Invoke(data as PointerEventData);
        });

        et.triggers.Add(down);
        et.triggers.Add(up);
    }

    /// <summary>
    /// 扣除生命值
    /// </summary>
    private void SubLoves()
    {
        m_lovesCount = Mathf.Max(0, m_lovesCount - 1);

        GameObject love = m_loves[m_lovesCount];
        love.transform.DOKill();
        love.transform.localScale = Vector3.one;
        love.SetActive(false);

        SaveProgressCache();
        RefreshPropButtons();
    }

    /// <summary>
    /// 恢复生命值
    /// </summary>
    private void AddLoves()
    {
        if (m_lovesCount == m_loves.Count)
            return;

        GameObject love = m_loves[m_lovesCount];
        love.transform.DOKill();
        love.transform.localScale = Vector3.zero;
        love.SetActive(true);
        love.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetLink(love);

        m_lovesCount++;

        SaveProgressCache();
        RefreshPropButtons();
    }


    /// <summary>
    /// 判断当前展示范围内是否还有未完成的格子。
    /// </summary>
    /// <returns>当前展示范围内存在未完成格子返回true，否则返回false。</returns>
    private bool HasVisibleUncompletedBlock()
    {
        return GetVisibleHintBlock() != null;
    }

    /// <summary>
    /// 在当前展示范围内自动完成一个格子，并同步触发行列完成检查。
    /// </summary>
    private void AutoCompleteOneVisibleBlock()
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
    /// 点击提示道具按钮，在当前展示范围内自动完成一个格子。
    /// </summary>
    private void OnHintPropClick()
    {
        if (!HasVisibleUncompletedBlock())
        {
            RefreshPropButtons();
            return;
        }

        if (!MPUser.instance.UseHintProp())
        {
            RefreshPropButtons();
            return;
        }

        AutoCompleteOneVisibleBlock();
        SaveProgressCache();
        RefreshPropButtons();
    }

    /// <summary>
    /// 点击生命恢复道具按钮，消耗一个恢复道具并恢复一颗生命。
    /// </summary>
    private void OnLoveRecoverPropClick()
    {
        if (m_lovesCount >= m_loves.Count)
        {
            RefreshPropButtons();
            return;
        }

        if (!MPUser.instance.UseLoveRecoverProp())
        {
            RefreshPropButtons();
            return;
        }

        AddLoves();
    }

    private IEnumerator StartMove(Vector2Int dir)
    {
        // 1、计算是是否还可以移动
        if (!TryMoveContent(dir))
        {
            yield break;
        }

        // 2、进行移动，每一次移动都需要对游戏区域进行更新
        float delayTime = 0.3f;
        while (true)
        {
            yield return new WaitForSeconds(delayTime);
            delayTime = 0.1f;

            if (!TryMoveContent(dir))
            {
                yield break;
            }

            // 3、判断是否还可以继续移动

            // 4、等待继续移动
        }
    }

    /// <summary>
    /// 刷新游戏区域内容
    /// </summary>
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

    private void OnMoveUpPointerDown(PointerEventData pointerEvent)
    {
        if (m_moveCoroutine == null)
        {
            m_moveCoroutine = StartCoroutine(StartMove(new Vector2Int(-1, 0)));
        }
    }

    private void OnMoveDownPointerDown(PointerEventData pointerEvent)
    {
        if (m_moveCoroutine == null)
        {
            m_moveCoroutine = StartCoroutine(StartMove(new Vector2Int(1, 0)));
        }
    }

    private void OnMoveLeftPointerDown(PointerEventData pointerEvent)
    {
        if (m_moveCoroutine == null)
        {
            m_moveCoroutine = StartCoroutine(StartMove(new Vector2Int(0, -1)));
        }
    }

    private void OnMoveRightPointerDown(PointerEventData pointerEvent)
    {
        if (m_moveCoroutine == null)
        {
            m_moveCoroutine = StartCoroutine(StartMove(new Vector2Int(0, 1)));
        }
    }

    private void OnMovePointerUp(PointerEventData pointerEvent)
    {
        StopMoveCoroutine();
    }

    /// <summary>
    /// 停止中心区域连续移动协程。
    /// </summary>
    private void StopMoveCoroutine()
    {
        if (m_moveCoroutine != null)
        {
            StopCoroutine(m_moveCoroutine);
            m_moveCoroutine = null;
        }
    }

    /// <summary>
    /// 切换模式
    /// </summary>
    private void OnModeSwitchClick()
    {
        m_isFill = !m_isFill;

        m_modeSwitchTween?.Kill();
        m_modeSwitchTween = (m_modeSwitchBtn.transform as RectTransform).DOAnchorPosX(m_isFill ? m_modeSwitchDistance : -m_modeSwitchDistance, 0.1f).SetEase(Ease.Linear);

        m_modeSwitchFill.gameObject.SetActive(m_isFill);
        m_modeSwitchBlank.gameObject.SetActive(!m_isFill);

        for (int i = 0; i < m_blocks.Count; i++)
        {
            m_blocks[i].SetBlankHit(!m_isFill);
        }
    }

    /// <summary>
    /// 返回按钮回调
    /// </summary>
    private void OnBackClick()
    {
        SaveProgressCache();
        DestroyWindow();

        m_refreshAction?.Invoke();
    }
}
