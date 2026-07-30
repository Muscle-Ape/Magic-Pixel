using HQ.UIManager;
using System.Collections;
using UnityEngine;

public class MPLauncher : MonoBehaviour
{
    /// <summary>
    /// 是否已经进入游戏主流程，避免登录页回调和启动协程重复进入。
    /// </summary>
    private bool m_hasEnteredGame;

    void Start()
    {
        StartCoroutine(LaunchAsync());
    }


    private IEnumerator LaunchAsync()
    {
        // 初始化资源管理器
        MPLaunchYoo yoo = new MPLaunchYoo();
        yield return yoo.Initialize();

        // 初始化UI管理器
        UIManager.Inst.Init();

        // 执行登录启动策略：优先恢复历史会话，真正首次安装时才自动匿名登录。
        yield return MPLoginManager.Instance.Initialize();

        MPLoginStartupResult startupResult = MPLoginManager.Instance.LastStartupResult;
        //Debug.LogError((startupResult != null) + " " + (startupResult.action != MPLoginStartupAction.EnterGame) + " " + (!MPLoginManager.Instance.IsLoggedIn));
        if (startupResult != null && startupResult.action != MPLoginStartupAction.EnterGame && !MPLoginManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning($"[MPLauncher] 登录启动流程需要用户处理：{startupResult.action}，{startupResult.message}");
            UIManager.Inst.ShowWindow<MPLoginView>(new MPLoginViewUIMsgData(startupResult, OnLoginViewSucceeded), true, UILayer.Top);
            yield break;
        }

        EnterGame();
    }

    /// <summary>
    /// 临时登录页登录成功后的回调。
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

        // 数据管理器初始化
        MPDataManager.Instance.Initialize();

        // 用户缓存数据初始化
        MPUser.instance.Initialization();

        //UIManager.Inst.ShowWindow<MPGameView>(new UIMsgDataGeneric(blockInfo));
        UIManager.Inst.ShowWindow<MPHomeView>();
    }
}
