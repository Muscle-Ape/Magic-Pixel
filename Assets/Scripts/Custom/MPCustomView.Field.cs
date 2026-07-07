using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;

[Component("MPCustomView")]
public partial class MPCustomView : AWindow
{
    /// <summary>
    /// 固定格子大小
    /// </summary>
    private const int GRID_SIZE = 800;

    /// <summary>
    /// 网格节点
    /// </summary>
    [TransformPath("View/Content/Grid")]
    private GridLayoutGroup m_blockGrid;

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
    /// 模式切换按钮
    /// </summary>
    [TransformPath("View/ModeSwitch")]
    private Button m_modeSwitchFrame;

    // <summary>
    /// 模式切换移动节点
    /// </summary>
    [TransformPath("View/ModeSwitch/Btn")]
    private RectTransform m_modeSwitchBtn;

    /// <summary>
    /// 填充模式图片
    /// </summary>
    [TransformPath("View/ModeSwitch/Btn/Fill")]
    private Image m_modeSwitchFill;

    /// <summary>
    /// 上色模式图片
    /// </summary>
    [TransformPath("View/ModeSwitch/Btn/Blank")]
    private Image m_modeSwitchBlank;

    /// </summary>
    /// 大小切换模式按钮
    /// </summary>
    [TransformPath("View/SizeSwitch")]
    private Button m_sizeSwitchFrame;

    // <summary>
    /// 大小切换移动节点
    /// </summary>
    [TransformPath("View/SizeSwitch/Btn")]
    private RectTransform m_sizeSwitchBtn;

    /// <summary>
    /// 10
    /// </summary>
    [TransformPath("View/SizeSwitch/Btn/Ten")]
    private RectTransform m_sizeSwitchTen;

    /// <summary>
    /// 5
    /// </summary>
    [TransformPath("View/SizeSwitch/Btn/Five")]
    private RectTransform m_sizeSwitchFive;

    /// <summary>
    /// 用户手指输入节点
    /// </summary>
    [TransformPath("View/Content/Input")]
    private RectTransform m_input;

    /// <summary>
    /// 色块按钮
    /// </summary>
    [TransformPath("View/ColorNode/ColorFrame")]
    private Button m_colorFrame;

    /// <summary>
    /// 调色板
    /// </summary>
    [TransformPath("View/ColorNode/ColorPanel")]
    private CanvasGroup m_colorPanel;

    /// <summary>
    /// 保存自定义关卡按钮。
    /// </summary>
    [TransformPath("View/SaveBtn")]
    private Button m_saveBtn;

    /// <summary>
    /// 自定义关卡标题输入框。
    /// </summary>
    [TransformPath("View/Title")]
    private TMP_InputField m_titleInput;

    /// <summary>
    /// 关卡仓库按钮
    /// </summary>
    [TransformPath("View/WarehouseBtn")]
    private Button m_warehouseBtn;

    /// <summary>
    /// 模式切换动画
    /// </summary>
    private Tween m_modeSwitchTween;

    /// <summary>
    /// 方格数量按钮切换动画
    /// </summary>
    private Tween m_sizeSwithcTween;

    /// <summary>
    /// 方块预制体
    /// </summary>
    private MPCustomBlock m_blockPrefab;

    /// <summary>
    /// 方块对象池
    /// </summary>
    private ObjectPool<MPCustomBlock> m_blockPool;

    /// <summary>
    /// 创建出来的所有方块
    /// </summary>
    private List<MPCustomBlock> m_blocks;

    /// <summary>
    /// 是否是填充模式
    /// </summary>
    private bool m_isFillMode = false;

    /// <summary>
    /// 网格大小是否是10
    /// </summary>
    private bool m_isTenSize = false;

    /// <summary>
    ///  射线检测结果
    /// </summary>
    private List<RaycastResult> m_rayResults = new List<RaycastResult>();

    /// <summary>
    /// 当前颜色
    /// </summary>
    private Color m_currentColor = Color.white;

    /// <summary>
    /// 当前拖拽过的格子
    /// </summary>
    private List<MPCustomBlock> m_currentDragBlocks;

    /// <summary>
    /// 是否是清除状态
    /// </summary>
    private bool m_isClear;

    /// <summary>
    /// 调色板是否打开
    /// </summary>
    private bool m_colorPanelIsOpen;

    /// <summary>
    /// 调色板动画
    /// </summary>
    private Sequence m_colorPanelSequence;

    /// <summary>
    /// 色块动画
    /// </summary>
    private Tween m_colorFrameTween;

    /// <summary>
    /// 保存后刷新关卡列表的回调。
    /// </summary>
    private Action m_refreshAction;

    /// <summary>
    /// 当前自定义关卡网格尺寸。
    /// </summary>
    private int m_currentSize = 5;



    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPCustomViewUIMsgData data = uiMsg as MPCustomViewUIMsgData;
        m_refreshAction = data?.refresh;

        m_blockPrefab = MPLoad.Load<GameObject>("MPCustomBlock").GetComponent<MPCustomBlock>();

        m_blockPool = new ObjectPool<MPCustomBlock>(PoolCreate, PoolGet, PoolRelease, defaultCapacity: 25, maxSize: 100);

        m_blocks = new List<MPCustomBlock>();

        m_currentDragBlocks = new List<MPCustomBlock>();


        StartInitialization();
    }

    private void StartInitialization()
    {
        CreateGrid(5);

        RegisterUI();

        RegisterInput();
    }
}

public class MPCustomViewUIMsgData : UIMsgData
{
    /// <summary>
    /// 自定义关卡保存后的刷新回调。
    /// </summary>
    public Action refresh;
}

