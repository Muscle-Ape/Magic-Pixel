using Unity.Services.CloudCode;

static class ClientTests
{
    public static async Task Run()
    {
        var manager = Setup();
        await manager.DeleteCurrentAccountAsync("A");
        Check.That(Events.SequenceEqual(new[] { "stored", "data", "identity", "local" }), "Stored revocation must precede destructive operations.");
        Check.That(manager.Repo.Guest.playerId == "B", "Independent guest must be preserved.");
        Check.That(MPAppleAuthAdapter.Calls == 0, "Stored credential must not prompt Apple.");
        Console.WriteLine("PASS client stored credential deletion without Apple prompt");

        manager = Setup();
        CloudCodeService.Instance.Revoked = false;
        await manager.DeleteCurrentAccountAsync("A");
        Check.That(Events.SequenceEqual(new[] { "stored", "identity-lookup", "authorize", "fresh", "data", "identity", "local" }) &&
            MPAppleAuthAdapter.Calls == 1, "Missing credential must authorize and revoke exactly once before data deletion.");
        Check.That(MPAppleAuthAdapter.Last.authorizationCode == null && MPAppleAuthAdapter.Last.identityToken == null, "Clear temporary credentials.");
        Console.WriteLine("PASS client missing credential uses fresh Apple authorization");

        manager = Setup();
        CloudCodeService.Instance.Fail = true;
        await manager.DeleteCurrentAccountAsync("A");
        Check.That(manager.Auth.Deletes == 1 && MPAppleAuthAdapter.Calls == 1, "Stored read failure must fallback once.");
        Console.WriteLine("PASS client stored request failure falls back successfully");

        foreach (string mode in new[] { "cancel", "wrong-user", "no-code", "native-failure", "fresh-failure", "fresh-false", "null-identity" })
        {
            manager = Setup(); CloudCodeService.Instance.Revoked = false;
            CloudCodeService.Instance.Mode = mode; MPAppleAuthAdapter.Mode = mode;
            await Check.Fails(() => manager.DeleteCurrentAccountAsync("A"));
            Check.That(manager.Auth.Deletes == 0 && MPCloudSaveManager.Instance.Remote == 0 &&
                !manager.IsDeletingAccount && MPCloudSaveManager.Instance.Held == 0 && MPAppleAuthAdapter.Calls <= 1,
                "Failed fallback must preserve data, release guards and never loop: " + mode);
            Console.WriteLine("PASS client fallback " + mode);
        }

        manager = Setup(); CloudCodeService.Instance.Fail = true; CloudCodeService.Instance.AppleId = "";
        await manager.DeleteCurrentAccountAsync("A");
        Check.That(MPAppleAuthAdapter.Calls == 0, "Guest must not get an Apple prompt after transient failure.");
        Console.WriteLine("PASS client guest fallback lookup skips Apple prompt");

        manager = Setup();
        CloudCodeService.Instance.Fail = true;
        CloudCodeService.Instance.IdentityFail = true;
        await Check.Fails(() => manager.DeleteCurrentAccountAsync("A"));
        Check.That(manager.Auth.Deletes == 0 && MPCloudSaveManager.Instance.Remote == 0 &&
            MPCloudSaveManager.Instance.Held == 0, "Network failure must preserve data and release guards.");
        Console.WriteLine("PASS client stored revocation failure preserves data");

        manager = Setup();
        using (var cancellation = new CancellationTokenSource())
        {
            CloudCodeService.Instance.OnRevoke = cancellation.Cancel;
            await Check.Fails(() => manager.DeleteCurrentAccountAsync("A", cancellation.Token));
            Check.That(manager.Auth.Deletes == 0 && MPCloudSaveManager.Instance.Remote == 0, "Cancellation stops deletion after request.");
        }
        Console.WriteLine("PASS client UI cancellation after revocation");

        manager = Setup();
        CloudCodeService.Instance.OnRevoke = () => manager.PlayerId = "B";
        await Check.Fails(() => manager.DeleteCurrentAccountAsync("A"));
        Check.That(manager.Auth.Deletes == 0, "Account change must prevent deletion.");
        Console.WriteLine("PASS client changed account is rejected");

        manager = Setup();
        MPCloudSaveManager.Instance.FailRemote = true;
        await Check.Fails(() => manager.DeleteCurrentAccountAsync("A"));
        MPCloudSaveManager.Instance.FailRemote = false;
        await manager.DeleteCurrentAccountAsync("A");
        Check.That(manager.Auth.Deletes == 1 && Events.Count(x => x == "stored") == 2, "Retry uses stored revocation again.");
        Console.WriteLine("PASS client retry uses server credential only");

        manager = Setup();
        MPCloudSaveManager.Instance.FailLocal = true;
        await Check.Fails(() => manager.DeleteCurrentAccountAsync("A"));
        MPCloudSaveManager.Instance.FailLocal = false;
        await manager.DeleteCurrentAccountAsync("A");
        Check.That(manager.Auth.Deletes == 1 && Events.Count(x => x == "stored") == 1, "Local retry skips completed remote steps.");
        Console.WriteLine("PASS client local cleanup retry");

        manager = Setup();
        var pending = new TaskCompletionSource<bool>();
        CloudCodeService.Instance.Pending = pending.Task;
        Task first = manager.DeleteCurrentAccountAsync("A");
        await Check.Fails(() => manager.DeleteCurrentAccountAsync("A"));
        pending.SetResult(true);
        await first;
        Check.That(manager.Auth.Deletes == 1, "Concurrent deletion rejected.");
        Console.WriteLine("PASS client concurrent deletion blocked");
    }

