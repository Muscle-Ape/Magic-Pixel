using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 基于 ES3 的安装状态服务。
/// 它只判断“是否有历史启动/登录线索”，不把单个 PlayerPrefs 标记作为唯一依据。
/// </summary>
public class MPEasySaveInstallationService : IMPInstallationService
{
    /// <summary>登录流程启动标记的 ES3 Key。</summary>
    private const string LOGIN_FLOW_STARTED_KEY = "MPLogin.LoginFlowStarted";

    /// <summary>安装实例 Id 的 ES3 Key。</summary>
    private const string INSTALLATION_ID_KEY = "MPLogin.InstallationId";

    /// <summary>本地登录资料 JSON 的 ES3 Key。</summary>
    private const string PROFILE_KEY = "MPLogin.LocalProfileJson";

    /// <summary>匿名账号 Id 的 ES3 Key。</summary>
    private const string ANONYMOUS_ID_KEY = "MPLogin.AnonymousId";

    /// <summary>历史 PlayerId 的 ES3 Key。</summary>
    private const string HISTORY_PLAYER_ID_KEY = "MPLogin.HistoryPlayerId";

    /// <summary>最近登录方式的 ES3 Key。</summary>
    private const string LAST_PROVIDER_KEY = "MPLogin.LastProvider";

    public Task<MPInstallationState> GetInstallationStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool hasAnyHistory =
            ES3.KeyExists(LOGIN_FLOW_STARTED_KEY) ||
            ES3.KeyExists(INSTALLATION_ID_KEY) ||
            ES3.KeyExists(PROFILE_KEY) ||
            ES3.KeyExists(ANONYMOUS_ID_KEY) ||
            ES3.KeyExists(HISTORY_PLAYER_ID_KEY) ||
            ES3.KeyExists(LAST_PROVIDER_KEY);

        return Task.FromResult(hasAnyHistory ? MPInstallationState.ExistingInstall : MPInstallationState.FirstInstall);
    }

    public Task MarkLoginFlowStartedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ES3.Save(LOGIN_FLOW_STARTED_KEY, true);
        return Task.CompletedTask;
    }
}
