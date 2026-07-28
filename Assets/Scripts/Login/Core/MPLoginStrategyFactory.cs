using System;
using System.Collections.Generic;

/// <summary>
/// 登录策略工厂。将登录类型映射到对应 Strategy，实现开放扩展、集中注册。
/// </summary>
public class MPLoginStrategyFactory : IMPLoginStrategyFactory
{
    /// <summary>
    /// 登录类型到登录策略的映射表。
    /// </summary>
    private readonly Dictionary<MPLoginType, IMPLoginStrategy> m_strategies = new Dictionary<MPLoginType, IMPLoginStrategy>();

    public MPLoginStrategyFactory(IEnumerable<IMPLoginStrategy> strategies)
    {
        foreach (IMPLoginStrategy strategy in strategies)
        {
            m_strategies[strategy.LoginType] = strategy;
        }
    }

    /// <summary>
    /// 获取指定登录方式的策略；未注册时抛出不支持异常，由核心层统一转为登录错误。
    /// </summary>
    public IMPLoginStrategy GetStrategy(MPLoginType loginType)
    {
        if (m_strategies.TryGetValue(loginType, out IMPLoginStrategy strategy))
        {
            return strategy;
        }

        throw new NotSupportedException($"Login type is not supported: {loginType}");
    }
}
