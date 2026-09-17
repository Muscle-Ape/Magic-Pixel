using System.Net;
using System.Security.Cryptography;
using System.Text;
using MagicPixelCustomLevelPublish;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

await ServerTests.Run();
await ModuleTests.Run();
await ClientTests.Run();

static class Check
{
    public static void That(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static async Task Fails(Func<Task> operation)
    {
        try { await operation(); }
        catch { return; }
        throw new Exception("Expected operation to fail.");
    }
}

static class ServerTests
{
    public static async Task Run()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var config = new AppleRevocationConfiguration
        {
            TeamId = "TEST_TEAM", KeyId = "TEST_KEY", ClientId = "com.yunqi.magicpixel",
            PrivateKeyPem = signingKey.ExportPkcs8PrivateKeyPem()
        };
        foreach (string scenario in new[] { "success", "wrong-subject", "wrong-audience", "wrong-issuer",
                     "expired", "future-iat", "bad-signature", "invalid-grant", "no-refresh-token", "revoke-failure", "keys-failure" })
        {
            using var handler = new AppleHandler(scenario, signingKey);
            using var http = new HttpClient(handler);
            var revoker = new AppleTokenRevoker(http);
            if (scenario == "success")
            {
                await revoker.RevokeAsync("fresh-code", "apple-owner", config);
                Check.That(handler.RevokeCount == 1, "Successful flow must revoke once.");
            }
            else
            {
                await Check.Fails(() => revoker.RevokeAsync("fresh-code", "apple-owner", config));
                Check.That(handler.RevokeCount == (scenario == "revoke-failure" ? 1 : 0),
                    "Invalid claims/code must never reach revocation: " + scenario);
            }
            Console.WriteLine("PASS server " + scenario);
        }
        using var noCalls = new AppleHandler("success", signingKey);
        using var noCallsHttp = new HttpClient(noCalls);
        var invalidRevoker = new AppleTokenRevoker(noCallsHttp);
        await Check.Fails(() => invalidRevoker.RevokeAsync("", "apple-owner", config));
        config.ClientId = "other.app";
        await Check.Fails(() => invalidRevoker.RevokeAsync("fresh-code", "apple-owner", config));
        Check.That(noCalls.RequestCount == 0, "Invalid input/config must fail before network.");
        Console.WriteLine("PASS server invalid configuration and empty code");
    }
}

sealed class AppleHandler(string scenario, ECDsa clientSigningKey) : HttpMessageHandler
{
    private readonly RSA m_appleKey = RSA.Create(2048);
    public int RevokeCount, RequestCount;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        RequestCount++;
        Check.That(request.RequestUri.Host == "appleid.apple.com", "Only Apple endpoints are allowed.");
        if (request.RequestUri.AbsolutePath == "/auth/keys")
        {
            var key = m_appleKey.ExportParameters(false);
            return Json(new { keys = new[] { new { kid = "apple-key", kty = "RSA", alg = "RS256", use = "sig",
                n = B64(key.Modulus), e = B64(key.Exponent) } } },
                scenario == "keys-failure" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        }
        var form = (await request.Content.ReadAsStringAsync(token)).Split('&').Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
        Check.That(form["client_id"] == "com.yunqi.magicpixel", "Client ID mismatch.");
        string[] secret = form["client_secret"].Split('.');
        Check.That(clientSigningKey.VerifyData(Encoding.ASCII.GetBytes(secret[0] + "." + secret[1]),
            Decode(secret[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation), "ES256 client secret signature.");
        var secretHeader = JObject.Parse(Encoding.UTF8.GetString(Decode(secret[0])));
        var secretClaims = JObject.Parse(Encoding.UTF8.GetString(Decode(secret[1])));
        Check.That((string)secretHeader["alg"] == "ES256" && (string)secretHeader["kid"] == "TEST_KEY" &&
            (string)secretClaims["iss"] == "TEST_TEAM" && (string)secretClaims["sub"] == "com.yunqi.magicpixel" &&
            (string)secretClaims["aud"] == "https://appleid.apple.com" &&
            (long)secretClaims["exp"] - (long)secretClaims["iat"] == 300, "Client secret claims.");

        if (request.RequestUri.AbsolutePath == "/auth/token")
        {
            Check.That(form["code"] == "fresh-code" && form["grant_type"] == "authorization_code", "Code exchange payload.");
            if (scenario == "invalid-grant") return Json(new { error = "invalid_grant" }, HttpStatusCode.BadRequest);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string header = B64(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"kid\":\"apple-key\"}"));
            string body = B64(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
            {
                iss = scenario == "wrong-issuer" ? "https://attacker.invalid" : "https://appleid.apple.com",
                aud = scenario == "wrong-audience" ? "other.app" : "com.yunqi.magicpixel",
                sub = scenario == "wrong-subject" ? "someone-else" : "apple-owner",
                exp = now + (scenario == "expired" ? -1 : 300), iat = now + (scenario == "future-iat" ? 120 : 0)
            })));
            byte[] signature = m_appleKey.SignData(Encoding.ASCII.GetBytes(header + "." + body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (scenario == "bad-signature") signature[0] ^= 1;
            return Json(new { id_token = header + "." + body + "." + B64(signature),
                refresh_token = scenario == "no-refresh-token" ? "" : "test-refresh" });
        }
        Check.That(request.RequestUri.AbsolutePath == "/auth/revoke", "Unexpected endpoint.");
        Check.That(form["token"] == "test-refresh" && form["token_type_hint"] == "refresh_token", "Must revoke refresh token, not identity token or code.");
        RevokeCount++;
        return new HttpResponseMessage(scenario == "revoke-failure" ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
    }

    private static HttpResponseMessage Json(object value, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(JsonConvert.SerializeObject(value)) };
    private static string B64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value)
    {
        string text = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(text.PadRight((text.Length + 3) / 4 * 4, '='));
    }
    protected override void Dispose(bool disposing) { if (disposing) m_appleKey.Dispose(); base.Dispose(disposing); }
}
