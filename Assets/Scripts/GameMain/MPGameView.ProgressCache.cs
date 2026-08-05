using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public partial class MPGameView
{
    /// <summary>
    /// 是否正在恢复关卡进度，避免恢复过程中触发通关结算。
    /// </summary>
    private bool m_isRestoringProgress;

    /// <summary>
    /// 当前关卡是否已经完成，完成后不再写入进度缓存。
    /// </summary>
    private bool m_hasCompleted;

    /// <summary>
    /// 恢复主线关卡进度缓存。
    /// </summary>
    private void RestoreProgressCache()
    {
        if (m_isCustomLevel)
            return;

        MPLevelProgressCacheInfo cacheInfo = MPUser.instance.GetMainLevelProgressCache(m_blockInfo.ID);
        if (cacheInfo == null)
            return;

        m_isRestoringProgress = true;

        RestoreLoves(cacheInfo.UsedLoves);
        RestoreBlocks(cacheInfo.CompletedBlocks);

        m_isRestoringProgress = false;
    }

    /// <summary>
    /// 保存主线关卡进度缓存。
    /// </summary>
    private void SaveProgressCache()
    {
        if (m_isCustomLevel || m_hasCompleted || m_blockInfo == null || m_blocks == null)
            return;

        MPLevelProgressCacheInfo cacheInfo = new MPLevelProgressCacheInfo();
        cacheInfo.UsedLoves = Mathf.Clamp(m_loves.Count - m_lovesCount, 0, m_loves.Count);

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
    private void ClearProgressCache()
    {
        if (!m_isCustomLevel && m_blockInfo != null)
        {
            MPUser.instance.ClearMainLevelProgressCache(m_blockInfo.ID);
        }
    }

    /// <summary>
    /// 根据已消耗生命值恢复生命显示。
    /// </summary>
    /// <param name="usedLoves">已经消耗的生命值数量。</param>
    private void RestoreLoves(int usedLoves)
    {
        usedLoves = Mathf.Clamp(usedLoves, 0, m_loves.Count);
        m_lovesCount = m_loves.Count - usedLoves;

        for (int i = 0; i < m_loves.Count; i++)
        {
            m_loves[i].transform.DOKill();
            m_loves[i].transform.localScale = Vector3.one;
            m_loves[i].SetActive(i < m_lovesCount);
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

    /// <summary>
    /// 窗口释放时保存当前关卡进度并清理Tween。
    /// </summary>
    public override void OnRelease()
    {
        SaveProgressCache();
        m_modeSwitchTween?.Kill();
        ReleaseRuntimePixelTexture();
        MPLoad.ReleaseAll(this);
    }

    /// <summary>
    /// 游戏进入后台时保存当前关卡进度。
    /// </summary>
    /// <param name="pause">是否进入后台。</param>
    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            SaveProgressCache();
        }
    }

    /// <summary>
    /// 游戏退出时保存当前关卡进度。
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveProgressCache();
    }
}
