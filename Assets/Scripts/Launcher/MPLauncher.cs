using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using HQ.UIManager;
using UnityEngine;

public class MPLauncher : MonoBehaviour
{
    // 启动预制体直接随场景依赖加载，不能等 YooAsset 初始化之后再加载首屏。
    [SerializeField] private GameObject m_loadingViewPrefab;
    [SerializeField] private RectTransform m_loadingViewParent;
    private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
    private MPLoadingView m_loadingView;
    private bool m_resourcesReady;
    private bool m_uiReady;
    private bool m_dataReady;
    private bool m_userReady;
    private bool m_isLaunching;
    private bool m_hasEnteredGame;

    private void Start()
    {
        if (m_loadingViewPrefab == null || m_loadingViewParent == null)
        {
            Debug.LogError("[MPLauncher] 请绑定 MPLoadingView 预制体及其 UI 父节点。");
            return;
        }
        GameObject loadingObject = Instantiate(m_loadingViewPrefab, m_loadingViewParent);
        loadingObject.SetActive(true);
        m_loadingView = loadingObject.GetComponent<MPLoadingView>();
        if (m_loadingView == null)
            m_loadingView = loadingObject.AddComponent<MPLoadingView>();
        m_loadingView.ConfigureStartup(RetryStartup, OnLoginSucceeded);
        RetryStartup();
    }

    private void RetryStartup()
    {
        if (m_isLaunching || m_hasEnteredGame || m_loadingView == null || m_loadingView.IsDestoried)
            return;
        m_isLaunching = true;
        m_loadingView.BeginLoading();
        StartCoroutine(RunStartupSafely());
    }

    /// <summary>
    /// 登录页登录成功后的回调。
    /// </summary>
    private void OnLoginSucceeded(MPLoginResult result)
    {
        if (result != null && result.isSuccess)
            RetryStartup();
    }

    private IEnumerator RunStartupSafely()
    {
        // 保证至少绘制一帧，再开始同步配置加载或 SDK 初始化。
        yield return null;
        IEnumerator routine = LaunchRoutine();
        try
        {
            while (!m_lifetime.IsCancellationRequested && m_loadingView != null && !m_loadingView.IsDestoried)
            {
                object step;
                try
                {
                    if (!routine.MoveNext())
                        break;
                    step = routine.Current;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[MPLauncher] 启动初始化失败：{exception.GetType().Name}");
                    if (m_loadingView != null && !m_loadingView.IsDestoried)
                        m_loadingView.ShowInitializationFailure("Initialization failed.");
                    break;
                }
                yield return step;
            }
        }
        finally
        {
            (routine as IDisposable)?.Dispose();
            m_isLaunching = false;
        }
    }

    private IEnumerator LaunchRoutine()
    {
        if (!m_resourcesReady)
        {
            MPLaunchYoo yoo = new MPLaunchYoo();
            // 直接推进资源协程，让同步异常也能被 RunStartupSafely 捕获。
            IEnumerator initialize = yoo.Initialize();
            while (initialize.MoveNext())
                yield return initialize.Current;
            if (!yoo.IsInitialized)
            {
                m_loadingView.ShowInitializationFailure("Could not load game resources.");
                yield break;
            }
            m_resourcesReady = true;
        }

        if (!m_uiReady)
        {
            UIManager.Inst.Init();
            // 接管启动时新创建的实例，UIManager 不再重复实例化。
            UIManager.Inst.HistoryList.Add(m_loadingView);
            m_loadingView.GetFocus();
            m_uiReady = true;
        }

        if (!MPLoginManager.Instance.IsLoggedIn)
        {
            Task<MPLoginStartupResult> login = MPLoginManager.Instance.StartLoginFlowAsync(m_lifetime.Token);
            while (!login.IsCompleted)
                yield return null;
            if (login.IsFaulted || login.IsCanceled)
            {
                Debug.LogWarning($"[MPLauncher] 自动登录未完成：{login.Exception?.GetType().Name ?? "Cancelled"}");
                m_loadingView.ShowLogin(MPLoginStartupResult.Failed(MPLoginError.Create(
                    MPLoginErrorCodes.Unknown, "Automatic sign-in failed. Please choose a login method or retry.")));
                yield break;
            }
            MPLoginStartupResult result = login.GetAwaiter().GetResult();
            if (result?.action != MPLoginStartupAction.EnterGame || !MPLoginManager.Instance.IsLoggedIn)
            {
                m_loadingView.ShowLogin(result);
                yield break;
            }
        }

        // 广告预热仍为非阻塞服务：广告 SDK 未回调不能阻止玩家进入游戏。
        if (!MPAdsManager.Instance.IsConfigured)
            MPAdsManager.Instance.Initialize(userId: MPLoginManager.Instance.PlayerId);

        yield return null;
        if (!m_dataReady)
        {
            MPDataManager.Instance.Initialize();
            m_dataReady = true;
        }
        if (!m_userReady)
        {
            MPUser.instance.Initialization();
            m_userReady = true;
        }

        Task<bool> sync = MPCloudSaveManager.Instance.InitializeAfterUserLoadedAsync(m_lifetime.Token);
        while (!sync.IsCompleted)
            yield return null;
        if (!sync.GetAwaiter().GetResult())
        {
            m_loadingView.ShowInitializationFailure("Could not sync your data.");
            yield break;
        }

        MPVibrationManager.Instance.Initialize();
        m_loadingView.CompleteLoading(EnterGame);
    }

    private void EnterGame()
    {
        if (m_hasEnteredGame || m_lifetime.IsCancellationRequested)
            return;
        // 先创建主页，失败时保留加载页的重试入口，不留下空白首屏。
        try
        {
            MPHomeView home = UIManager.Inst.ShowWindow<MPHomeView>();
            if (home == null)
                throw new InvalidOperationException("MPHomeView could not be loaded.");
            m_hasEnteredGame = true;
            m_loadingView.DestroyWindow();
            m_loadingView = null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPLauncher] 打开主页失败：{exception.GetType().Name}");
            if (m_loadingView != null && !m_loadingView.IsDestoried)
            {
                m_loadingView.GetFocus();
                m_loadingView.ShowInitializationFailure("Could not open Home.");
            }
        }
    }

    private void OnDestroy()
    {
        m_lifetime.Cancel();
        StopAllCoroutines();
        m_lifetime.Dispose();
    }
}
