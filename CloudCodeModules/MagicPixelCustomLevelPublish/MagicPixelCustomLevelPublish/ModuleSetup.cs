using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;

namespace MagicPixelCustomLevelPublish;

/// <summary>
/// Cloud Code C# Module 依赖配置。
/// AddGameApiClient 会注入可访问 Cloud Save 等 UGS 服务的 IGameApiClient。
/// </summary>
public class ModuleSetup : ICloudCodeSetup
{
    /// <inheritdoc />
    public void Setup(ICloudCodeConfig config)
    {
        config.AddGameApiClient();
    }
}
