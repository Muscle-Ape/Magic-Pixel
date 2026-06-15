using UnityEngine;
using System.Threading.Tasks;
public interface IAssetLoader
{
    public T Load<T>(string path) where T : Object;
    public Task<T> AsyncLoad<T>(string path) where T : Object;
    public void Unload(string path);
}