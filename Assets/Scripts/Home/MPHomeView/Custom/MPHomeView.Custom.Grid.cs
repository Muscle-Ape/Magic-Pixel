using UnityEngine;
using UnityEngine.UI;

public partial class MPHomeView
{
    private void CreateCustomGrid(int size)
    {
        if (!m_customInitialized || m_customBlockPool == null || m_customBlockGrid == null)
            return;

        ClearCustomGrid();
        m_customCurrentSize = size;
        m_customBlockGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        m_customBlockGrid.constraintCount = size;
        m_customBlockGrid.cellSize = Vector2.one * (CUSTOM_GRID_SIZE / (float)size);

        int cellCount = size * size;
        for (int i = 0; i < cellCount; i++)
        {
            MPCustomBlock block = m_customBlockPool.Get();
            block.ClearColor();
            block.Fill(false);
            block.SetMode(m_customIsFillMode);
            block.SetFrameSprite(GetCustomGridCornerSprite(i / size, i % size, size));
            m_customBlocks.Add(block);
        }
    }

    /// <summary>
    /// 获取自定义网格四角外框，其余格子恢复预制体默认外框。
    /// </summary>
    private Sprite GetCustomGridCornerSprite(int row, int column, int size)
    {
        bool isTop = row == 0;
        bool isBottom = row == size - 1;
        bool isLeft = column == 0;
        bool isRight = column == size - 1;

        if (isTop && isLeft)
            return m_customBlockLeftTopSprite;
        if (isTop && isRight)
            return m_customBlockRightTopSprite;
        if (isBottom && isLeft)
            return m_customBlockLeftDownSprite;
        if (isBottom && isRight)
            return m_customBlockRightDownSprite;

        return null;
    }

    private void ClearCustomGrid()
    {
        if (m_customBlocks == null || m_customBlockPool == null)
            return;

        for (int i = 0; i < m_customBlocks.Count; i++)
        {
            if (m_customBlocks[i] != null)
                m_customBlockPool.Release(m_customBlocks[i]);
        }

        m_customBlocks.Clear();
    }

    private MPCustomBlock CreateCustomBlock()
    {
        MPCustomBlock block = Instantiate(m_customBlockPrefab, m_customBlockGrid.transform);
        block.Init();
        return block;
    }

    private static void GetCustomBlock(MPCustomBlock block)
    {
        if (block != null)
            block.gameObject.SetActive(true);
    }

    private static void ReleaseCustomBlock(MPCustomBlock block)
    {
        if (block != null)
            block.gameObject.SetActive(false);
    }

    private static void DestroyCustomBlock(MPCustomBlock block)
    {
        if (block != null)
            Destroy(block.gameObject);
    }
}
