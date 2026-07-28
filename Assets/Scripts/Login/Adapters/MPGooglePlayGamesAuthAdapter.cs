/// <summary>
/// Google Play Games 登录适配器。当前要求外部传入 Auth Code。
/// </summary>
public class MPGooglePlayGamesAuthAdapter : MPProvidedTokenAuthAdapterBase
{
    /// <summary>
    /// 当前适配器支持 Google Play Games 登录。
    /// </summary>
    public override MPLoginType LoginType => MPLoginType.GooglePlayGames;

    protected override MPThirdPartyAuthResult CreateResult(MPThirdPartyLoginRequest request)
    {
        MPThirdPartyAuthResult invalid = RequireToken(request.authorizationCode, "Google Play Games 登录需要 Auth Code。");
        return invalid ?? MPThirdPartyAuthResult.Success(authorizationCode: request.authorizationCode, platformUserId: request.platformUserId);
    }
}
