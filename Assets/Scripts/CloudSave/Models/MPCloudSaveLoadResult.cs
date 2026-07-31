using System;

/// <summary>
/// Cloud Save 读取单个 Player Data Key 的结果。
/// </summary>
public class MPCloudSaveLoadResult<T>
{
    /// <summary>
    /// 云端是否存在该 Key。
    /// </summary>
    public bool exists;

    /// <summary>
    /// 反序列化后的数据。
    /// </summary>
    public T value;

    /// <summary>
    /// 当前云端数据的写锁。
    /// </summary>
    public string writeLock;

    /// <summary>
    /// 云端创建时间。
    /// </summary>
    public DateTime? created;

    /// <summary>
    /// 云端最后修改时间。
    /// </summary>
    public DateTime? modified;

    /// <summary>
    /// 创建一个不存在数据的读取结果。
    /// </summary>
    public static MPCloudSaveLoadResult<T> Missing()
    {
        return new MPCloudSaveLoadResult<T>
        {
            exists = false
        };
    }
}
