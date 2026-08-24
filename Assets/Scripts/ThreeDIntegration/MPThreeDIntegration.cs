using System;
using System.Collections.Generic;
using HQ.UIManager;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 3D 模块与当前项目的接入点。模块本身不依赖 ES3、项目 UI 入口或其它业务系统。
/// </summary>
public static class MPThreeDIntegration
{
    private static readonly IMPThreeDStorage s_storage = new MPEasySaveThreeDStorage();

    public static void Open()
    {
        MPThreeDModuleServices.Storage = s_storage;
        UIManager.Inst.ShowWindow<MPThreeDView>();
    }
}

/// <summary>
/// 使用 Newtonsoft.Json + ES3 保存 3D 拼装数据，并保留一份最近的有效存档作为回退。
/// </summary>
internal sealed class MPEasySaveThreeDStorage : IMPThreeDStorage
{
    private enum RawReadStatus
    {
        Missing,
        Success,
        Error
    }

    private const string CURRENT_KEY = "Build3D.Assembly.Default.Current";
    private const string BACKUP_KEY = "Build3D.Assembly.Default.Backup";
    private const string DEFAULT_TITLE = "My 3D Build";
    private const int MAX_PLACED_PARTS = 101;
    private const int MAX_NON_ROOT_PARTS = 100;

    public MPThreeDAssemblySaveDto Load()
    {
        if (TryLoad(CURRENT_KEY, out MPThreeDAssemblySaveDto current))
        {
            return current;
        }

        if (TryLoad(BACKUP_KEY, out MPThreeDAssemblySaveDto backup))
        {
            Debug.LogWarning("[MPThreeD] 当前存档不可用，已回退到备份存档。");
            return backup;
        }

        return MPThreeDAssemblySaveDto.CreateEmpty();
    }

    public void Save(MPThreeDAssemblySaveDto data)
    {
        string newJson;
        try
        {
            // 先完成一次完整的 JSON 往返，避免把不可序列化或结构非法的数据写入 ES3。
            string serialized = JsonConvert.SerializeObject(data);
            if (!TryDeserializeAndNormalize(serialized, out MPThreeDAssemblySaveDto validated, out string error))
            {
                Debug.LogWarning($"[MPThreeD] 3D 存档校验失败，本次保存已取消：{error}");
                return;
            }

            newJson = JsonConvert.SerializeObject(validated);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPThreeD] 3D 存档序列化失败，本次保存已取消：{exception.Message}");
            return;
        }

        RawReadStatus oldCurrentStatus = TryReadRaw(CURRENT_KEY, out string oldCurrentJson);
        if (oldCurrentStatus == RawReadStatus.Error)
        {
            Debug.LogWarning("[MPThreeD] 读取旧 Current 失败，为保护现有存档，本次保存已取消。");
            return;
        }

        if (oldCurrentStatus == RawReadStatus.Success &&
            TryDeserializeAndNormalize(oldCurrentJson, out _, out _))
        {
            try
            {
                // 只有旧 Current 能被完整解析和校验时，才允许它覆盖 Backup。
                ES3.Save(BACKUP_KEY, oldCurrentJson);
            }
            catch (Exception exception)
            {
                // 保不住旧数据时不继续覆盖 Current，确保至少仍有一份有效存档。
                Debug.LogWarning($"[MPThreeD] 3D 备份存档写入失败，本次保存已取消：{exception.Message}");
                return;
            }
        }

