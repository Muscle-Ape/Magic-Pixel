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

        RefreshCustomGridSprites(size);
    }

    /// <summary>
    /// 根据当前尺寸统一刷新外框及显示颜色的底图。
    /// MPCustomBlock 内部会跳过相同 Sprite，避免对象池复用时反复赋值。
    /// </summary>
    private void RefreshCustomGridSprites(int size)
    {
        if (m_customBlocks == null)
            return;

        Sprite frameSprite = size == 10
            ? m_customBlockFrameTenSprite
            : m_customBlockFrameFiveSprite;
        Sprite fillSprite = size == 10
            ? m_customBlockFillTenSprite
            : m_customBlockFillFiveSprite;

        for (int i = 0; i < m_customBlocks.Count; i++)
        {
            MPCustomBlock block = m_customBlocks[i];
            if (block == null)
                continue;

            block.SetFrameSprite(frameSprite);
            block.SetFillSprite(fillSprite);
        }
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
