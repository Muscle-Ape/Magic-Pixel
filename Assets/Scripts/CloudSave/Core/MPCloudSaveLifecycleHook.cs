using UnityEngine;

/// <summary>
/// 云同步生命周期钩子，用于在切后台和退出时尝试刷新 dirty 数据。
/// </summary>
public class MPCloudSaveLifecycleHook : MonoBehaviour
{
    /// <summary>
    /// 应用切后台时尝试刷新云存档。
    /// </summary>
    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            _ = MPCloudSaveManager.Instance.FlushAsync();
        }
    }

    /// <summary>
    /// 应用退出时尝试刷新云存档。
    /// </summary>
    private void OnApplicationQuit()
    {
        _ = MPCloudSaveManager.Instance.FlushAsync();
    }
}
