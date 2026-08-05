using System.Collections.Generic;
using UnityEngine;

public partial class MPLargeImageGameView
{
    /// <summary>
    /// 恢复大图关卡进度缓存。
    /// </summary>
    protected override void RestoreProgressCache()
    {
        MPLevelProgressCacheInfo cacheInfo = MPUser.instance.GetLargeImageLevelProgressCache(m_blockInfo.ID);
        if (cacheInfo == null)
            return;

        m_isRestoringProgress = true;

        RestoreLoves(cacheInfo.UsedLoves);
        RestoreBlockStatues(cacheInfo.CompletedBlocks);
        RestoreViewPosition(cacheInfo.ViewX, cacheInfo.ViewY);
        RefreshContent();
        RecalculateCompletedCount();

        m_isRestoringProgress = false;
    }

    /// <summary>
    /// 保存大图关卡进度缓存。
    /// </summary>
    protected override void SaveProgressCache()
    {
        if (m_hasCompleted || m_blockInfo == null || m_blockStatues == null)
            return;

        MPLevelProgressCacheInfo cacheInfo = new MPLevelProgressCacheInfo();
        cacheInfo.UsedLoves = Mathf.Clamp(m_loves.Count - m_lovesCount, 0, m_loves.Count);
        cacheInfo.ViewX = m_blockStatueHead.x;
        cacheInfo.ViewY = m_blockStatueHead.y;

        for (int i = 0; i < m_size; i++)
        {
            for (int j = 0; j < m_size; j++)
            {
                if (m_blockStatues[i][j] != BlockStatue.Empty)
                {
                    cacheInfo.CompletedBlocks.Add(i * m_size + j);
                }
            }
        }

        MPUser.instance.SaveLargeImageLevelProgressCache(m_blockInfo.ID, cacheInfo);
    }

    /// <summary>
    /// 清理当前大图关卡进度缓存。
    /// </summary>
    protected override void ClearProgressCache()
    {
        if (m_blockInfo != null)
        {
            MPUser.instance.ClearLargeImageLevelProgressCache(m_blockInfo.ID);
        }
    }

    /// <summary>
    /// 根据已完成格子下标恢复完整大图的格子状态。
    /// </summary>
    /// <param name="completedBlocks">已经操作完成的格子下标列表。</param>
    private void RestoreBlockStatues(List<int> completedBlocks)
    {
        if (completedBlocks == null)
            return;

        HashSet<int> completedSet = new HashSet<int>(completedBlocks);
        for (int i = 0; i < m_size; i++)
        {
            for (int j = 0; j < m_size; j++)
            {
                int index = i * m_size + j;
                if (!completedSet.Contains(index))
                    continue;

                m_blockStatues[i][j] = m_blockInfo.Block.Contains(index) ? BlockStatue.Fill : BlockStatue.Blank;
            }
        }
    }

    /// <summary>
    /// 恢复大图关卡当前显示窗口位置。
    /// </summary>
    /// <param name="viewX">窗口左上角所在行下标。</param>
    /// <param name="viewY">窗口左上角所在列下标。</param>
    private void RestoreViewPosition(int viewX, int viewY)
    {
        int maxHead = Mathf.Max(0, m_size - FIXED_SIZE);
        m_blockStatueHead = new Vector2Int(Mathf.Clamp(viewX, 0, maxHead), Mathf.Clamp(viewY, 0, maxHead));
    }

    /// <summary>
    /// 重新计算完整大图已完成的行列数量。
    /// </summary>
    private void RecalculateCompletedCount()
    {
        m_hvCompleted = 0;

        for (int i = 0; i < m_size; i++)
        {
            bool rowFinish = true;
            bool columnFinish = true;

            for (int j = 0; j < m_size; j++)
            {
                if (m_blockStatues[i][j] == BlockStatue.Empty)
                {
                    rowFinish = false;
                }

                if (m_blockStatues[j][i] == BlockStatue.Empty)
                {
                    columnFinish = false;
                }
            }

            if (rowFinish)
            {
                m_hvCompleted++;
            }

            if (columnFinish)
            {
                m_hvCompleted++;
            }
        }
    }

}
