#if yoo_asset
using UnityEngine;
using System.Threading.Tasks;
using YooAsset;
namespace HQ.UIManager
{
    public class YooAssetLoader : IAssetLoader
    {
        public T Load<T>(string path) where T : Object
        {
            var handle = YooAssets.LoadAssetSync<T>(path);
            return handle.AssetObject as T;
        }

        public async Task<T> AsyncLoad<T>(string path) where T : Object
        {
            var handle = YooAssets.LoadAssetAsync<T>(path);
            await handle.Task;
            return handle.AssetObject as T;
        }

        public void Unload(string path)
        {
               
        }
    }
}
#endif