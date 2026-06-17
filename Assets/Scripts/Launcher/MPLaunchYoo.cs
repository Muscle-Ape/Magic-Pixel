using System.Collections;
using UnityEngine;
using YooAsset;

/// <summary>
/// YooAsset 初始化组件。
/// </summary>
public class MPLaunchYoo
{

    private string m_packageName = "Main";

    public IEnumerator Initialize()
    {
        // 初始化资源系统
        YooAssets.Initialize();

        var package = YooAssets.TryGetPackage(m_packageName);
        // 获取指定的资源包，如果没有找到不会报错
        if (package == null)
        {
            package = YooAssets.CreatePackage(m_packageName);
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
        {
            Log("资源包初始化成功！");
        }
        else
        {
            Debug.LogError($"资源包初始化失败：{operation.Error}");
            yield break;
        }

        var op = package.RequestPackageVersionAsync();
        yield return op;

        if (op.Status == EOperationStatus.Succeed)
        {
            Log("版本请求成功！");
        }
        else
        {
            Debug.LogError($"资源版本请求失败：{op.Error}");
            yield break;
        }

        yield return package.UpdatePackageManifestAsync(op.PackageVersion);

        yield return null;
    }

    private InitializationOperation EditorInitializeYooAsset(ResourcePackage package)
    {
        var buildResult = EditorSimulateModeHelper.SimulateBuild(m_packageName);
        var packageRoot = buildResult.PackageRootDirectory;
        var fileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
        var initParameters = new EditorSimulateModeParameters()
        {
            EditorFileSystemParameters = fileSystemParams
        };
        return package.InitializeAsync(initParameters);
    }

    private InitializationOperation SingleInitializeYooAsset(ResourcePackage package)
    {
        var fileSystemParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
        var initParameters = new OfflinePlayModeParameters()
        {
            BuildinFileSystemParameters = fileSystemParams
        };
        return package.InitializeAsync(initParameters);
    }

    private void Log(string msg)
    {
#if UNITY_EDITOR
        Debug.Log($"[YooLaunch]:{msg}");
#endif
    }
}