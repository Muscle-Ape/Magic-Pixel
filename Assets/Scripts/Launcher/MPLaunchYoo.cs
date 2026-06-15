using System.Collections;
using UnityEngine;
using YooAsset;

/// <summary>
/// YooAsset 初始化组件。
/// 
/// 编辑器状态 → EditorSimulateMode（无需打包，直接加载 AssetDatabase 资源）
/// 打包后运行 → OfflinePlayMode（从 StreamingAssets 加载）
/// 
/// 使用方式：挂载到启动场景的任意 GameObject 上即可，建议放在第一个加载的场景中。
/// </summary>
public class MPLaunchYoo
{

    public IEnumerator Initialize()
    {
        // 初始化资源系统
        YooAssets.Initialize();

        var package = YooAssets.TryGetPackage("DefaultPackage");
        // 获取指定的资源包，如果没有找到不会报错
        if (package == null)
        {
            package = YooAssets.CreatePackage("DefaultPackage");
        }
        YooAssets.SetDefaultPackage(package); // 设置默认的资源包

#if UNITY_EDITOR
        EPlayMode playMode = EPlayMode.EditorSimulateMode;
#else
        EPlayMode playMode = EPlayMode.OfflinePlayMode;
#endif

        InitializationOperation operation = null;
        switch (playMode)
        {
            case EPlayMode.EditorSimulateMode:
                {
                    operation = EditorInitializeYooAsset(package);
                    break;
                }
            case EPlayMode.OfflinePlayMode:
                {
                    operation = SingleInitializeYooAsset(package);
                    break;
                }
            case EPlayMode.HostPlayMode:
                {
                    break;
                }
        }

        yield return operation;

        if (operation.Status == EOperationStatus.Succeed)
            Debug.Log("资源包初始化成功！");
        else
            Debug.LogError($"资源包初始化失败：{operation.Error}");
    }

    private InitializationOperation EditorInitializeYooAsset(ResourcePackage package)
    {
        var initParameters = new EditorSimulateModeParameters();
        initParameters.SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild("DefaultPackage");
        return package.InitializeAsync(initParameters);
    }

    private InitializationOperation SingleInitializeYooAsset(ResourcePackage package)
    {
        var initParameters = new OfflinePlayModeParameters();
        return package.InitializeAsync(initParameters);
    }

}
