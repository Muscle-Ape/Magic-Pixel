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

        UIManager.Inst.ShowWindow<MPGameView>();
    }
}
