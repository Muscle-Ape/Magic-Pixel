using HQ.UIManager;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class MPLauncher : MonoBehaviour
{
    /// <summary>
    /// 是否已经进入游戏主流程，避免登录页回调和启动协程重复进入。
    /// </summary>
    private bool m_hasEnteredGame;

    private void Start()
    {
        StartCoroutine(LaunchAsync());
    }

    private IEnumerator LaunchAsync()
    {
        // 初始化资源管理器。
        MPLaunchYoo yoo = new MPLaunchYoo();
        yield return yoo.Initialize();

        // 初始化 UI 管理器。
        UIManager.Inst.Init();

        // 执行登录启动策略：优先恢复历史会话，真正首次安装时才自动匿名登录。
        yield return MPLoginManager.Instance.Initialize();

        MPLoginStartupResult startupResult = MPLoginManager.Instance.LastStartupResult;
        if (startupResult != null && startupResult.action != MPLoginStartupAction.EnterGame && !MPLoginManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning($"[MPLauncher] 登录启动流程需要用户处理：{startupResult.action}，{startupResult.message}");
            UIManager.Inst.ShowWindow<MPLoginView>(new MPLoginViewUIMsgData(startupResult, OnLoginViewSucceeded), true, UILayer.Top);
            yield break;
        }

        EnterGame();
    }

    /// <summary>
    /// 登录页登录成功后的回调。
    /// </summary>
    private void OnLoginViewSucceeded(MPLoginResult result)
    {
        EnterGame();
    }

    /// <summary>
    /// 进入游戏主流程。
    /// </summary>
    private void EnterGame()
    {
        if (m_hasEnteredGame)
        {
            return;
        }

        m_hasEnteredGame = true;
        InitializeAds();
        StartCoroutine(EnterGameRoutine());
    }

    /// <summary>
    /// 使用当前广告配置和登录玩家 ID 初始化广告模块。
    /// </summary>
    private void InitializeAds()
    {
        MPAdsManager.Instance.Initialize(
            userId: MPLoginManager.Instance.PlayerId);
    }

    /// <summary>
    /// 初始化本地用户数据并执行一次 Cloud Save 同步。
    /// </summary>
    private IEnumerator EnterGameRoutine()
    {
        MPDataManager.Instance.Initialize();
        MPUser.instance.Initialization();

        Task<bool> cloudSaveTask = MPCloudSaveManager.Instance.InitializeAfterUserLoadedAsync();
        while (!cloudSaveTask.IsCompleted)
        {
            yield return null;
        }

        if (cloudSaveTask.IsFaulted)
        {
            Debug.LogWarning($"[MPLauncher] Cloud Save initialize failed: {cloudSaveTask.Exception}");
        }

        UIManager.Inst.ShowWindow<MPHomeView>();
    }
}