        try
        {
            ES3.Save(CURRENT_KEY, newJson);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPThreeD] 3D 当前存档写入失败：{exception.Message}");
        }
    }

    private static bool TryLoad(string key, out MPThreeDAssemblySaveDto data)
    {
        RawReadStatus status = TryReadRaw(key, out string json);
        if (status != RawReadStatus.Success)
        {
            data = null;
            return false;
        }

        if (TryDeserializeAndNormalize(json, out data, out string error))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"[MPThreeD] 3D 存档 {key} 无效：{error}");
        }

        data = null;
        return false;
    }

    private static RawReadStatus TryReadRaw(string key, out string json)
    {
        json = null;
        try
        {
            if (!ES3.KeyExists(key))
            {
                return RawReadStatus.Missing;
            }

            json = ES3.Load<string>(key, defaultValue: null);
            return RawReadStatus.Success;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPThreeD] 读取 3D 存档 {key} 失败：{exception.Message}");
            return RawReadStatus.Error;
        }
    }

    private static bool TryDeserializeAndNormalize(
        string json,
        out MPThreeDAssemblySaveDto data,
        out string error)
    {
        data = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "存档内容为空";
            return false;
        }

        try
        {
            data = JsonConvert.DeserializeObject<MPThreeDAssemblySaveDto>(json);
        }
        catch (Exception exception)
        {
            error = $"JSON 解析失败：{exception.Message}";
            return false;
        }

        if (data == null)
        {
            error = "JSON 未包含有效的存档对象";
            return false;
        }

        if (data.schemaVersion < 0 ||
            data.schemaVersion > MPThreeDAssemblySaveDto.CurrentSchemaVersion)
        {
            error = $"不支持的存档版本 {data.schemaVersion}，当前版本 " +
                    MPThreeDAssemblySaveDto.CurrentSchemaVersion;
            data = null;
            return false;
        }

        // 兼容旧版本或字段缺失的数据，并保证模块拿到的集合始终可用。
        if (data.schemaVersion <= 0)
        {
            data.schemaVersion = MPThreeDAssemblySaveDto.CurrentSchemaVersion;
        }

        if (string.IsNullOrWhiteSpace(data.title))
        {
            data.title = DEFAULT_TITLE;
        }

        if (data.placedParts == null)
        {
            data.placedParts = new List<MPThreeDPlacedPartDto>();
        }
        else
        {
            if (data.placedParts.Count > MAX_PLACED_PARTS)
            {
                error = $"零件数量超过上限 {MAX_PLACED_PARTS}";
                data = null;
                return false;
            }

            HashSet<string> instanceIds = new HashSet<string>();
            int rootCount = 0;
            int nonRootCount = 0;
            for (int i = 0; i < data.placedParts.Count; i++)
            {
                MPThreeDPlacedPartDto part = data.placedParts[i];
                if (part == null ||
                    string.IsNullOrWhiteSpace(part.instanceId) ||
                    string.IsNullOrWhiteSpace(part.partId))
                {
                    error = $"第 {i} 个零件缺少 ID";
                    data = null;
                    return false;
                }

                if (!instanceIds.Add(part.instanceId))
                {
                    error = $"零件实例 ID 重复：{part.instanceId}";
                    data = null;
                    return false;
                }

                if (!MPThreeDPartCatalog.TryGet(part.partId, out _))
                {
                    error = $"未知零件类型：{part.partId}";
                    data = null;
                    return false;
                }

                if (!HasFinitePose(part))
                {
                    error = $"零件 {part.instanceId} 的位姿无效";
                    data = null;
                    return false;
                }

                bool isRoot = part.partId == MPThreeDPartCatalog.RootPartId;
                if ((part.instanceId == "root") != isRoot)
                {
                    error = $"零件 {part.instanceId} 非法占用底座实例 ID";
                    data = null;
                    return false;
                }

                if (isRoot)
                {
                    rootCount++;
                    if (rootCount > 1 || !string.IsNullOrEmpty(part.connectedToInstanceId))
                    {
                        error = "底座实例无效或重复";
                        data = null;
                        return false;
                    }
                }
                else if (++nonRootCount > MAX_NON_ROOT_PARTS)
                {
                    error = $"普通零件数量超过上限 {MAX_NON_ROOT_PARTS}";
                    data = null;
                    return false;
                }
            }

            for (int i = 0; i < data.placedParts.Count; i++)
            {
                MPThreeDPlacedPartDto part = data.placedParts[i];
                if (string.IsNullOrEmpty(part.connectedToInstanceId))
                {
                    continue;
                }

                if (part.connectedToInstanceId == part.instanceId ||
                    !instanceIds.Contains(part.connectedToInstanceId))
                {
                    error = $"零件 {part.instanceId} 的连接目标无效";
                    data = null;
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasFinitePose(MPThreeDPlacedPartDto part)
    {
        if (!IsFinite(part.positionX) || !IsFinite(part.positionY) ||
            !IsFinite(part.positionZ) || !IsFinite(part.rotationX) ||
            !IsFinite(part.rotationY) || !IsFinite(part.rotationZ) ||
            !IsFinite(part.rotationW))
        {
            return false;
        }

        float rotationMagnitude =
            part.rotationX * part.rotationX +
            part.rotationY * part.rotationY +
            part.rotationZ * part.rotationZ +
            part.rotationW * part.rotationW;
        return IsFinite(rotationMagnitude) && rotationMagnitude >= 0.01f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
