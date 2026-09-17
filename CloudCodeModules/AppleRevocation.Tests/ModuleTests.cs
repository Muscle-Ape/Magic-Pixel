using System.Reflection;
using System.Security.Cryptography;
using MagicPixelCustomLevelPublish;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

static class ModuleTests
{
    public static async Task Run()
    {
        using var signing = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var handler = new AppleHandler("success", signing);
        using var http = new HttpClient(handler);
        var module = (CustomLevelPublishModule)Activator.CreateInstance(typeof(CustomLevelPublishModule),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { NullLogger<CustomLevelPublishModule>.Instance, http }, null);
        var fake = new CredentialApi(signing);
        IGameApiClient api = fake.Create();
        IExecutionContext a = fake.Context("A"), b = fake.Context("B"), guest = fake.Context("Guest");

        Check.That(await module.TryRevokeStoredAppleAuthorization(guest, api), "Guest deletion needs no credential.");
        Check.That(!await module.TryRevokeStoredAppleAuthorization(a, api), "Missing credential reported explicitly.");
        Check.That(handler.RequestCount == 0, "Missing/guest paths never call Apple.");
        Console.WriteLine("PASS module guest and missing credential");

        await module.StoreAppleAuthorization(a, api, "fresh-code");
        Check.That(fake.Records.Count == 1 && fake.Records.ContainsKey("A") &&
            (string)fake.Record("A")["RefreshToken"] == "test-refresh", "Store only in authenticated player's Private data.");
        Check.That(handler.RevokeCount == 0, "Login must not revoke authorization.");
        int calls = handler.RequestCount;
        await module.StoreAppleAuthorization(a, api, "fresh-code");
        Check.That(handler.RequestCount == calls, "Duplicate successful code doesn't exchange again.");
        Check.That(!await module.TryRevokeStoredAppleAuthorization(b, api), "B cannot use A's token.");
        Console.WriteLine("PASS module private per-player storage and idempotent code exchange");

        await Check.Fails(() => module.StoreAppleAuthorization(b, api, "fresh-code"));
        Check.That(!fake.Records.ContainsKey("B"), "Token with A's signed Apple subject cannot be saved for B.");
        Console.WriteLine("PASS module subject mismatch rejected before persistence");

        Check.That(await module.TryRevokeStoredAppleAuthorization(a, api), "Saved token revokes.");
        Check.That(handler.RevokeCount == 1 && (bool)fake.Record("A")["Revoked"] &&
            (string)fake.Record("A")["RefreshToken"] == "", "Revocation removes token, stores receipt.");
        await module.TryRevokeStoredAppleAuthorization(a, api);
        Check.That(handler.RevokeCount == 1, "Retry with receipt doesn't contact Apple.");
        Console.WriteLine("PASS module revoke saved token and retry without Apple authorization");

        // 新的一次登录可替换撤销标记；伪造的 codeHash 仅用于模拟不同的真实登录请求。
        var record = fake.Record("A"); record["CodeHash"] = "previous-code";
        fake.Put("A", record);
        await module.StoreAppleAuthorization(a, api, "fresh-code");
        Check.That(!(bool)fake.Record("A")["Revoked"], "New login resets receipt with a validated token.");
        fake.FailWrite = true;
        await Check.Fails(() => module.TryRevokeStoredAppleAuthorization(a, api));
        Check.That(!(bool)fake.Record("A")["Revoked"], "Write conflict cannot mark a new credential revoked.");
        fake.FailWrite = false;
        Console.WriteLine("PASS module optimistic write failure stops deletion");

        fake.FailRead = true;
        try { await module.TryRevokeStoredAppleAuthorization(a, api); throw new Exception("accepted storage failure"); }
        catch (InvalidOperationException error)
        { Check.That(!error.ToString().Contains("private-test-value") && error.InnerException == null, "No token-bearing inner exception exposed."); }
        fake.FailRead = false;
        Console.WriteLine("PASS module storage failure redacts underlying exception");

        fake.Records["B"] = fake.Records["A"];
        await Check.Fails(() => module.DeleteAccountCommunityData(a, api));
        Check.That(fake.Records.ContainsKey("A"), "Cleanup cannot remove a new unrevoked credential.");
        await module.TryRevokeStoredAppleAuthorization(a, api);
        Check.That(await module.DeleteAccountCommunityData(a, api) == "", "Cleanup reaches completion.");
        Check.That(!fake.Records.ContainsKey("A") && fake.Records.ContainsKey("B"), "Deletion removes only caller's private credential.");
        Console.WriteLine("PASS module account cleanup removes private credential, preserves other player");

        fake.Put("A", JObject.Parse("{\"RefreshToken\":{\"invalid\":true}}"));
        await Check.Fails(() => module.TryRevokeStoredAppleAuthorization(a, api));
        Check.That(await module.GetAppleAccountDeletionIdentity(a, api) == "apple-owner", "Fallback identity lookup independent of credential data.");
        Check.That(await module.RevokeAppleAuthorization(a, api, "fresh-code"), "Fresh code fallback can replace unreadable stored token.");
        Check.That((bool)fake.Record("A")["Revoked"] && (string)fake.Record("A")["RefreshToken"] == "", "Fallback writes no token receipt.");
        Console.WriteLine("PASS module fresh authorization bypasses malformed stored credential");

        int beforeInvalid = handler.RevokeCount;
        await Check.Fails(() => module.RevokeAppleAuthorization(b, api, "fresh-code"));
        Check.That(handler.RevokeCount == beforeInvalid, "Fallback rejects Apple identity belonging to a different player.");
        fake.FailRead = true;
        await Check.Fails(() => module.RevokeAppleAuthorization(a, api, "fresh-code"));
        Check.That(handler.RevokeCount == beforeInvalid, "Persistent storage outage does not pretend deletion succeeded.");
        fake.FailRead = false;
        Console.WriteLine("PASS module fallback identity and persistent service failure guards");
    }
}

