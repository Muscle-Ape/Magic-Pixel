using UnityEngine;

/// <summary>
/// 登录模块运行配置。
/// 可以在 Resources 下创建名为 MPLoginConfiguration 的配置资产覆盖默认值。
/// </summary>
[CreateAssetMenu(menuName = "Game/Authentication/LoginConfig")]
public sealed class MPLoginConfiguration : ScriptableObject
{
    /// <summary>
    /// Resources 中可选配置资产的加载路径。
    /// </summary>
    private const string RESOURCE_PATH = "MPLoginConfiguration";

    /// <summary>
    /// 真正首次安装时是否自动创建游客账号。
    /// </summary>
    [Header("首次登录")]
    public bool AutoAnonymousOnFirstInstall = true;

    /// <summary>
    /// 是否允许游客/匿名登录。
    /// </summary>
    [Header("登录方式")]
    public bool EnableAnonymousLogin = true;

    /// <summary>
    /// 是否在 UI 和策略中开放通用 Google Identity Token 登录入口。
    /// 当前项目未安装通用 Google Sign-In SDK，默认关闭；Android 游戏登录使用下方的 Google Play Games。
    /// </summary>
    public bool EnableGoogleLogin = false;

    /// <summary>
    /// 是否在 UI 和策略中开放 Google Play Games 登录入口。
    /// Android 包体通常使用它获取 Auth Code，再交给 Unity Authentication。
    /// </summary>
    public bool EnableGooglePlayGamesLogin = true;

    /// <summary>
    /// 是否在 UI 和策略中开放 Apple 登录入口。
    /// </summary>
    public bool EnableAppleLogin = true;

    /// <summary>
    /// 是否在 UI 和策略中开放 Facebook 登录入口。
    /// </summary>
    public bool EnableFacebookLogin = true;

    /// <summary>
    /// 是否在 UI 和策略中开放账号密码登录入口。
    /// </summary>
    public bool EnableUsernamePasswordLogin = true;

    /// <summary>
    /// 启动时是否尝试恢复 Unity Authentication 本地 Session。
    /// </summary>
    [Header("自动恢复")]
    public bool EnableSessionRestore = true;

    /// <summary>
    /// 是否允许登录模块刷新当前认证状态。
    /// 当前 Unity Authentication 会由 SDK 维护 Token，这里保留给后续游戏服务器 RefreshToken。
    /// </summary>
    public bool EnableTokenRefresh = true;

    /// <summary>
    /// 匿名 Session 彻底失效时是否先进入匿名恢复流程。
    /// 当前项目尚未接入服务端匿名账号恢复接口，默认关闭，避免每次本地凭证缺失时误弹恢复页。
    /// </summary>
    public bool EnableAnonymousRecovery = false;

    /// <summary>
    /// 完成新手引导后是否提示绑定 Apple 或 Google。
    /// </summary>
    [Header("绑定提示")]
    public bool ShowBindAfterTutorial = true;

    /// <summary>
    /// 推荐再次提示绑定账号的玩家等级。
    /// </summary>
    public int BindPromptLevel = 3;

    /// <summary>
    /// 首次付费等高价值操作前是否提示游客绑定账号。
    /// </summary>
    public bool PromptBeforeFirstPurchase = true;

    /// <summary>
    /// 网络错误时是否保留本地登录资料并进入重试状态。
    /// </summary>
    [Header("错误策略")]
    public bool PreserveSessionOnNetworkError = true;

    /// <summary>
    /// 匿名恢复失败后是否允许用户手动创建新的游客账号。
    /// </summary>
    public bool AllowCreateNewGuestAfterRecoveryFailure = true;

    /// <summary>
    /// 加载项目配置；没有配置资产时创建运行时默认配置，避免登录模块依赖资源先存在。
    /// </summary>
    public static MPLoginConfiguration LoadOrCreateDefault()
    {
        MPLoginConfiguration configuration = Resources.Load<MPLoginConfiguration>(RESOURCE_PATH);
        return configuration != null ? configuration : CreateInstance<MPLoginConfiguration>();
    }
}
