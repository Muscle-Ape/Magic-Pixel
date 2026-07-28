/// <summary>
/// 第三方平台授权结果。
/// Adapter 负责把各平台 SDK 的返回值统一转换成这个结构。
/// </summary>
public class MPThirdPartyAuthResult
{
    /// <summary>第三方授权是否成功。</summary>
    public bool success;
    /// <summary>授权码，例如 Google Play Games Auth Code。</summary>
    public string authorizationCode;
    /// <summary>访问令牌，例如 Facebook Access Token。</summary>
    public string accessToken;
    /// <summary>身份令牌，例如 Google/Apple Identity Token。</summary>
    public string identityToken;
    /// <summary>第三方平台用户 Id，当前仅透传。</summary>
    public string platformUserId;
    /// <summary>授权失败时的错误码。</summary>
    public string errorCode;
    /// <summary>授权失败时的错误消息。</summary>
    public string errorMessage;

    /// <summary>
    /// 创建成功的第三方授权结果。
    /// </summary>
    public static MPThirdPartyAuthResult Success(string authorizationCode = null, string accessToken = null, string identityToken = null, string platformUserId = null)
    {
        return new MPThirdPartyAuthResult
        {
            success = true,
            authorizationCode = authorizationCode,
            accessToken = accessToken,
            identityToken = identityToken,
            platformUserId = platformUserId,
            errorCode = string.Empty,
            errorMessage = string.Empty
        };
    }

    /// <summary>
    /// 创建失败的第三方授权结果。
    /// </summary>
    public static MPThirdPartyAuthResult Failed(string errorCode, string errorMessage)
    {
        return new MPThirdPartyAuthResult
        {
            success = false,
            errorCode = errorCode,
            errorMessage = errorMessage
        };
    }
}
