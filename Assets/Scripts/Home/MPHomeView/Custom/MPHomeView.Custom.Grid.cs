using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class MPHomeView
{
    /// <summary>
    /// 将仓库中选中的未上传关卡加载到主页编辑器。
    /// 保存时继续使用原 ID，从而覆盖原关卡数据和像素图。
    /// </summary>
    private void BeginEditCustomLevel(MPCustomLevelInfo levelInfo)
    {
        if (!m_customInitialized || levelInfo == null || string.IsNullOrEmpty(levelInfo.ID))
            return;

        if (MPCustomLevelPublishManager.Instance.IsPublishPending(levelInfo.ID)
            || MPCustomLevelPublishManager.Instance.IsLocalLevelPublished(levelInfo.ID))
        {
            Debug.LogWarning($"[MPHomeView] 已上传或正在上传的关卡不允许编辑：{levelInfo.ID}");
            return;
        }

        CloseCustomPalette(false);
        m_customPendingPublishLevelInfo = null;
        m_customEditingLevelId = levelInfo.ID;

        int targetSize = levelInfo.Size == 10 ? 10 : 5;
        m_customIsTenSize = targetSize == 10;
        CreateCustomGrid(targetSize);

        if (m_customTitleInput != null)
        {
            m_customTitleInput.text = string.IsNullOrWhiteSpace(levelInfo.Title)
                ? MPUser.instance.GetDefaultCustomLevelTitle()
                : levelInfo.Title;
        }

        HashSet<int> filledIndexes = levelInfo.Block == null
            ? new HashSet<int>()
            : new HashSet<int>(levelInfo.Block);
        Dictionary<int, Color> colors = new Dictionary<int, Color>();
        if (levelInfo.Colors != null)
        {
            for (int i = 0; i < levelInfo.Colors.Count; i++)
            {
                MPCustomLevelColorInfo colorInfo = levelInfo.Colors[i];
                if (colorInfo == null
                    || colorInfo.Index < 0
                    || colorInfo.Index >= m_customBlocks.Count
                    || !ColorUtility.TryParseHtmlString(colorInfo.Color, out Color color))
                {
                    continue;
                }

                colors[colorInfo.Index] = color;
            }
        }

        for (int i = 0; i < m_customBlocks.Count; i++)
        {
            MPCustomBlock block = m_customBlocks[i];
            block.Fill(filledIndexes.Contains(i));
            if (colors.TryGetValue(i, out Color color))
                block.SetColor(color);
            else
                block.ClearColor();
        }

        RefreshCustomSizeState();
        RefreshCustomModeState();
        RefreshCustomPublishButtonState();
    }

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
            m_customBlocks.Add(block);
        }

        RefreshCustomGridFrameSprites(size);
    }

    /// <summary>
    /// 对象池复用后 Item 顺序可能变化，因此尺寸切换完成后按当前可见顺序重新设置所有外框。
    /// MPCustomBlock 内部会跳过相同 Sprite，只替换实际发生位置变化的外框。
    /// </summary>
    private void RefreshCustomGridFrameSprites(int size)
    {
        if (m_customBlocks == null)
            return;

        for (int i = 0; i < m_customBlocks.Count; i++)
        {
            MPCustomBlock block = m_customBlocks[i];
            if (block == null)
                continue;

            block.SetFrameSprite(GetCustomGridCornerSprite(i / size, i % size, size));
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
        if (block == null)
            return;

        block.gameObject.SetActive(true);
        // GridLayoutGroup 按 Hierarchy 顺序排版，确保对象池取出顺序与 m_customBlocks 一致。
        block.transform.SetAsLastSibling();
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
