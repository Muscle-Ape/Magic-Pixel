using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏通关时需要做的事情
/// </summary>
public partial class MPLargeImageGameView
{
    /// <summary>
    /// 更新数据
    /// </summary>
    private void UpdateData()
    {
        // 1、记录当前已通关关卡
        MPUser.instance.LargeImageLevelPass(m_blockInfo.ID);

        // 2、更新解锁到的关卡位置，解锁新关卡
        if (m_index == MPUser.instance.GetLargeImageLevlPassIndex())
        {
            // 向后遍历，检查是否有通关关卡
            int newIndex = m_index;
            var blockList = MPDataManager.Instance.m_mainLevelModel.blockInfos;
            for (int i = newIndex + 1; i < blockList.Count; i++)
            {
                newIndex++;

                if (!MPUser.instance.LargeImageLevelIsPass(blockList[i].ID))
                {
                    break;
                }
            }

            MPUser.instance.SetLargeImageLevelPassIndex(newIndex);
            MPUser.instance.LargeImageLevelUnlock(blockList[newIndex].ID);
        }

        m_refreshAction?.Invoke();
    }
}
