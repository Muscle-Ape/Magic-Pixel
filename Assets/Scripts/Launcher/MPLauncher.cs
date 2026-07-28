using HQ.UIManager;
using System.Collections;
using UnityEngine;

public class MPLauncher : MonoBehaviour
{
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

        // 默认使用游客模式登录。
        yield return MPLoginManager.Instance.Initialize();
        if (!MPLoginManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning($"[MPLauncher] 游客登录失败，将以本地数据继续启动。{MPLoginManager.Instance.LastError}");
        }

        // 数据管理器初始化
        MPDataManager.Instance.Initialize();

        // 用户缓存数据初始化
        MPUser.instance.Initialization();


        MPMainBlockInfo blockInfo = MPDataManager.Instance.m_mainLevelModel.blockInfos[0];
        //UIManager.Inst.ShowWindow<MPGameView>(new UIMsgDataGeneric(blockInfo));
        UIManager.Inst.ShowWindow<MPHomeView>();
    }
}
