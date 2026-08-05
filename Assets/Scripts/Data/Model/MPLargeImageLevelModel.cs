using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大图关卡在列表中的展示状态。
/// </summary>
public enum MPLargeImageLevelState
{
    /// <summary>
    /// 未解锁。
    /// </summary>
    Locked,

    /// <summary>
    /// 已解锁但未完成。
    /// </summary>
    Unlocked,

    /// <summary>
    /// 已完成。
    /// </summary>
    Completed,
}

/// <summary>
/// 大图模式关卡数据模型。
/// </summary>
public class MPLargeImageLevelModel
{
    /// <summary>
    /// 大图模式所有关卡数据。
    /// </summary>
    public List<MPLargeImageBlockInfo> blockInfos;

    /// <summary>
    /// 获取关卡在列表中的展示状态。
    /// </summary>
    /// <param name="levelInfo">大图关卡数据。</param>
    /// <returns>当前关卡展示状态。</returns>
    public static MPLargeImageLevelState GetLevelState(MPLargeImageBlockInfo levelInfo)
    {
        if (levelInfo == null || !MPUser.instance.LargeImageLevelIsUnlock(levelInfo.ID))
        {
            return MPLargeImageLevelState.Locked;
        }

        if (MPUser.instance.LargeImageLevelIsPass(levelInfo.ID))
        {
            return MPLargeImageLevelState.Completed;
        }

        return MPLargeImageLevelState.Unlocked;
    }

    /// <summary>
    /// 获取关卡图片尺寸，优先使用大图纹理尺寸。
    /// </summary>
    /// <param name="levelInfo">大图关卡数据。</param>
    /// <returns>关卡宽高。</returns>
    public static Vector2Int GetLevelSize(MPLargeImageBlockInfo levelInfo)
    {
        if (levelInfo == null)
        {
            return Vector2Int.zero;
        }

        using (MPAssetLoadLease<Texture2D> lease = MPLoad.LoadLease<Texture2D>(levelInfo.ID))
        {
            Texture2D pixel = lease.Asset;
            if (pixel != null)
            {
                return new Vector2Int(pixel.width, pixel.height);
            }
        }

        int size = GetFallbackLevelSize(levelInfo);
        return new Vector2Int(size, size);
    }

    /// <summary>
    /// 根据大图关卡进度缓存计算当前完成比例。
    /// </summary>
    /// <param name="levelInfo">大图关卡数据。</param>
    /// <returns>0到1之间的完成进度。</returns>
    public static float GetLevelProgress(MPLargeImageBlockInfo levelInfo)
    {
        if (levelInfo == null)
        {
            return 0f;
        }

        Vector2Int size = GetLevelSize(levelInfo);
        int totalCount = size.x * size.y;
        if (totalCount <= 0)
        {
            return 0f;
        }

        MPLevelProgressCacheInfo cacheInfo = MPUser.instance.GetLargeImageLevelProgressCache(levelInfo.ID);
        if (cacheInfo == null || cacheInfo.CompletedBlocks == null)
        {
            return 0f;
        }

        HashSet<int> completedBlocks = new HashSet<int>(cacheInfo.CompletedBlocks);
        return Mathf.Clamp01((float)completedBlocks.Count / totalCount);
    }

    /// <summary>
    /// 获取大图关卡通关星数。
    /// </summary>
    /// <param name="levelInfo">大图关卡数据。</param>
    /// <returns>通关星数。</returns>
    public static int GetLevelStars(MPLargeImageBlockInfo levelInfo)
    {
        if (levelInfo == null)
        {
            return 0;
        }

        return MPUser.instance.GetLargeImageLevelStars(levelInfo.ID);
    }

    /// <summary>
    /// 当图片资源未加载到时，根据配置中最大的格子下标估算关卡尺寸。
    /// </summary>
    /// <param name="levelInfo">大图关卡数据。</param>
    /// <returns>估算出的正方形边长。</returns>
    private static int GetFallbackLevelSize(MPLargeImageBlockInfo levelInfo)
    {
        if (levelInfo.Block == null || levelInfo.Block.Count == 0)
        {
            return 0;
        }

        int maxIndex = 0;
        for (int i = 0; i < levelInfo.Block.Count; i++)
        {
            maxIndex = Mathf.Max(maxIndex, levelInfo.Block[i]);
        }

        return Mathf.CeilToInt(Mathf.Sqrt(maxIndex + 1));
    }
}
