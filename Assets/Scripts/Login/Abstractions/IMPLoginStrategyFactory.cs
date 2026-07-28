/// <summary>
/// 登录策略工厂，根据登录方式返回对应 Strategy。
/// </summary>
public interface IMPLoginStrategyFactory
{
    /// <summary>
    /// 根据登录类型获取对应登录策略。
    /// </summary>
    IMPLoginStrategy GetStrategy(MPLoginType loginType);
}
