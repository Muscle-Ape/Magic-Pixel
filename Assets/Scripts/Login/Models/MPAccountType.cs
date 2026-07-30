/// <summary>
/// 登录策略中使用的账号类型。
/// 它描述的是游戏账号状态，不等同于某一种登录方式。
/// </summary>
public enum MPAccountType
{
    /// <summary>客户端暂时无法判断账号状态。</summary>
    Unknown = 0,
    /// <summary>当前账号只有游客/匿名身份。</summary>
    Anonymous = 1,
    /// <summary>当前账号至少绑定了账号密码、Google、Apple 或其他正式身份。</summary>
    Bound = 2,
    /// <summary>本地有历史资料，但服务端或网络暂时无法确认。</summary>
    Temporary = 3
}
