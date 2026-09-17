using System.Net.Http;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace MagicPixelCustomLevelPublish;

public partial class CustomLevelPublishModule
{
    private const string AppleSecretName = "MP_APPLE_SIGN_IN_CONFIG";
    private const string AppleCredentialKey = "mp_apple_revocation_credential_v1";
    private static readonly HttpClient AppleHttpClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private readonly HttpClient m_appleHttpClient = AppleHttpClient;

    // 测试使用独立的 HTTP 边界；线上仍由现有公共构造函数使用共享 HttpClient。
    internal CustomLevelPublishModule(Microsoft.Extensions.Logging.ILogger<CustomLevelPublishModule> logger, HttpClient appleHttpClient)
        : this(logger) => m_appleHttpClient = appleHttpClient;

    /// <summary>
    /// 在登录/绑定成功后调用。只接受 code，玩家和 Apple 身份均从已认证调用者取得。
    /// Player Data 的 Private 访问类别只能由服务器读写，不参与玩家普通存档同步/资产覆盖。
    /// </summary>
    [CloudCodeFunction("StoreAppleAuthorization")]
    public async Task<bool> StoreAppleAuthorization(
        IExecutionContext context, IGameApiClient gameApiClient, string authorizationCode)
    {
        EnsureSignedIn(context);
        try
        {
            if (string.IsNullOrWhiteSpace(authorizationCode) || authorizationCode.Length > 4096)
                throw new InvalidOperationException();
            string subject = await GetLinkedAppleSubjectAsync(context, gameApiClient);
            if (string.IsNullOrEmpty(subject)) throw new InvalidOperationException();
            var existing = await ReadAppleCredentialAsync(context, gameApiClient);
            string codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorizationCode)));
            // 响应丢失重试时，不再向 Apple 消耗同一个一次性授权码。
            if (existing.Record?.Subject == subject && existing.Record.CodeHash == codeHash)
                return !existing.Record.Revoked && !string.IsNullOrEmpty(existing.Record.RefreshToken);
            var configuration = await GetAppleRevocationConfigurationAsync(context, gameApiClient);
            string refreshToken = await new AppleTokenRevoker(m_appleHttpClient)
                .ExchangeAsync(authorizationCode, subject, configuration);
            // 换码期间可能在另一台设备解绑/切换绑定，写入之前再次核对服务端身份。
            if (await GetLinkedAppleSubjectAsync(context, gameApiClient) != subject)
                throw new InvalidOperationException();
            await WriteAppleCredentialAsync(context, gameApiClient, new AppleStoredCredential
            {
                Subject = subject, ClientId = configuration.ClientId,
                RefreshToken = refreshToken, CodeHash = codeHash
            }, existing.WriteLock);
            return true;
        }
        catch
        {
            // 不携带底层异常，防止 Cloud Save 请求正文/Apple token 出现在 Cloud Code 日志。
            throw new InvalidOperationException("APPLE_CREDENTIAL_SAVE_FAILED");
        }
    }

    /// <summary>优先使用保存凭证。缺失返回 false；异常抛错，由客户端进入一次补授权兜底。</summary>
    [CloudCodeFunction("TryRevokeStoredAppleAuthorization")]
    public async Task<bool> TryRevokeStoredAppleAuthorization(IExecutionContext context, IGameApiClient gameApiClient)
    {
        EnsureSignedIn(context);
        try
        {
            string subject = await GetLinkedAppleSubjectAsync(context, gameApiClient);
            if (string.IsNullOrEmpty(subject)) return true;
            var existing = await ReadAppleCredentialAsync(context, gameApiClient);
            var record = existing.Record;
            if (record == null || record.Subject != subject || record.ClientId != "com.yunqi.magicpixel") return false;
            // 仅保留不含 token 的完成标记，后续删除数据失败重试不再要求 Apple 扫脸。
            if (record.Revoked) return true;
            if (string.IsNullOrWhiteSpace(record.RefreshToken)) return false;
            var configuration = await GetAppleRevocationConfigurationAsync(context, gameApiClient);
            await new AppleTokenRevoker(m_appleHttpClient).RevokeRefreshTokenAsync(record.RefreshToken, configuration);
            record.RefreshToken = string.Empty;
            record.Revoked = true;
            // WriteLock 防止并发新登录的凭证被旧撤销请求覆盖。冲突时终止删除，让用户重试。
            await WriteAppleCredentialAsync(context, gameApiClient, record, existing.WriteLock);
            return true;
        }
        catch
        {
            throw new InvalidOperationException("APPLE_STORED_REVOCATION_FAILED: Retry account deletion.");
        }
    }

    /// <summary>补授权前校验调用者绑定；不读取 Cloud Save 中保存的凭证。</summary>
    [CloudCodeFunction("GetAppleAccountDeletionIdentity")]
    public async Task<string> GetAppleAccountDeletionIdentity(IExecutionContext context, IGameApiClient api)
    {
        EnsureSignedIn(context);
        return await GetLinkedAppleSubjectAsync(context, api);
    }

    /// <summary>
    /// 兜底：使用新 code 换取并撤销令牌，不使用保存记录中的 token。
    /// 原始记录只取 WriteLock，损坏/缺失的旧值不影响新授权的身份校验。
    /// </summary>
    [CloudCodeFunction("RevokeAppleAuthorization")]
    public async Task<bool> RevokeAppleAuthorization(IExecutionContext context, IGameApiClient api, string authorizationCode)
    {
        EnsureSignedIn(context);
        try
        {
            string subject = await GetLinkedAppleSubjectAsync(context, api);
            if (string.IsNullOrWhiteSpace(subject)) throw new InvalidOperationException();
            var existing = await ReadAppleCredentialItemAsync(context, api);
            var configuration = await GetAppleRevocationConfigurationAsync(context, api);
            await new AppleTokenRevoker(m_appleHttpClient).RevokeAsync(authorizationCode, subject, configuration);
            if (await GetLinkedAppleSubjectAsync(context, api) != subject)
                throw new InvalidOperationException();
            // 保留无 token 的完成标记，后续数据清理仍有重试保护；写入失败不假装删除成功。
            await WriteAppleCredentialAsync(context, api, new AppleStoredCredential
            {
                Subject = subject, ClientId = configuration.ClientId, Revoked = true,
                CodeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorizationCode)))
            }, existing.WriteLock);
            return true;
        }
        catch
        {
            throw new InvalidOperationException("APPLE_FRESH_REVOCATION_FAILED: Retry account deletion.");
        }
    }

    private static async Task<(AppleStoredCredential? Record, string? WriteLock)> ReadAppleCredentialAsync(
        IExecutionContext context, IGameApiClient api)
    {
        var item = await ReadAppleCredentialItemAsync(context, api);
        return (DeserializeItemValue<AppleStoredCredential>(item.Value), item.WriteLock);
    }

    private static async Task<(object? Value, string? WriteLock)> ReadAppleCredentialItemAsync(
        IExecutionContext context, IGameApiClient api)
    {
        try
        {
            var response = await api.CloudSaveData.GetPrivateItemsAsync(context, context.ServiceToken,
                context.ProjectId!, context.PlayerId!, new List<string> { AppleCredentialKey });
            var item = response.Data.Results.SingleOrDefault(value => value.Key == AppleCredentialKey);
            return (item?.Value, item?.WriteLock);
        }
        catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, null);
        }
    }

    private static async Task WriteAppleCredentialAsync(IExecutionContext context, IGameApiClient api,
        AppleStoredCredential record, string? writeLock)
    {
        var body = string.IsNullOrEmpty(writeLock)
            ? new SetItemBody(AppleCredentialKey, record)
            : new SetItemBody(AppleCredentialKey, record, writeLock);
        await api.CloudSaveData.SetPrivateItemAsync(context, context.ServiceToken, context.ProjectId!, context.PlayerId!, body);
    }

    /// <summary>在账号数据清理阶段移除凭证/撤销标记；普通登出不清理。</summary>
    private static async Task DeleteAppleCredentialAsync(IExecutionContext context, IGameApiClient api)
    {
        try
        {
            var existing = await ReadAppleCredentialAsync(context, api);
            if (existing.Record == null) return;
            // 撤销以后若另一台设备刚登录并保存了新凭证，不能直接把尚未撤销的新 token 清掉。
            if (!existing.Record.Revoked && !string.IsNullOrEmpty(await GetLinkedAppleSubjectAsync(context, api)))
                throw new InvalidOperationException("Apple authorization changed. Retry deletion.");
            await api.CloudSaveData.DeletePrivateItemAsync(context, context.ServiceToken, context.ProjectId!,
                context.PlayerId!, AppleCredentialKey, existing.WriteLock!, CancellationToken.None);
        }
        catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound) { }
        catch { throw new InvalidOperationException("APPLE_CREDENTIAL_CLEANUP_FAILED"); }
    }

    private sealed class AppleStoredCredential
    {
        public string Subject { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string CodeHash { get; set; } = string.Empty;
        public bool Revoked { get; set; }
    }

    private static async Task<string> GetLinkedAppleSubjectAsync(IExecutionContext context, IGameApiClient api)
    {
        // 使用调用者 AccessToken，而非客户端提供的 playerId / Apple UserId。
        var response = await api.PlayerAuth.GetPlayerAsync(context, context.AccessToken,
            context.PlayerId!, context.ProjectId!);
        if (response.Data == null || response.Data.Id != context.PlayerId || response.Data.Disabled)
            throw new InvalidOperationException("Could not verify the signed-in player.");
        return response.Data.ExternalIds?.SingleOrDefault(identity => identity.ProviderId == "apple.com")
            ?.VarExternalId ?? string.Empty;
    }

    private static async Task<AppleRevocationConfiguration> GetAppleRevocationConfigurationAsync(
        IExecutionContext context, IGameApiClient api)
    {
        try
        {
            var secret = await api.SecretManager.GetSecret(context, AppleSecretName);
            var configuration = JsonConvert.DeserializeObject<AppleRevocationConfiguration>(secret.Value);
            if (configuration == null) throw new InvalidOperationException();
            configuration.Validate();
            return configuration;
        }
        catch
        {
            throw new InvalidOperationException("Apple account deletion is not configured on the server.");
        }
    }
}
