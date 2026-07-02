using DG.Tweening;
using HQ.UIManager;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;

[Component("MPCustomView")]
public partial class MPCustomView : AWindow
{
    /// <summary>
    /// 网格区域固定大小
    /// </summary>
    private const int GRID_SIZE = 800;

    /// <summary>
    /// 像素网格
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
    /// 模式切换（上色模式/填充模式）
    /// </summary>
    [TransformPath("View/ModeSwitch")]
    private Button m_modeSwitchFrame;

    // <summary>
    /// 滑动的按钮
    /// </summary>
    [TransformPath("View/ModeSwitch/Btn")]
    private RectTransform m_modeSwitchBtn;

    /// <summary>
    /// 填充模式图标
    /// </summary>
    [TransformPath("View/ModeSwitch/Btn/Fill")]
    private Image m_modeSwitchFill;

    /// <summary>
    /// 空白模式图片
    /// </summary>
    [TransformPath("View/ModeSwitch/Btn/Blank")]
    private Image m_modeSwitchBlank;

    /// </summary>
    /// 大小切换（5/10）
    /// </summary>
    [TransformPath("View/SizeSwitch")]
    private Button m_sizeSwitchFrame;

    // <summary>
    /// 大小切换滑动的按钮
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
    /// 输入控制节点
    /// </summary>
    [TransformPath("View/Content/Input")]
    private RectTransform m_input;

    /// <summary>
    /// 模式切换动画
    /// </summary>
    private Tween m_modeSwitchTween;

    /// <summary>
    /// 大小切换动画
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
    /// 存放已经创建了的方块
    /// </summary>
    private List<MPCustomBlock> m_blocks;

    /// <summary>
    /// 是否是填充模式
    /// </summary>
    private bool m_isFillMode = false;

    /// <summary>
    /// 大小是否是十
    /// </summary>
    private bool m_isTenSize = false;

    /// <summary>
    ///  存放射线检测的结果
    /// </summary>
    private List<RaycastResult> m_rayResults = new List<RaycastResult>();

    /// <summary>
    /// 当前使用的颜色
    /// </summary>
    private Color m_currentColor = Color.red;

    /// <summary>
    /// 当前这一轮拖拽所经过的方块
    /// </summary>
    private List<MPCustomBlock> m_currentDragBlocks;

    /// <summary>
    /// 是否是清除状态
    /// </summary>
    private bool m_isClear;



    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
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
