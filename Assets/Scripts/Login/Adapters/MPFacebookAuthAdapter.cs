/// <summary>
/// Facebook 登录适配器。当前要求外部传入 Facebook Access Token。
/// </summary>
public class MPFacebookAuthAdapter : MPProvidedTokenAuthAdapterBase
{
    /// <summary>
    /// 当前适配器支持 Facebook 登录。
    /// </summary>
    public override MPLoginType LoginType => MPLoginType.Facebook;

    protected override MPThirdPartyAuthResult CreateResult(MPThirdPartyLoginRequest request)
    {
        MPThirdPartyAuthResult invalid = RequireToken(request.accessToken, "Facebook 登录需要 Access Token。");
        return invalid ?? MPThirdPartyAuthResult.Success(accessToken: request.accessToken, platformUserId: request.platformUserId);
    }
}
