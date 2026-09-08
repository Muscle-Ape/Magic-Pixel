using DG.Tweening;
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

        StopCustomGridWave();
        for (int i = 0; i < m_customBlocks.Count; i++)
        {
            if (m_customBlocks[i] != null)
                m_customBlockPool.Release(m_customBlocks[i]);
        }

        m_customBlocks.Clear();
    }

    /// <summary>
    /// 尺寸切换后播放格子缩放波浪。5×5 从左上到右下，10×10 从右上到左下。
    /// 最后一个对角线动画结束时，整段时长固定为 0.7 秒。
    /// </summary>
    private void PlayCustomGridWave(bool fromRightTop)
    {
        StopCustomGridWave();
        if (m_customBlocks == null || m_customBlocks.Count == 0)
            return;

        int size = Mathf.Max(1, m_customCurrentSize);
        int diagonalCount = size * 2 - 1;
        Sequence sequence = DOTween.Sequence();
        sequence.AppendInterval(CUSTOM_GRID_WAVE_TOTAL_DURATION);

        for (int i = 0; i < m_customBlocks.Count; i++)
        {
            MPCustomBlock block = m_customBlocks[i];
            if (block == null)
                continue;

            Transform blockTransform = block.transform;
            blockTransform.DOKill();
            blockTransform.localScale = Vector3.zero;

            int row = i / size;
            int column = i % size;
            int diagonal = fromRightTop
                ? row + size - 1 - column
                : row + column;
            float delay = diagonalCount <= 1
                ? 0f
                : CUSTOM_GRID_WAVE_DELAY_SPAN * diagonal / (diagonalCount - 1f);

            sequence.Insert(
                delay,
                blockTransform
                    .DOScale(Vector3.one, CUSTOM_GRID_WAVE_ITEM_DURATION)
                    .SetEase(Ease.OutBack));
        }

        m_customGridWaveSequence = sequence;
        sequence.SetUpdate(true);
        sequence.SetLink(gameObject);
        sequence.OnComplete(() =>
        {
            if (m_customGridWaveSequence == sequence)
                m_customGridWaveSequence = null;
        });
    }

    /// <summary>停止尺寸切换动画，并确保对象池中的方块恢复最终缩放。</summary>
    private void StopCustomGridWave()
    {
        m_customGridWaveSequence?.Kill();
        m_customGridWaveSequence = null;
        if (m_customBlocks == null)
            return;

        for (int i = 0; i < m_customBlocks.Count; i++)
        {
            MPCustomBlock block = m_customBlocks[i];
            if (block == null)
                continue;

            block.transform.DOKill();
            block.transform.localScale = Vector3.one;
        }
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
