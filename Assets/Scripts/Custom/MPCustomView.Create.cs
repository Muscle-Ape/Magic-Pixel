using UnityEngine;


public partial class MPCustomView
{

    /// <summary>
    /// 创建Block网格
    /// </summary>
    private void CreateGrid(int size)
    {
        ClearGrid();

        // 计算Grid单个格子大小
        float singleSize = GRID_SIZE / (float)size;
        m_blockGrid.cellSize = Vector2.one * singleSize;

        // 创建网格格子
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                MPCustomBlock block = m_blockPool.Get();

                block.ClearColor();
                block.Fill(false);

                m_blocks.Add(block);
            }
        }
    }

    /// <summary>
    /// 清空已经创建的方块
    /// </summary>
    private void ClearGrid()
    {
        if (m_blocks == null || m_blocks.Count == 0)
            return;

        for (int i = 0; i < m_blocks.Count; i++)
        {
            m_blockPool.Release(m_blocks[i]);
        }

        m_blocks.Clear();
    }


    private MPCustomBlock PoolCreate()
    {
        MPCustomBlock block = Instantiate(m_blockPrefab, m_blockGrid.transform);
        block.Init();

        return block;
    }

    private void PoolGet(MPCustomBlock block)
    {
        block.gameObject.SetActive(true);
    }

    private void PoolRelease(MPCustomBlock block)
    {
        block.gameObject.SetActive(false);
    }
}
