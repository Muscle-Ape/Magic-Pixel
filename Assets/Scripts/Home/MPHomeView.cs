using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HQ.UIManager;
using SuperScrollView;
using UnityEngine.UI;

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
    /// 主关卡数据
    /// </summary>
    private MPMainLevelModel m_levelModel;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_levelModel = MPDataManager.Instance.m_mainLevelModel;

        m_loopGrid.InitGridView(m_levelModel.blockInfos.Count, GetMainLevelByRowColumn);

        m_settingBtn.onClick.AddListener(OnSettingClick);
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
            level.Initialize(RefreshLevels);
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

    }
}