    public static readonly List<string> Events = new();
    private static MPLoginManager Setup()
    {
        Events.Clear(); MPAppleAuthAdapter.Calls = 0; MPAppleAuthAdapter.Mode = "";
        MPCloudSaveManager.Instance = new(); MPCustomLevelPublishManager.Instance = new();
        CloudCodeService.Instance = new(); return new MPLoginManager();
    }
}

public enum MPLoginType { Apple }
public class MPThirdPartyLoginRequest { public MPLoginType loginType, provider; }
public static class MPLoginErrorCodes { public const string UserCancelled = "cancelled"; }
public class MPAppleAuthAdapter
{
    public static int Calls;
    public static string Mode;
    public static MPThirdPartyAuthResult Last;
    public Task<MPThirdPartyAuthResult> AuthorizeAsync(MPThirdPartyLoginRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Calls++; ClientTests.Events.Add("authorize");
        Last = Mode == "cancel" ? MPThirdPartyAuthResult.Failed(MPLoginErrorCodes.UserCancelled, "") :
            Mode == "native-failure" ? MPThirdPartyAuthResult.Failed("unsupported", "") :
            MPThirdPartyAuthResult.Success(authorizationCode: Mode == "no-code" ? "" : "fresh-code",
                identityToken: "must-not-be-sent", platformUserId: Mode == "wrong-user" ? "other" : "apple-owner");
        return Task.FromResult(Last);
    }
}

public static class MPCustomLevelPublishConstants { public const string MODULE_NAME = "MagicPixelCustomLevelPublish"; }
public class MPCustomLevelPublishManager
{
    public static MPCustomLevelPublishManager Instance = new();
    public bool HasPendingAccountWrites;
}
public class MPLocalLoginProfile { public string playerId = "B", unityProfile = "guest"; }
public class FakeRepository
{
    public MPLocalLoginProfile Guest = new();
    public Task<MPLocalLoginProfile> LoadGuestProfileAsync() => Task.FromResult(Guest);
    public Task RemoveDeletedAccountAsync(string id) { if (Guest?.playerId == id) Guest = null; return Task.CompletedTask; }
}
public class FakeAuthentication(MPLoginManager manager)
{
    public int Deletes;
    public Task DeleteAccountAsync(CancellationToken token)
    { token.ThrowIfCancellationRequested(); Deletes++; ClientTests.Events.Add("identity"); manager.IsLoggedIn = false; manager.PlayerId = ""; return Task.CompletedTask; }
    public bool SwitchProfile(string profile) => true;
    public bool ClearSessionToken() => true;
}
public partial class MPLoginManager
{
    public bool IsLoggedIn = true, IsLoginFlowRunning;
    public string PlayerId = "A";
    private readonly FakeAuthentication m_inner;
    private readonly FakeRepository m_localLoginRepository = new();
    public FakeAuthentication Auth => m_inner;
    public FakeRepository Repo => m_localLoginRepository;
    public MPLoginManager() { m_inner = new(this); }
}
public class MPCloudSaveManager
{
    public static MPCloudSaveManager Instance = new();
    public int Remote, Held;
    public bool FailRemote, FailLocal;
    public Task<IDisposable> BeginAccountDeletionAsync(CancellationToken token)
    { token.ThrowIfCancellationRequested(); Held++; return Task.FromResult<IDisposable>(new Guard(this)); }
    public Task DeleteRemoteAccountDataAsync(string player, CancellationToken token)
    { token.ThrowIfCancellationRequested(); Remote++; ClientTests.Events.Add("data"); if (FailRemote) throw new Exception(); return Task.CompletedTask; }
    public void ClearDeletedAccountLocalData(string player)
    { if (FailLocal) throw new Exception(); ClientTests.Events.Add("local"); }
    private sealed class Guard(MPCloudSaveManager owner) : IDisposable { public void Dispose() => owner.Held--; }
}
namespace Unity.Services.CloudCode
{
    public class CloudCodeService
    {
        public static CloudCodeService Instance = new();
        public bool Revoked = true, Fail, IdentityFail;
        public string AppleId = "apple-owner", Mode;
        public Action OnRevoke;
        public Task<bool> Pending;
        public async Task<T> CallModuleEndpointAsync<T>(string module, string method, Dictionary<string, object> args = null)
        {
            if (method == "GetAppleAccountDeletionIdentity")
            {
                Check.That(args == null, "Identity lookup cannot target arbitrary player.");
                ClientTests.Events.Add("identity-lookup");
                if (IdentityFail) throw new Exception();
                return (T)(object)(Mode == "null-identity" ? null : AppleId);
            }
            if (method == "RevokeAppleAuthorization")
            {
                ClientTests.Events.Add("fresh");
                Check.That(args.Count == 1 && (string)args["authorizationCode"] == "fresh-code", "Send only new code, never identity/refresh token.");
                if (Mode == "fresh-failure") throw new Exception();
                return (T)(object)(Mode != "fresh-false");
            }
            Check.That(method == "TryRevokeStoredAppleAuthorization" && args == null, "Deletion sends no credentials or player ID.");
            ClientTests.Events.Add("stored"); OnRevoke?.Invoke();
            if (Fail) throw new Exception();
            return (T)(object)(Pending == null ? Revoked : await Pending);
        }
    }
}
