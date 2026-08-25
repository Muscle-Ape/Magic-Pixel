using Newtonsoft.Json;
using System;
using System.Collections.Generic;

/// <summary>
/// 关卡游戏进度缓存数据。
/// </summary>
public class MPLevelProgressCacheInfo
{
    /// <summary>
    /// 已经操作完成的格子下标列表。
    /// </summary>
    public List<int> CompletedBlocks = new List<int>();

    /// <summary>
    /// 当前关卡已经消耗的生命值数量。
    /// </summary>
    public int UsedLoves;

    /// <summary>
    /// 创建当前关卡进度时使用的宠物 ID。
    /// </summary>
    public string PetId;

    /// <summary>
    /// 当前关卡已经使用的宠物免费技能次数。
    /// </summary>
    public int UsedPetSkillCount;

    /// <summary>
    /// 大图关卡当前窗口左上角所在行下标。
    /// </summary>
    public int ViewX;

    /// <summary>
    /// 大图关卡当前窗口左上角所在列下标。
    /// </summary>
    public int ViewY;
}

public partial class MPUser
{
    /// <summary>
    /// 主线关卡进度缓存Key前缀。
    /// </summary>
    private string m_key_mainlevel_progress_cache_prefix = "key_mainlevel_progress_cache_";

    /// <summary>
    /// 大图关卡进度缓存Key前缀。
    /// </summary>
    private string m_key_largeimagelevel_progress_cache_prefix = "key_largeimagelevel_progress_cache_";

    /// <summary>
    /// 保存主线关卡进度缓存。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <param name="cacheInfo">关卡进度缓存数据。</param>
    public void SaveMainLevelProgressCache(string id, MPLevelProgressCacheInfo cacheInfo)
    {
        SaveLevelProgressCache(GetMainLevelProgressCacheKey(id), cacheInfo);
    }

    /// <summary>
    /// 获取主线关卡进度缓存。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>关卡进度缓存数据，不存在或解析失败时返回null。</returns>
    public MPLevelProgressCacheInfo GetMainLevelProgressCache(string id)
    {
        return GetLevelProgressCache(GetMainLevelProgressCacheKey(id));
    }

    /// <summary>
    /// 清理主线关卡进度缓存。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    public void ClearMainLevelProgressCache(string id)
    {
        ClearLevelProgressCache(GetMainLevelProgressCacheKey(id));
    }

    /// <summary>
    /// 保存大图关卡进度缓存。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <param name="cacheInfo">关卡进度缓存数据。</param>
    public void SaveLargeImageLevelProgressCache(string id, MPLevelProgressCacheInfo cacheInfo)
    {
        SaveLevelProgressCache(GetLargeImageLevelProgressCacheKey(id), cacheInfo);
    }

    /// <summary>
    /// 获取大图关卡进度缓存。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>关卡进度缓存数据，不存在或解析失败时返回null。</returns>
    public MPLevelProgressCacheInfo GetLargeImageLevelProgressCache(string id)
    {
        return GetLevelProgressCache(GetLargeImageLevelProgressCacheKey(id));
    }

    /// <summary>
    /// 清理大图关卡进度缓存。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    public void ClearLargeImageLevelProgressCache(string id)
    {
        ClearLevelProgressCache(GetLargeImageLevelProgressCacheKey(id));
    }

    /// <summary>
    /// 获取主线关卡进度缓存Key。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>ES3存档Key。</returns>
    private string GetMainLevelProgressCacheKey(string id)
    {
        return m_key_mainlevel_progress_cache_prefix + id;
    }

    /// <summary>
    /// 获取大图关卡进度缓存Key。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>ES3存档Key。</returns>
    private string GetLargeImageLevelProgressCacheKey(string id)
    {
        return m_key_largeimagelevel_progress_cache_prefix + id;
    }

    /// <summary>
    /// 保存指定Key对应的关卡进度缓存。
    /// </summary>
    /// <param name="key">ES3存档Key。</param>
    /// <param name="cacheInfo">关卡进度缓存数据。</param>
    private void SaveLevelProgressCache(string key, MPLevelProgressCacheInfo cacheInfo)
    {
        if (string.IsNullOrEmpty(key) || cacheInfo == null)
            return;

        ES3.Save(key, JsonConvert.SerializeObject(cacheInfo));
    }

    /// <summary>
    /// 获取指定Key对应的关卡进度缓存。
    /// </summary>
    /// <param name="key">ES3存档Key。</param>
    /// <returns>关卡进度缓存数据，不存在或解析失败时返回null。</returns>
    private MPLevelProgressCacheInfo GetLevelProgressCache(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        string json = ES3.Load<string>(key, defaultValue: null);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<MPLevelProgressCacheInfo>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 清理指定Key对应的关卡进度缓存。
    /// </summary>
    /// <param name="key">ES3存档Key。</param>
    private void ClearLevelProgressCache(string key)
    {
        if (!string.IsNullOrEmpty(key) && ES3.KeyExists(key))
        {
            ES3.DeleteKey(key);
        }
    }
}
