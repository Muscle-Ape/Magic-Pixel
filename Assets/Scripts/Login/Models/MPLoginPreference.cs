using System;

/// <summary>
/// 登录页展示偏好。
/// 用于根据最近登录方式和绑定状态调整按钮顺序或文案。
/// </summary>
[Serializable]
public sealed class MPLoginPreference
{
    /// <summary>最近一次使用的登录提供方。</summary>
    public MPLoginProvider lastLoginProvider;

    /// <summary>是否已有 Google 绑定。</summary>
    public bool hasGoogleBinding;

    /// <summary>是否已有 Apple 绑定。</summary>
    public bool hasAppleBinding;

    /// <summary>是否已有账号密码绑定。</summary>
    public bool hasUsernamePasswordBinding;

    /// <summary>
    /// 从本地登录资料生成 UI 偏好。
    /// </summary>
    public static MPLoginPreference FromProfile(MPLocalLoginProfile profile)
    {
        if (profile == null)
        {
            return new MPLoginPreference();
        }

        return new MPLoginPreference
        {
            lastLoginProvider = profile.lastLoginProvider,
            hasGoogleBinding = profile.hasGoogleBinding || profile.hasGooglePlayGamesBinding,
            hasAppleBinding = profile.hasAppleBinding,
            hasUsernamePasswordBinding = profile.hasUsernamePasswordBinding
        };
    }
}
