using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HQ.UIManager;
using SuperScrollView;
using UnityEngine.UI;
using TMPro;

[Component("MPHomeView")]
public class MPHomeView : AWindow
{
    /// <summary>
    /// 无线滚动视图
    /// </summary>
    [TransformPath("View/Center/Levels")]
    private LoopGridView m_loopGrid;

    /// <summary>
    /// 设置按钮
    /// </summary>
    [TransformPath("View/Up/SettingBtn")]
    private Button m_settingBtn;

    /// <summary>
    /// 大图模式按钮
    /// </summary>
    [TransformPath("View/Down/Tab/LargeImage")]
    private Button m_largeImageBtn;

    /// <summary>
    /// 自定义模式按钮
    /// </summary>
    [TransformPath("View/Down/Tab/Custom")]
    private Button m_customBtn;

    /// <summary>
    /// 宠物功能按钮
    /// </summary>
    [TransformPath("View/Down/Tab/Pets")]
    private Button m_petsBtn;

    /// <summary>
    /// 3D
    /// </summary>
    [TransformPath("View/Down/Tab/ThreeD")]
    private Button m_threeDBtn;

    /// <summary>
    /// 金币数量
    /// </summary>
    [TransformPath("View/Up/Coin/Count")]
    private TMP_Text m_coinText;

    /// <summary>
    /// 钻石数量
    /// </summary>
    [TransformPath("View/Up/Diamond/Count")]
    private TMP_Text m_diamondText;

    /// <summary>
    /// 主关卡数据
    /// </summary>
    private MPMainLevelModel m_levelModel;

    /// <summary>
    /// Item未解锁下标框图片资源
    /// </summary>
    private Sprite m_itemIndexFrameLockSpriteAsset;
    /// <summary>
    /// Item已解锁下标框图片资源
    /// </summary>
    private Sprite m_itemIndexFrameUnLockSpriteAsset;
    /// <summary>
    /// Item已通关下标框图片资源
    /// </summary>
    private Sprite m_itemIndexFramePassSpriteAsset;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_levelModel = MPDataManager.Instance.m_mainLevelModel;

        m_itemIndexFrameLockSpriteAsset = null;
        m_itemIndexFrameUnLockSpriteAsset = null;
        m_itemIndexFramePassSpriteAsset = null;

        m_loopGrid.InitGridView(m_levelModel.blockInfos.Count, GetMainLevelByRowColumn);

        m_settingBtn.onClick.AddListener(OnSettingClick);
        m_largeImageBtn.onClick.AddListener(OnLargeImageClick);
        m_customBtn.onClick.AddListener(OnCustomClick);
        m_petsBtn.onClick.AddListener(OnPetsClick);
        m_threeDBtn.onClick.AddListener(OnThreeDClick);

        // 开始播放背景音乐
        MPAudioManager.Instance.PlayBGM(MPMusic.MPBGMMain);
    }

    public override void OnFocus(bool focus)
    {
        if (focus)
        {
            RefreshUI();
        }
    }

    private void RefreshUI()
    {
        m_coinText.text = MPUser.instance.GetCoins().ToString();
        m_diamondText.text = MPUser.instance.GetDiamond().ToString();
    }

    private LoopGridViewItem GetMainLevelByRowColumn(LoopGridView view, int index, int row, int column)
    {
        // 1、索引越界
        if (index < 0 || index >= m_levelModel.blockInfos.Count)
            return null;

        // 2、获取对应数据
        MPMainBlockInfo data = m_levelModel.blockInfos[index];

        // 3、从对象池中获取或者创建新对象
        LoopGridViewItem item = m_loopGrid.NewListViewItem("MPMainLevelItem");

        // 4、获取控制组件
        MPMainLevelItem level = item.GetComponent<MPMainLevelItem>();

        // 5、初始化并刷新
        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            level.Initialize(RefreshLevels, m_itemIndexFrameLockSpriteAsset, m_itemIndexFrameUnLockSpriteAsset, m_itemIndexFramePassSpriteAsset);
        }
        level.Refresh(data, index);

        return item;
    }

    private void RefreshLevels()
    {
        m_loopGrid.RefreshAllShownItem();
    }


    /// <summary>
    /// 设置按钮点击回调
    /// </summary>
    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }

    /// <summary>
    /// 大图模式点击回调
    /// </summary>
    private void OnLargeImageClick()
    {
        UIManager.Inst.ShowWindow<MPLargeImageLevelView>();
    }

    /// <summary>
    /// 自定义模式点击回调
    /// </summary>
    private void OnCustomClick()
    {
        UIManager.Inst.ShowWindow<MPCustomView>();
    }

    /// <summary>
    /// 宠物功能点击回调
    /// </summary>
    private void OnPetsClick()
    {
        UIManager.Inst.ShowWindow<MPPetsView>();
    }

    /// <summary>
    /// 3D功能点击回调
    /// </summary>
    private void OnThreeDClick()
    {

    }
}
