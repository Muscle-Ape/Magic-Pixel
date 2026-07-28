/// <summary>
/// Apple 登录适配器。当前要求外部传入 Apple Identity Token。
/// </summary>
public class MPAppleAuthAdapter : MPProvidedTokenAuthAdapterBase
{
    /// <summary>
    /// 当前适配器支持 Apple 登录。
    /// </summary>
    public override MPLoginType LoginType => MPLoginType.Apple;

    protected override MPThirdPartyAuthResult CreateResult(MPThirdPartyLoginRequest request)
    {
        MPThirdPartyAuthResult invalid = RequireToken(request.identityToken, "Apple 登录需要 Identity Token。");
        return invalid ?? MPThirdPartyAuthResult.Success(identityToken: request.identityToken, authorizationCode: request.authorizationCode, platformUserId: request.platformUserId);
    }
}
