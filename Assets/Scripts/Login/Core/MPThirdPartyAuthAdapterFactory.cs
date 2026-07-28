using System;
using System.Collections.Generic;

/// <summary>
/// 第三方授权适配器工厂。负责把登录类型映射到具体平台 Adapter。
/// </summary>
public class MPThirdPartyAuthAdapterFactory : IMPThirdPartyAuthAdapterFactory
{
    /// <summary>
    /// 登录类型到第三方授权适配器的映射表。
    /// </summary>
    private readonly Dictionary<MPLoginType, IMPThirdPartyAuthAdapter> m_adapters = new Dictionary<MPLoginType, IMPThirdPartyAuthAdapter>();

    public MPThirdPartyAuthAdapterFactory(IEnumerable<IMPThirdPartyAuthAdapter> adapters)
    {
        foreach (IMPThirdPartyAuthAdapter adapter in adapters)
        {
            m_adapters[adapter.LoginType] = adapter;
        }
    }

    /// <summary>
    /// 获取指定第三方平台的授权适配器。
    /// </summary>
    public IMPThirdPartyAuthAdapter GetAdapter(MPLoginType loginType)
    {
        if (m_adapters.TryGetValue(loginType, out IMPThirdPartyAuthAdapter adapter))
        {
            return adapter;
        }

        throw new NotSupportedException($"Third party adapter is not supported: {loginType}");
    }
}
