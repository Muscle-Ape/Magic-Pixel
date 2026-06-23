using HQ.UIManager;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 创建游戏中的对象
/// </summary>
public partial class MPGameView
{

    /// <summary>
    /// 创建Block网格
    /// </summary>
    private void CreateGrid()
    {
        // 计算Grid单个格子大小
        float singleSize = GRID_SIZE / (float)m_size;
        m_blockGrid.cellSize = Vector2.one * singleSize;

        // 创建网格格子
        m_blocks = new List<MPGameBlock>();
        int index = 0;
        for (int i = 0; i < m_size; i++)
        {
            for (int j = 0; j < m_size; j++)
            {
                MPGameBlock block = Instantiate(m_blockPrefab, m_blockGrid.transform);

                // 是否需要填充
                bool isFill = m_blockInfo.Block.Contains(index);
                block.Init(isFill);

                m_blocks.Add(block);
                index++;
            }
        }
    }

    /// <summary>
    /// 创建顶部的数字提示
    /// </summary>
    private void CreateHorizontalNumber()
    {
        // 计算列分布情况
        Dictionary<int, List<int>> numbers = new Dictionary<int, List<int>>();

        for (int i = 0; i < m_size; i++)
        {
            List<int> number = new List<int>();
            int count = 0;

            for (int j = 0; j < m_size; j++)
            {
                int index = i + j * m_size;
                if (m_blocks[index].isFill)
                {
                    count++;
                }
                else if (count > 0)
                {
                    number.Add(count);
                    count = 0;
                }
            }

            if (count != 0)
                number.Add(count);

            if (number.Count == 0)
                number.Add(0);

            numbers.Add(i, number);
        }

        // 设置字体大小
        float fontSize = GetFontSize();

        for (int i = 0; i < m_size; i++)
        {
            MPGameNumberFrameHorizontal frame = Instantiate(m_numberHorizontalPrefab, m_numberHorizontal);
            frame.Init(numbers[i], fontSize);
        }
    }

    /// <summary>
    /// 创建左侧的数字提示
    /// </summary>
    private void CreateVerticalNumver()
    {
        // 计算行分布情况
        Dictionary<int, List<int>> numbers = new Dictionary<int, List<int>>();

        for (int i = 0; i < m_size; i++)
        {
            List<int> number = new List<int>();
            int count = 0;

            for (int j = 0; j < m_size; j++)
            {
                int index = i * m_size + j;
                if (m_blocks[index].isFill)
                {
                    count++;
                }
                else if (count > 0)
                {
                    number.Add(count);
                    count = 0;
                }
            }

            if (count != 0)
                number.Add(count);

            if (number.Count == 0)
                number.Add(0);

            numbers.Add(i, number);
        }

        // 设置字体大小
        float fontSize = GetFontSize();

        for (int i = 0; i < m_size; i++)
        {
            MPGameNumberFrameVertical frame = Instantiate(m_numberVerticalPrefab, m_numberVertical);
            frame.Init(numbers[i], fontSize);
        }
    }

    /// <summary>
    /// 创建分隔线段
    /// </summary>
    private void CreateLine()
    {
        if (m_size == 5)
            return;

        if (m_size == 10)
        {
            RectTransform h = NewLineImage(true);
            RectTransform v = NewLineImage(false);

            h.anchoredPosition = Vector2.zero;
            v.anchoredPosition = Vector2.zero;
        }
        else if (m_size == 15)
        {
            float unit = GRID_SIZE / 6f;

            for (int i = -1; i < 2; i += 2)
            {
                RectTransform h = NewLineImage(true);
                RectTransform v = NewLineImage(false);

                h.anchoredPosition = new Vector2(unit * i, 0);
                v.anchoredPosition = new Vector2(0, unit * i);
            }
        }
        else if (m_size == 20)
        {
            float unit = GRID_SIZE / 4f;

            for (int i = -1; i < 2; i++)
            {
                RectTransform h = NewLineImage(true);
                RectTransform v = NewLineImage(false);

                h.anchoredPosition = new Vector2(unit * i, 0);
                v.anchoredPosition = new Vector2(0, unit * i);
            }
        }
    }

    /// <summary>
    /// 统一字体大小
    /// </summary>
    private float GetFontSize()
    {
        float fontSize = 0;

        switch (m_size)
        {
            case 5:
                fontSize = 80;
                break;
            case 10:
                fontSize = 40;
                break;
            case 15:
                fontSize = 32;
                break;
            case 20:
                fontSize = 25;
                break;
        }

        return fontSize;
    }

    /// <summary>
    /// 创建新的线段Image
    /// </summary>
    /// <returns></returns>
    private RectTransform NewLineImage(bool isHorizontal)
    {
        // new GameObj
        GameObject obj = new GameObject("line");
        obj.layer = LayerMask.NameToLayer("UI");

        // 设置父对象
        obj.transform.SetParent(m_lineNode);

        // 添加组件
        RectTransform rectTransform = obj.AddComponent<RectTransform>();
        Image img = obj.AddComponent<Image>();
        rectTransform.localScale = Vector3.one;
        rectTransform.localPosition = Vector3.zero;

        // 设置大小和颜色
        Vector2 size = isHorizontal ? new Vector2(4, GRID_SIZE) : new Vector2(GRID_SIZE, 4);
        rectTransform.sizeDelta = size;
        img.color = Color.black;

        return rectTransform;
    }
}
