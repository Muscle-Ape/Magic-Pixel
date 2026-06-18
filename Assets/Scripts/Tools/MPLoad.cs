using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using YooAsset;

public static class MPLoad
{
    /// <summary>
    /// 同步加载资源
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <returns></returns>
    public static T Load<T>(string url) where T : UnityEngine.Object
    {
        return YooAssets.LoadAssetSync<T>(url).AssetObject as T;
    }

    /// <summary>
    /// 异步加载资源
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <returns></returns>
    public static async Task<T> LoadAsync<T>(string url) where T : UnityEngine.Object
    {
        return YooAssets.LoadAssetAsync<T>(url).AssetObject as T; 
    }
}