// 使用实际 SDK 接口的代理，只替换外部 API/HTTP，不重新实现被测的服务端逻辑。
public class ApiProxy : DispatchProxy
{
    public Func<MethodInfo, object[], object> InvokeCall;
    protected override object Invoke(MethodInfo method, object[] args) => InvokeCall(method, args);
    public static object Make(Type type, Func<MethodInfo, object[], object> callback)
    { var proxy = (ApiProxy)Create(type, typeof(ApiProxy)); proxy.InvokeCall = callback; return proxy; }
    public static T Make<T>(Func<MethodInfo, object[], object> callback) where T : class => (T)Make(typeof(T), callback);
}

sealed class CredentialApi(ECDsa key)
{
    public readonly Dictionary<string, (string Json, string Lock)> Records = new();
    public bool FailRead, FailWrite;
    public JObject Record(string player) => JObject.Parse(Records[player].Json);
    public void Put(string player, JObject value) => Records[player] = (value.ToString(Formatting.None), Guid.NewGuid().ToString());
    public IExecutionContext Context(string player) => ApiProxy.Make<IExecutionContext>((method, _) => method.Name switch
    {
        "get_PlayerId" => player, "get_ProjectId" => "project", "get_ServiceToken" => "server-only",
        "get_AccessToken" => "user-access", "get_EnvironmentId" => "environment", _ => null
    });
    public IGameApiClient Create() => ApiProxy.Make<IGameApiClient>((method, _) => method.Name switch
    {
        "get_CloudSaveData" => ApiProxy.Make(method.ReturnType, Data),
        "get_PlayerAuth" => ApiProxy.Make(method.ReturnType, Auth),
        "get_SecretManager" => ApiProxy.Make(method.ReturnType, Secret),
        _ => throw new Exception("Unexpected API: " + method.Name)
    });
    private object Secret(MethodInfo method, object[] args)
    {
        Check.That((string)args[1] == "MP_APPLE_SIGN_IN_CONFIG", "Use existing server secret.");
        return Task.FromResult(new Secret(JsonConvert.SerializeObject(new AppleRevocationConfiguration
        { TeamId = "TEST_TEAM", KeyId = "TEST_KEY", ClientId = "com.yunqi.magicpixel", PrivateKeyPem = key.ExportPkcs8PrivateKeyPem() })));
    }
    private object Auth(MethodInfo method, object[] args)
    {
        var context = (IExecutionContext)args[0];
        Check.That((string)args[1] == "user-access" && (string)args[2] == context.PlayerId, "Read only caller's auth identity.");
        return Response(method, new { id = context.PlayerId, disabled = false,
            externalIds = context.PlayerId == "Guest" ? Array.Empty<object>() : new object[] {
                new { providerId = "apple.com", externalId = context.PlayerId == "A" ? "apple-owner" : "apple-B" } } });
    }
    private object Data(MethodInfo method, object[] args)
    {
        var context = (IExecutionContext)args[0];
        Check.That((string)args[1] == "server-only" && (string)args[2] == "project", "Private data requires server token.");
        if (method.Name == "GetPrivateCustomKeysAsync") return Response(method, new { results = Array.Empty<object>() });
        string player = (string)args[3];
        Check.That(player == context.PlayerId, "Never trust client-selected player.");
        switch (method.Name)
        {
            case "GetPrivateItemsAsync":
                if (FailRead) throw new Exception("private-test-value");
                Check.That(((List<string>)args[4]).SequenceEqual(new[] { "mp_apple_revocation_credential_v1" }), "Read exact credential key.");
                return Response(method, new { results = Records.TryGetValue(player, out var value) ? new object[] {
                    new { key = "mp_apple_revocation_credential_v1", value = JObject.Parse(value.Json), writeLock = value.Lock } } : Array.Empty<object>() });
            case "SetPrivateItemAsync":
                if (FailWrite) throw new Exception("private-test-value");
                var body = (SetItemBody)args[4];
                Check.That(body.Key == "mp_apple_revocation_credential_v1", "Write exact key.");
                if (Records.TryGetValue(player, out var previous)) Check.That(body.WriteLock == previous.Lock, "Existing writes use optimistic lock.");
                Records[player] = (JsonConvert.SerializeObject(body.Value), Guid.NewGuid().ToString());
                return Response(method, new { writeLock = Records[player].Lock });
            case "DeletePrivateItemAsync":
                Check.That((string)args[4] == "mp_apple_revocation_credential_v1" && (string)args[5] == Records[player].Lock, "Cleanup uses key and write lock.");
                Records.Remove(player); return Response(method, null);
            default: throw new Exception("Unexpected data method: " + method.Name);
        }
    }
    private static object Response(MethodInfo method, object data)
    {
        Type resultType = method.ReturnType.GetGenericArguments()[0];
        object response = Activator.CreateInstance(resultType);
        PropertyInfo property = resultType.GetProperty("Data");
        if (property != null) property.SetValue(response, JsonConvert.DeserializeObject(JsonConvert.SerializeObject(data), property.PropertyType,
            new JsonSerializerSettings { ContractResolver = new PartialResponseContract() }));
        return typeof(Task).GetMethod(nameof(Task.FromResult)).MakeGenericMethod(resultType).Invoke(null, new[] { response });
    }
}

// API 替身只填本次逻辑需要的字段，省略 SDK 要求的无关时间戳等字段。
sealed class PartialResponseContract : Newtonsoft.Json.Serialization.DefaultContractResolver
{
    protected override Newtonsoft.Json.Serialization.JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
    {
        var property = base.CreateProperty(member, serialization);
        property.Required = Required.Default;
        return property;
    }
}
