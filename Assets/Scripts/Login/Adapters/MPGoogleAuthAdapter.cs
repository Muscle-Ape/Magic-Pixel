/// <summary>
/// Google 登录适配器。当前要求外部传入 Google Identity Token。
/// </summary>
public class MPGoogleAuthAdapter : MPProvidedTokenAuthAdapterBase
{
    /// <summary>
    /// 当前适配器支持 Google 登录。
    /// </summary>
    public override MPLoginType LoginType => MPLoginType.Google;

    protected override MPThirdPartyAuthResult CreateResult(MPThirdPartyLoginRequest request)
    {
        MPThirdPartyAuthResult invalid = RequireToken(request.identityToken, "Google 登录需要 Identity Token。");
        return invalid ?? MPThirdPartyAuthResult.Success(identityToken: request.identityToken, platformUserId: request.platformUserId);
    }
}
