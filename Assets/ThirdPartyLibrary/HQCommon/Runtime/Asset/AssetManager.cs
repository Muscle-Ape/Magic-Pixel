using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;
using YooAsset;
using Joy;

namespace Joy.Asset
{
    public class AssetManager : Singleton<AssetManager>
    {
        public static string PackageName = "Main";

        private readonly Dictionary<string, AssetHandle> m_dicRefAssets = new Dictionary<string, AssetHandle>();

        public async UniTask InitAsync()
        {
            YooAssets.Initialize();
#if UNITY_EDITOR
            EPlayMode playMode = EPlayMode.EditorSimulateMode;
#else
            EPlayMode playMode = EPlayMode.OfflinePlayMode;
#endif

            // 创建默认的资源包
            string packageName = PackageName;
            var package = YooAssets.TryGetPackage(packageName);
            if (package == null)
            {
                package = YooAssets.CreatePackage(packageName);
            }
            YooAssets.SetDefaultPackage(package);

            if (package.InitializeStatus == EOperationStatus.Succeed)
            {
                return;
            }

            if (playMode == EPlayMode.EditorSimulateMode)
            {
                // 编辑器下的模拟模式
                var buildResult = EditorSimulateModeHelper.SimulateBuild(packageName);
                var fileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(
                    buildResult.PackageRootDirectory);
                var param = new EditorSimulateModeParameters
                {
                    EditorFileSystemParameters = fileSystemParameters
                };
                InitializationOperation operation = package.InitializeAsync(param);
                await operation.Task;
                if (operation.Status != EOperationStatus.Succeed)
                {
                    Joy.Debug.LogError($"LauncherYooAsset: {operation.Error}");
                    return;
                }
            }
            else if (playMode == EPlayMode.OfflinePlayMode)
            {
                // 单机运行模式
                var fileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(
                    new AESDecryptionServices());
                var param = new OfflinePlayModeParameters
                {
                    BuildinFileSystemParameters = fileSystemParameters
                };
                InitializationOperation operation = package.InitializeAsync(param);
                await operation.Task;
                if (operation.Status != EOperationStatus.Succeed)
                {
                    Joy.Debug.LogError($"LauncherYooAsset: {operation.Error}");
                    return;
                }
            }
        }

        public bool IsExists(string assetName)
        {
            var info = YooAssets.GetAssetInfo(assetName);
            if (string.IsNullOrEmpty(info.AssetPath))
            {
                return false;
            }
            return true;
        }

        public T LoadAsset<T>(string assetName) where T : UnityEngine.Object
        {
            AssetHandle handler = YooAssets.LoadAssetSync<T>(assetName);
            return handler.AssetObject as T;
        }

        public UnityEngine.Object LoadAsset(string assetName, System.Type type)
        {
            return YooAssets.LoadAssetSync(assetName, type).AssetObject;
        }

        public async UniTask<T> LoadAssetAsync<T>(string assetName) where T : UnityEngine.Object
        {
            AssetHandle handler = YooAssets.LoadAssetAsync<T>(assetName);
            await handler.Task;
            return handler.AssetObject as T;
        }

        public void LoadAssetAsync<T>(string assetName, UnityAction<T> callback) where T : UnityEngine.Object
        {
            AssetHandle handler = YooAssets.LoadAssetAsync<T>(assetName);
            handler.Completed += (_handler) =>
            {
                callback?.Invoke(_handler.AssetObject as T);
            };
        }

        public void LoadAssetAsync(string assetName, System.Type assetType, UnityAction<UnityEngine.Object> callback)
        {
            AssetHandle handler = YooAssets.LoadAssetAsync(assetName, assetType);
            handler.Completed += (_handler) =>
            {
                callback?.Invoke(_handler.AssetObject);
            };
        }

        public async UniTask LoadSceneAsync(string sceneName)
        {
            SceneHandle handler = YooAssets.LoadSceneAsync(sceneName);
            await handler.Task;
        }

        public void LoadSceneAsync(string sceneName, UnityAction callback)
        {
            SceneHandle handler = YooAssets.LoadSceneAsync(sceneName);
            handler.Completed += (_handler) =>
            {
                callback?.Invoke();
            };
        }

        public void UnloadAsset(string assetName)
        {

        }
    }
}
