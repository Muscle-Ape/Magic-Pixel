using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MagicPixelCustomLevelPublish;

/// <summary>仅由 Cloud Code 从 Secret Manager 读取，绝不能打包进 Unity 客户端。</summary>
public sealed class AppleRevocationConfiguration
{
    public string TeamId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string PrivateKeyPem { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TeamId) || string.IsNullOrWhiteSpace(KeyId) ||
            ClientId != "com.yunqi.magicpixel" || string.IsNullOrWhiteSpace(PrivateKeyPem))
            throw new InvalidOperationException("Invalid Apple revocation configuration.");
    }
}

/// <summary>Apple 原生授权码 → 服务端换取令牌 → 校验绑定身份 → 撤销 refresh_token。</summary>
public sealed class AppleTokenRevoker
{
    private const string AppleIssuer = "https://appleid.apple.com";
    private readonly HttpClient m_http;

    public AppleTokenRevoker(HttpClient http) => m_http = http;

    public async Task RevokeAsync(string authorizationCode, string expectedSubject,
        AppleRevocationConfiguration configuration)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        string refreshToken = await ExchangeAsync(authorizationCode, expectedSubject, configuration, deadline.Token);
        await RevokeRefreshTokenAsync(refreshToken, configuration, deadline.Token);
    }

    /// <summary>仅服务端使用；先验签和核对绑定身份，再允许持久化 refresh_token。</summary>
    public async Task<string> ExchangeAsync(string authorizationCode, string expectedSubject,
        AppleRevocationConfiguration configuration, CancellationToken cancellationToken = default)
    {
        configuration.Validate();
        if (string.IsNullOrWhiteSpace(authorizationCode) || authorizationCode.Length > 4096 ||
            string.IsNullOrWhiteSpace(expectedSubject))
            throw new InvalidOperationException("Invalid Apple authorization request.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        string clientSecret = CreateClientSecret(configuration);
        // 先获取公钥，避免公钥服务故障时消耗一次性的授权码。
        using var keysResponse = await m_http.GetAsync(AppleIssuer + "/auth/keys", deadline.Token);
        if (!keysResponse.IsSuccessStatusCode)
            throw new InvalidOperationException("Apple signing keys are unavailable.");
        var keys = JObject.Parse(await keysResponse.Content.ReadAsStringAsync(deadline.Token));

        using var tokenBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = configuration.ClientId,
            ["client_secret"] = clientSecret,
            ["code"] = authorizationCode,
            ["grant_type"] = "authorization_code"
        });
        using var tokenResponse = await m_http.PostAsync(AppleIssuer + "/auth/token", tokenBody, deadline.Token);
        if (!tokenResponse.IsSuccessStatusCode)
            throw new InvalidOperationException("Apple authorization code exchange failed. A fresh authorization is required.");

        var tokens = JObject.Parse(await tokenResponse.Content.ReadAsStringAsync(deadline.Token));
        string? identityToken = tokens.Value<string>("id_token");
        string? refreshToken = tokens.Value<string>("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Apple returned no revocable token.");
        ValidateIdentityToken(identityToken, expectedSubject, configuration.ClientId, keys);

        return refreshToken;
    }

    /// <summary>已有服务端凭证直接撤销，不再请求原生 Apple 授权。</summary>
    public async Task RevokeRefreshTokenAsync(string refreshToken, AppleRevocationConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        configuration.Validate();
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Missing Apple refresh token.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var revokeBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = configuration.ClientId,
            ["client_secret"] = CreateClientSecret(configuration),
            ["token"] = refreshToken,
            ["token_type_hint"] = "refresh_token"
        });
        using var revokeResponse = await m_http.PostAsync(AppleIssuer + "/auth/revoke", revokeBody, deadline.Token);
        // Apple 成功返回 200，空响应体是正常情况；不能把任意 4xx 当作已撤销。
        if (revokeResponse.StatusCode != System.Net.HttpStatusCode.OK)
            throw new InvalidOperationException("Apple token revocation failed.");
    }

    private static string CreateClientSecret(AppleRevocationConfiguration configuration)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string header = Encode(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
        {
            alg = "ES256", kid = configuration.KeyId, typ = "JWT"
        })));
        string payload = Encode(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
        {
            iss = configuration.TeamId, iat = now, exp = now + 300,
            aud = AppleIssuer, sub = configuration.ClientId
        })));
        string message = header + "." + payload;
        using var key = ECDsa.Create();
        key.ImportFromPem(configuration.PrivateKeyPem);
        if (key.KeySize != 256) throw new InvalidOperationException("Apple requires an ES256 signing key.");
        byte[] signature = key.SignData(Encoding.ASCII.GetBytes(message), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return message + "." + Encode(signature);
    }

    private static void ValidateIdentityToken(string? token, string subject, string clientId, JObject jwks)
    {
        string[] parts = token?.Split('.') ?? Array.Empty<string>();
        if (parts.Length != 3) throw new InvalidOperationException("Invalid Apple identity token.");
        var header = JObject.Parse(Encoding.UTF8.GetString(Decode(parts[0])));
        var claims = JObject.Parse(Encoding.UTF8.GetString(Decode(parts[1])));
        string? kid = header.Value<string>("kid");
        if (header.Value<string>("alg") != "RS256" || string.IsNullOrWhiteSpace(kid))
            throw new InvalidOperationException("Unsupported Apple token signature.");
        var key = (jwks["keys"] as JArray)?.OfType<JObject>().SingleOrDefault(k =>
            k.Value<string>("kid") == kid && k.Value<string>("kty") == "RSA" &&
            k.Value<string>("alg") == "RS256" && k.Value<string>("use") == "sig");
        if (key == null) throw new InvalidOperationException("Apple signing key was not found.");
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Decode(key.Value<string>("n")!), Exponent = Decode(key.Value<string>("e")!)
        });
        if (!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new InvalidOperationException("Invalid Apple token signature.");

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (claims.Value<string>("iss") != AppleIssuer || claims.Value<string>("aud") != clientId ||
            claims.Value<string>("sub") != subject || (claims.Value<long?>("exp") ?? 0) <= now ||
            !claims.ContainsKey("iat") || claims.Value<long>("iat") > now + 60)
            throw new InvalidOperationException("Apple authorization does not match this game account.");
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }
}
