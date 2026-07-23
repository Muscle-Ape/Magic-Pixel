using HQ.UIManager;
using SuperScrollView;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPCustomLevelView")]
public class MPCustomLevelView : AWindow
{
    /// <summary>
    /// 返回按钮。
    /// </summary>
    [TransformPath("View/Up/BackBtn")]
    private Button m_backBtn;

    /// <summary>
    /// 设置按钮。
    /// </summary>
    [TransformPath("View/Up/SettingBtn")]
    private Button m_settingBtn;

    /// <summary>
    /// 自定义关卡滚动列表。
    /// </summary>
    [TransformPath("View/Center/Levels")]
    private LoopGridView m_loopGrid;

    /// <summary>
    /// 空仓库提示节点。
    /// </summary>
    [TransformPath("View/Center/EmptyTip")]
    private RectTransform m_emptyTip;

    /// <summary>
    /// 空关卡 创建按钮
    /// </summary>
    [TransformPath("View/Center/EmptyTip/CreateBtn")]
    private Button m_createBtn;

    /// <summary>
    /// 自定义关卡数据列表。
    /// </summary>
    private List<MPCustomLevelInfo> m_levelInfos;

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
    /// 加载自定义关卡列表页面数据。
    /// </summary>
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_levelInfos = MPUser.instance.GetCustomLevels();
        m_loopGrid.InitGridView(m_levelInfos.Count, GetCustomLevelByRowColumn);
        RefreshEmptyTip();

        m_backBtn.onClick.AddListener(OnBackClick);
        m_settingBtn.onClick.AddListener(OnSettingClick);
        m_createBtn.onClick.AddListener(OnBackClick);

        RefreshUI();
    }

    private void RefreshUI()
    {
        m_coinText.text = MPUser.instance.GetCoins().ToString();
        m_diamondText.text = MPUser.instance.GetDiamond().ToString();
    }

    /// <summary>
    /// 根据索引获取自定义关卡列表项。
    /// </summary>
    private LoopGridViewItem GetCustomLevelByRowColumn(LoopGridView view, int index, int row, int column)
    {
        if (index < 0 || index >= m_levelInfos.Count)
            return null;

        LoopGridViewItem item = m_loopGrid.NewListViewItem("MPCustomLevelItem");
        MPCustomLevelItem level = item.GetComponent<MPCustomLevelItem>();
        if (level == null)
        {
            level = item.gameObject.AddComponent<MPCustomLevelItem>();
        }

        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            level.Initialize(RefreshLevels);
        }

        level.Refresh(m_levelInfos[index], index);
        return item;
    }

    /// <summary>
    /// 刷新自定义关卡列表。
    /// </summary>
    private void RefreshLevels()
    {
        m_levelInfos = MPUser.instance.GetCustomLevels();
        m_loopGrid.SetListItemCount(m_levelInfos.Count);
        m_loopGrid.RefreshAllShownItem();
        RefreshEmptyTip();
    }

    /// <summary>
    /// 刷新空仓库提示显示。
    /// </summary>
    private void RefreshEmptyTip()
    {
        if (m_emptyTip != null)
        {
            m_emptyTip.gameObject.SetActive(m_levelInfos == null || m_levelInfos.Count == 0);
        }
    }

    /// <summary>
    /// 返回上一页面。
    /// </summary>
    private void OnBackClick()
    {
        DestroyWindow();
    }

    /// <summary>
    /// 设置按钮点击回调。
    /// </summary>
    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }
}

