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
    private void RegisterUI()
    {
        m_modeSwitchFrame.onClick.AddListener(OnModeSwitchClick);

        RegisterMove(m_moveUp, OnMoveUpPointerDown, OnMovePointerUp);
        RegisterMove(m_moveDown, OnMoveDownPointerDown, OnMovePointerUp);
        RegisterMove(m_moveLeft, OnMoveLeftPointerDown, OnMovePointerUp);
        RegisterMove(m_moveRight, OnMoveRightPointerDown, OnMovePointerUp);

        m_backBtn.onClick.AddListener(OnBackClick);
    }

    /// <summary>
    /// 注册移动按钮的回调
    /// </summary>
    /// <param name="target">注册对象</param>
    /// <param name="pointerDown">按下</param>
    /// <param name="pointerUp">抬起</param>
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

    private IEnumerator StartMove(Vector2Int dir)
    {
        // 1、计算是是否还可以移动
        Vector2Int startPos = m_blockStatueHead + dir;
        Vector2Int endPos = m_blockStatueHead + dir * FIXED_SIZE;
        if (startPos.x < 0 || startPos.y < 0 || endPos.x >= m_size || endPos.y >= m_size)
        {
            yield break;
        }

        // 2、进行移动，每一次移动都需要对游戏区域进行更新
        float delayTime = 0.3f;
        while (true)
        {
            m_blockStatueHead += dir;

            RefreshContent();

            // 3、判断是否还可以继续移动
            startPos = m_blockStatueHead + dir;
            endPos = m_blockStatueHead + dir * FIXED_SIZE;
            if (startPos.x < 0 || startPos.y < 0 || endPos.x >= m_size || endPos.y >= m_size)
            {
                yield break;
            }

            // 4、等待继续移动
            yield return new WaitForSeconds(delayTime);
            delayTime = 0.1f;
        }
    }

    /// <summary>
    /// 刷新游戏区域内容
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
        m_modeSwitchTween = (m_modeSwitchBtn.transform as RectTransform).DOAnchorPosX(m_isFill ? 65 : -65, 0.1f).SetEase(Ease.Linear);

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
        DestroyWindow();
    }
}
