using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

/// <summary>
/// 基于 Unity Cloud Code SDK 的公开自定义关卡访问实现。
/// 客户端只调用 Cloud Code，不直接写公开 Cloud Save 数据。
/// </summary>
public class MPUnityCloudCodeCustomLevelPublishApi : IMPCustomLevelPublishApi
{
    /// <inheritdoc />
    public async Task<MPCustomLevelPublishResult> PublishAsync(MPCustomLevelInfo levelInfo, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, object> args = new Dictionary<string, object>
        {
            { "levelInfo", BuildLevelInfoPayload(levelInfo) },
            { "displayName", MPLoginManager.Instance.PlayerName },
            { "clientVersion", Application.version }
        };

        MPCustomLevelPublishResult result = await CloudCodeService.Instance.CallModuleEndpointAsync<MPCustomLevelPublishResult>(
            MPCustomLevelPublishConstants.MODULE_NAME,
            MPCustomLevelPublishConstants.PUBLISH_FUNCTION,
            args);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <inheritdoc />
    public async Task<MPCustomLevelListResult> GetListAsync(string sortType, int pageSize, string cursor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, object> args = new Dictionary<string, object>
        {
            { "sortType", string.IsNullOrEmpty(sortType) ? MPCustomLevelPublishConstants.SORT_LATEST : sortType },
            { "pageSize", Mathf.Clamp(pageSize, 1, MPCustomLevelPublishConstants.MAX_PAGE_SIZE) },
            { "cursor", string.IsNullOrEmpty(cursor) ? string.Empty : cursor }
        };

        MPCustomLevelListResult result = await CloudCodeService.Instance.CallModuleEndpointAsync<MPCustomLevelListResult>(
            MPCustomLevelPublishConstants.MODULE_NAME,
            MPCustomLevelPublishConstants.GET_LIST_FUNCTION,
            args);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <inheritdoc />
    public async Task<MPCustomLevelPublicRecord> GetDetailAsync(string publicLevelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, object> args = BuildPublicLevelIdArgs(publicLevelId);
        MPCustomLevelPublicRecord result = await CloudCodeService.Instance.CallModuleEndpointAsync<MPCustomLevelPublicRecord>(
            MPCustomLevelPublishConstants.MODULE_NAME,
            MPCustomLevelPublishConstants.GET_DETAIL_FUNCTION,
            args);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <inheritdoc />
    public async Task<MPCustomLevelStatsResult> GetStatsAsync(
        IReadOnlyList<string> publicLevelIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<string> ids = new List<string>();
        if (publicLevelIds != null)
        {
            int count = Mathf.Min(
                publicLevelIds.Count,
                MPCustomLevelPublishConstants.MAX_STATS_BATCH_SIZE);
            for (int i = 0; i < count; i++)
            {
                if (!string.IsNullOrEmpty(publicLevelIds[i]))
                    ids.Add(publicLevelIds[i]);
            }
        }

        Dictionary<string, object> args = new Dictionary<string, object>
        {
            { "publicLevelIds", ids }
        };
        MPCustomLevelStatsResult result =
            await CloudCodeService.Instance.CallModuleEndpointAsync<MPCustomLevelStatsResult>(
                MPCustomLevelPublishConstants.MODULE_NAME,
                MPCustomLevelPublishConstants.GET_STATS_FUNCTION,
                args);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <inheritdoc />
    public async Task<MPCustomLevelPublicRecord> PlayAsync(string publicLevelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, object> args = BuildPublicLevelIdArgs(publicLevelId);
        MPCustomLevelPublicRecord result = await CloudCodeService.Instance.CallModuleEndpointAsync<MPCustomLevelPublicRecord>(
            MPCustomLevelPublishConstants.MODULE_NAME,
            MPCustomLevelPublishConstants.PLAY_FUNCTION,
            args);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <inheritdoc />
    public async Task<MPCustomLevelLikeResult> LikeAsync(
        string publicLevelId,
        bool liked,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, object> args = BuildPublicLevelIdArgs(publicLevelId);
        args["liked"] = liked;
        MPCustomLevelLikeResult result = await CloudCodeService.Instance.CallModuleEndpointAsync<MPCustomLevelLikeResult>(
            MPCustomLevelPublishConstants.MODULE_NAME,
            MPCustomLevelPublishConstants.LIKE_FUNCTION,
            args);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <inheritdoc />
    public async Task<MPCustomLevelRevokeResult> RevokeAsync(string publicLevelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, object> args = BuildPublicLevelIdArgs(publicLevelId);
        MPCustomLevelRevokeResult result = await CloudCodeService.Instance.CallModuleEndpointAsync<MPCustomLevelRevokeResult>(
            MPCustomLevelPublishConstants.MODULE_NAME,
            MPCustomLevelPublishConstants.REVOKE_FUNCTION,
            args);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>
    /// 将项目内的自定义关卡对象转换为 Cloud Code 更容易接收的纯 JSON 字典。
    /// </summary>
    private static Dictionary<string, object> BuildLevelInfoPayload(MPCustomLevelInfo levelInfo)
    {
        Dictionary<string, object> payload = new Dictionary<string, object>
        {
            { "sourceLocalLevelId", levelInfo == null ? string.Empty : levelInfo.ID },
            { "title", levelInfo == null ? string.Empty : levelInfo.Title },
            { "size", levelInfo == null ? 0 : levelInfo.Size },
            { "block", BuildBlockPayload(levelInfo == null ? null : levelInfo.Block) },
            { "colors", BuildColorPayload(levelInfo == null ? null : levelInfo.Colors) }
        };
        return payload;
    }

    /// <summary>
    /// 构建公开关卡ID参数。
    /// </summary>
    private static Dictionary<string, object> BuildPublicLevelIdArgs(string publicLevelId)
    {
        return new Dictionary<string, object>
        {
            { "publicLevelId", string.IsNullOrEmpty(publicLevelId) ? string.Empty : publicLevelId }
        };
    }

    /// <summary>
    /// 构建填充格索引 JSON 数组。
    /// </summary>
    private static List<object> BuildBlockPayload(List<int> block)
    {
        List<object> result = new List<object>();
        if (block == null)
        {
            return result;
        }

        for (int i = 0; i < block.Count; i++)
        {
            result.Add(block[i]);
        }

        return result;
    }

    /// <summary>
    /// 构建颜色配置 JSON 数组。
    /// </summary>
    private static List<object> BuildColorPayload(List<MPCustomLevelColorInfo> colors)
    {
        List<object> result = new List<object>();
        if (colors == null)
        {
            return result;
        }

        for (int i = 0; i < colors.Count; i++)
        {
            MPCustomLevelColorInfo colorInfo = colors[i];
            if (colorInfo == null)
            {
                continue;
            }

            result.Add(new Dictionary<string, object>
            {
                { "index", colorInfo.Index },
                { "color", colorInfo.Color }
            });
        }

        return result;
    }
}
