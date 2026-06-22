using HQ.UIManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        float singleSize = GRID_SIZE / m_size;
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

            if (number.Count == 0)
                number.Add(0);

            numbers.Add(i, number);
        }

        for (int i = 0; i < m_size; i++)
        {
            MPGameNumberFrameHorizontal frame = Instantiate(m_numberHorizontalPrefab, m_numberHorizontal);
            frame.Init(numbers[i]);
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

            if (number.Count == 0)
                number.Add(0);

            numbers.Add(i, number);
        }

        for (int i = 0; i < m_size; i++)
        {
            MPGameNumberFrameVertical frame = Instantiate(m_numberVerticalPrefab, m_numberVertical);
            frame.Init(numbers[i]);
        }
    }

    /// <summary>
    /// 创建分隔线段
    /// </summary>
    private void CreateLine()
    {

    }
}
