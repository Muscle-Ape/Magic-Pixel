using UnityEngine;
using System.Threading.Tasks;

namespace HQ.UIManager
{
    public class ResourcesAssetLoader : IAssetLoader
    {
        public T Load<T>(string path) where T : Object
        {
            return Resources.Load<T>(path);
        }
        public async Task<T> AsyncLoad<T>(string path) where T : Object
        {
            ResourceRequest request = Resources.LoadAsync(path);

            while (!request.isDone)
            {
                await Task.Yield(); // Yield the thread to avoid blocking the main thread.
            }

            T loadedPrefab = request.asset as T;
            return loadedPrefab;
        }

        public void Unload(string path)
        {
            
        }
    }
}
