using HQ.UIManager;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    protected override void CreateGrid()
    {
        // 计算Grid单个格子大小
        float singleSize = GRID_SIZE / (float)m_size;
        m_blockGrid.cellSize = Vector2.one * singleSize;

        // 创建网格格子
        m_blocks = new List<MPGameBlock>();
        m_blockGrid2Array = Enumerable.Range(0, m_size).Select(i => new MPGameBlock[m_size]).ToArray();
        int index = 0;
        for (int i = 0; i < m_size; i++)
        {
            for (int j = 0; j < m_size; j++)
            {
                GameObject blockObject = Instantiate(m_blockPrefab, m_blockGrid.transform);
                MPGameBlock block = blockObject.GetComponent<MPGameBlock>();
                if (block == null)
                {
                    block = blockObject.AddComponent<MPGameBlock>();
                }

                // 是否需要填充
                bool isFill = m_blockInfo.Block.Contains(index);
                block.Init(isFill, index);
                block.SetFrameSprite(GetGridCornerSprite(i, j));

                m_blocks.Add(block);
                m_blockGrid2Array[i][j] = block;
                index++;
            }
        }
    }

    /// <summary>
    /// 创建顶部的数字提示
    /// </summary>
    protected override void CreateHorizontalNumber()
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
        Vector2 fontSize = GetFontSize();

        m_numberHorizontalList = new List<MPGameNumberFrameHorizontal>();

        for (int i = 0; i < m_size; i++)
        {
            GameObject frame = Instantiate(m_numberHorizontalPrefab, m_numberHorizontal);
            MPGameNumberFrameHorizontal sprite = frame.AddComponent<MPGameNumberFrameHorizontal>();
            sprite.Init(numbers[i], fontSize);
            sprite.SetFrameSprite(GetHorizontalNumberFrameSprite(i));

            m_numberHorizontalList.Add(sprite);
        }
    }

    /// <summary>
    /// 创建左侧的数字提示
    /// </summary>
    protected override void CreateVerticalNumber()
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
        Vector2 fontSize = GetFontSize();

        m_numberVerticalList = new List<MPGameNumberFrameVertical>();

        for (int i = 0; i < m_size; i++)
        {
            GameObject frame = Instantiate(m_numberVerticalPrefab, m_numberVertical);
            MPGameNumberFrameVertical sprite = frame.AddComponent<MPGameNumberFrameVertical>();
            sprite.Init(numbers[i], fontSize);
            sprite.SetFrameSprite(GetVerticalNumberFrameSprite(i));

            m_numberVerticalList.Add(sprite);
        }
    }

    /// <summary>
    /// 获取像素网格四角对应的外框图片，其余位置返回空并保留默认图片。
    /// </summary>
    private Sprite GetGridCornerSprite(int row, int column)
    {
        bool isTop = row == 0;
        bool isBottom = row == m_size - 1;
        bool isLeft = column == 0;
        bool isRight = column == m_size - 1;

        if (isTop && isLeft)
            return m_blockLeftTopSprite;
        if (isTop && isRight)
            return m_blockRightTopSprite;
        if (isBottom && isLeft)
            return m_blockLeftDownSprite;
        if (isBottom && isRight)
            return m_blockRightDownSprite;

        return null;
    }

    /// <summary>
    /// 顶部数字提示框仅替换最左和最右外框。
    /// </summary>
    private Sprite GetHorizontalNumberFrameSprite(int column)
    {
        if (column == 0)
            return m_numberLeftTopSprite;
        if (column == m_size - 1)
            return m_numberRightTopSprite;

        return null;
    }

    /// <summary>
    /// 左侧数字提示框仅替换最上和最下外框。
    /// </summary>
    private Sprite GetVerticalNumberFrameSprite(int row)
    {
        if (row == 0)
            return m_numberLeftTopSprite;
        if (row == m_size - 1)
            return m_numberLeftDownSprite;

        return null;
    }

    /// <summary>
    /// 创建分隔线段
    /// </summary>
    protected override void CreateLine()
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
    private Vector2 GetFontSize()
    {
        Vector2 fontSize = new Vector2(32, 32);

        switch (m_size)
        {
            case 5:
                fontSize = new Vector2(55, 70);
                break;
            case 10:
                fontSize = new Vector2(40, 40);
                break;
            case 15:
                fontSize = new Vector2(32, 32);
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
        ColorUtility.TryParseHtmlString("#B38337", out Color color);
        img.color = color;

        return rectTransform;
    }
}
