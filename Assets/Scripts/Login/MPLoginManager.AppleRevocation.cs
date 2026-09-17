using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

public partial class MPLoginManager
{
    /// <summary>
    /// 优先使用服务端凭证；缺失或读取/撤销失败时，仅在本次删除中补一次 Apple 原生授权。
    /// 是否绑定 Apple 由服务端查询认证身份，不能依赖可能过期的本地 hasAppleBinding。
    /// </summary>
    private async Task RevokeAppleAuthorizationForDeletionAsync(string expectedPlayerId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        bool revoked;
        try
        {
            revoked = await CloudCodeService.Instance.CallModuleEndpointAsync<bool>(
                MPCustomLevelPublishConstants.MODULE_NAME, "TryRevokeStoredAppleAuthorization");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch
        {
            // 不输出可能包含凭证的服务异常。失败只进入验证流程，绝不视为撤销成功。
            revoked = false;
        }
        EnsureDeletionAccount(expectedPlayerId, token);
        if (revoked) return;

        // 不依赖本地绑定缓存。确认调用者的 Apple 身份后，才能拉起补授权；此接口不读取保存凭证。
        string subject = await CloudCodeService.Instance.CallModuleEndpointAsync<string>(
            MPCustomLevelPublishConstants.MODULE_NAME, "GetAppleAccountDeletionIdentity");
        EnsureDeletionAccount(expectedPlayerId, token);
        if (subject == null) throw new InvalidOperationException("Could not verify the account's Apple binding.");
        if (subject.Length == 0) return;

        MPThirdPartyAuthResult result = null;
        try
        {
            result = await new MPAppleAuthAdapter().AuthorizeAsync(new MPThirdPartyLoginRequest
            {
                loginType = MPLoginType.Apple, provider = MPLoginType.Apple
            }, token);
            EnsureDeletionAccount(expectedPlayerId, token);
            if (result?.errorCode == MPLoginErrorCodes.UserCancelled)
                throw new OperationCanceledException("Apple authorization cancelled.", token);
            if (result?.success != true || string.IsNullOrWhiteSpace(result.authorizationCode))
                throw new InvalidOperationException("Apple authorization failed. Please retry.");
            if (!string.Equals(subject, result.platformUserId, StringComparison.Ordinal))
                throw new InvalidOperationException("Authorize the Apple account linked to this game account.");

            // 只获取新的授权码，不调用 Unity 登录/绑定，不切换游客或已登录账号。
            revoked = await CloudCodeService.Instance.CallModuleEndpointAsync<bool>(
                MPCustomLevelPublishConstants.MODULE_NAME, "RevokeAppleAuthorization",
                new Dictionary<string, object> { { "authorizationCode", result.authorizationCode } });
            EnsureDeletionAccount(expectedPlayerId, token);
            if (!revoked) throw new InvalidOperationException("Apple authorization could not be revoked. Please retry.");
        }
        finally
        {
            if (result != null)
            {
                result.authorizationCode = null;
                result.identityToken = null;
            }
        }
    }

    private void EnsureDeletionAccount(string expectedPlayerId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsLoggedIn || PlayerId != expectedPlayerId)
            throw new InvalidOperationException("The signed-in account changed.");
    }
}
