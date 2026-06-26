using HQ.UIManager;
using SuperScrollView;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Component("MPLargeImageLevelView")]
public class MPLargeImageLevelView : AWindow
{
    /// <summary>
    /// 返回按钮
    /// </summary>
    [TransformPath("View/Up/BackBtn")]
    private Button m_backBtn;

    /// <summary>
    /// 设置按钮
    /// </summary>
    [TransformPath("View/Up/SettingBtn")]
    private Button m_settingBtn;

    /// <summary>
    /// 关卡滚动视图
    /// </summary>
    [TransformPath("View/Center/Levels")]
    private LoopGridView m_loopGrid;

    /// <summary>
    /// 关卡数据
    /// </summary>
    private MPLargeImageLevelModel m_levelModel;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_levelModel = MPDataManager.Instance.m_largeImageModel;

        m_loopGrid.InitGridView(m_levelModel.blockInfos.Count, GetLargeImageLevelByRowColumn);

        m_backBtn.onClick.AddListener(OnBackClick);
        m_settingBtn.onClick.AddListener(OnSettingClick);
    }

    private LoopGridViewItem GetLargeImageLevelByRowColumn(LoopGridView view, int index, int row, int column)
    {
        // 1、索引越界
        if (index < 0 || index >= m_levelModel.blockInfos.Count)
            return null;

        // 2、获取对应数据
        MPLargeImageBlockInfo data = m_levelModel.blockInfos[index];

        // 3、从对象池中获取或者创建新对象
        LoopGridViewItem item = m_loopGrid.NewListViewItem("MPLargeImageLevelItem");

        // 4、获取控制组件
        MPLargeImageLevelItem level = item.GetComponent<MPLargeImageLevelItem>();

        // 5、初始化并刷新
        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            level.Initialize(RefreshLevels);
        }
        level.Refresh(data, index);

        return item;
    }

    private void RefreshLevels()
    {
        m_loopGrid.RefreshAllShownItem();
    }


    private void OnBackClick()
    {
        DestroyWindow();
    }


    private void OnSettingClick()
    {

    }
}
