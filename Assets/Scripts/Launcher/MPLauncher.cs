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

        // 数据管理器初始化
        MPDataManager.Instance.Initialize();

        // 用户缓存数据初始化
        MPUser.instance.Initialization();


        MPMainBlockInfo blockInfo = MPDataManager.Instance.m_mainLevelModel.blockInfos[0];
        //UIManager.Inst.ShowWindow<MPGameView>(new UIMsgDataGeneric(blockInfo));
        UIManager.Inst.ShowWindow<MPHomeView>();
    }
}
