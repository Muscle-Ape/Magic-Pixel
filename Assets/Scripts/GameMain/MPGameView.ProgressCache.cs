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

        MPLevelProgressCacheInfo cacheInfo = MPUser.instance.GetMainLevelProgressCache(m_blockInfo.ID);
        if (cacheInfo == null)
            return;

        m_isRestoringProgress = true;

        RestoreLoves(cacheInfo.UsedLoves);
        RestorePetSkillUsage(cacheInfo.PetId, cacheInfo.UsedPetSkillCount);
        RestoreBlocks(cacheInfo.CompletedBlocks);

        m_isRestoringProgress = false;
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
