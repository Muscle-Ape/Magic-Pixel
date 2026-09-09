using System.Collections.Generic;
using UnityEngine;

public partial class MPGameView
{
    /// <summary>
    /// 恢复主线关卡进度缓存。
    /// </summary>
    protected override void RestoreProgressCache()
    {
        if (m_isCustomLevel)
            return;

        MPLevelProgressCacheInfo cacheInfo = m_progressCacheValidated
            ? m_entryProgressCache
            : MPUser.instance.GetMainLevelProgressCache(m_blockInfo.ID);
        cacheInfo = cacheInfo?.GetValidIncompleteCopy(m_size, false, m_loves.Count);

        m_isRestoringProgress = true;

        try
        {
            // 默认叉只属于“新开局”数据。存在有效缓存时完全以缓存为准，
            // 避免配置热更新后改变玩家已经开始的盘面。
            if (cacheInfo == null)
            {
                RestoreBlocks(GetValidDefaultBlankIndexes());
                return;
            }

            RestoreLoves(cacheInfo.UsedLoves);
            RestorePetSkillUsage(cacheInfo.PetId, cacheInfo.UsedPetSkillCount);
            RestoreBlocks(cacheInfo.CompletedBlocks);
        }
        finally
        {
            m_isRestoringProgress = false;
            m_entryProgressCache = null;
        }
    }

    /// <summary>
    /// 过滤配置中的越界、重复和答案格下标。
    /// blank 的比例只用于编辑器自动生成，手动配置的合法下标会全部生效。
    /// </summary>
    private List<int> GetValidDefaultBlankIndexes()
    {
        List<int> configuredIndexes = m_blockInfo?.Blank;
        if (configuredIndexes == null || configuredIndexes.Count == 0)
            return null;

        int cellCount = m_size * m_size;
        HashSet<int> fillIndexes = new HashSet<int>();
        List<int> blockIndexes = m_blockInfo.Block;
        if (blockIndexes != null)
        {
            for (int i = 0; i < blockIndexes.Count; i++)
            {
                int index = blockIndexes[i];
                if (index >= 0 && index < cellCount)
                {
                    fillIndexes.Add(index);
                }
            }
        }

        HashSet<int> uniqueIndexes = new HashSet<int>();
        List<int> validIndexes = new List<int>(configuredIndexes.Count);

        for (int i = 0; i < configuredIndexes.Count; i++)
        {
            int index = configuredIndexes[i];
            if (index < 0 || index >= cellCount || fillIndexes.Contains(index) || !uniqueIndexes.Add(index))
                continue;

            validIndexes.Add(index);
        }

        if (validIndexes.Count != configuredIndexes.Count)
        {
            Debug.LogWarning(
                $"[MPGameView] 关卡 {m_blockInfo.ID} 的 blank 配置不符合要求：" +
                $"配置 {configuredIndexes.Count} 个，有效 {validIndexes.Count} 个。" +
                "已忽略越界、重复或与 block 重复的数据。");
        }

        return validIndexes;
    }

    /// <summary>
    /// 保存主线关卡进度缓存。
    /// </summary>
    protected override void SaveProgressCache()
    {
        if (m_isCustomLevel || m_hasCompleted || m_blockInfo == null || m_blocks == null)
            return;

        MPLevelProgressCacheInfo cacheInfo = new MPLevelProgressCacheInfo();
        cacheInfo.UsedLoves = Mathf.Clamp(m_loves.Count - m_lovesCount, 0, m_loves.Count);
        WritePetSkillUsage(cacheInfo);

        for (int i = 0; i < m_blocks.Count; i++)
        {
            if (m_blocks[i].completed)
            {
                cacheInfo.CompletedBlocks.Add(m_blocks[i].index);
            }
        }

        MPUser.instance.SaveMainLevelProgressCache(m_blockInfo.ID, cacheInfo);
    }

    /// <summary>
    /// 清理当前主线关卡进度缓存。
    /// </summary>
    protected override void ClearProgressCache()
    {
        if (!m_isCustomLevel && m_blockInfo != null)
        {
            MPUser.instance.ClearMainLevelProgressCache(m_blockInfo.ID);
        }
    }

    /// <summary>
    /// 根据已完成格子下标恢复格子状态和数字提示。
    /// </summary>
    /// <param name="completedBlocks">已经操作完成的格子下标列表。</param>
    private void RestoreBlocks(List<int> completedBlocks)
    {
        if (completedBlocks == null || completedBlocks.Count == 0)
            return;

        HashSet<int> completedSet = new HashSet<int>(completedBlocks);
        for (int i = 0; i < m_blocks.Count; i++)
        {
            MPGameBlock block = m_blocks[i];
            if (!completedSet.Contains(block.index))
                continue;

            if (block.isFill)
            {
                block.Fill(false);
            }
            else
            {
                block.Blank(false);
            }

            block.Disable();
        }

        for (int i = 0; i < m_blocks.Count; i++)
        {
            if (m_blocks[i].completed)
            {
                Check(m_blocks[i]);
            }
        }
    }

}
