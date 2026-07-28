/// <summary>
/// 第三方授权适配器工厂，根据平台类型返回对应 Adapter。
/// </summary>
public interface IMPThirdPartyAuthAdapterFactory
{
    /// <summary>
    /// 根据登录类型获取对应第三方授权适配器。
    /// </summary>
    IMPThirdPartyAuthAdapter GetAdapter(MPLoginType loginType);
}
