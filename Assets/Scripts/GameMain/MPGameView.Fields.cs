using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Component("MPGameView")]
public partial class MPGameView : AWindow
{
    /// <summary>
    /// 网格区域固定大小
    /// </summary>
    private const int GRID_SIZE = 800;

    /// <summary>
    /// 页面根节点交互控制组件，游戏完成后用于统一禁止按钮点击。
    /// </summary>
    [TransformPath("View")]
    private CanvasGroup m_viewCanvasGroup;

    /// <summary>
    /// 竖着的数字提示父节点
    /// </summary>
    [TransformPath("View/Content/Vertical")]
    private RectTransform m_numberVertical;

    /// <summary>
    /// 横着的数字提示父节点
    /// </summary>
    [TransformPath("View/Content/Horizontal")]
    private RectTransform m_numberHorizontal;

    /// <summary>
    /// 像素网格
    /// </summary>
    [TransformPath("View/Content/Grid")]
    private GridLayoutGroup m_blockGrid;

    /// <summary>
    /// 结算时覆盖在中心内容区域上的完成图片框。
    /// </summary>
    [TransformPath("View/Content/CompletedFrame")]
    private Image m_completedFrame;

    /// <summary>
    /// 分隔线段节点
    /// </summary>
    [TransformPath("View/Content/Line")]
    private RectTransform m_lineNode;

    /// <summary>
    /// 输入控制节点
    /// </summary>
    [TransformPath("View/Content/Input")]
    private RectTransform m_input;

    /// <summary>
    /// 模式切换按钮
    /// </summary>
    [TransformPath("View/ModeSwitch")]
    private Button m_modeSwitchFrame;

    /// <summary>
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
    /// 道具按钮节点
    /// </summary>
    [TransformPath("View/Props")]
    private RectTransform m_props;

    /// <summary>
    /// 提示道具按钮
    /// </summary>
    [TransformPath("View/Props/HintBtn")]
    private Button m_hintPropBtn;

    /// <summary>
    /// 提示道具数量
    /// </summary>
    [TransformPath("View/Props/HintBtn/CountFrame/Count")]
    private TMP_Text m_hintPropCountText;

    /// <summary>
    /// 生命恢复道具按钮
    /// </summary>
    [TransformPath("View/Props/RecoverBtn")]
    private Button m_loveRecoverPropBtn;

    /// <summary>
    /// 生命恢复道具数量
    /// </summary>
    [TransformPath("View/Props/RecoverBtn/CountFrame/Count")]
    private TMP_Text m_loveRecoverPropCountText;

    /// <summary>
    /// 标题节点，结算动画开始时会随数字提示同步淡出。
    /// </summary>
    [TransformPath("View/Title")]
    private RectTransform m_title;

    /// <summary>
    /// 标题文本
    /// </summary>
    [TransformPath("View/Title/Text")]
    private TMP_Text m_titleText;

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
    /// 生命值父节点，结算动画开始时会随数字提示同步淡出。
    /// </summary>
    [TransformPath("View/Loves")]
    private RectTransform m_lovesNode;

    /// <summary>
    /// 生命值
    /// </summary>
    private List<GameObject> m_loves;

    /// <summary>
    /// 剩余生命值
    /// </summary>
    private int m_lovesCount;

    /// <summary>
    /// 当前是否已经打开失败弹窗，避免生命值归零后重复弹出。
    /// </summary>
    private bool m_isFailPopShowing;

    /// <summary>
    /// 方块信息
    /// </summary>
    private MPMainBlockInfo m_blockInfo;

    /// <summary>
    /// 当前关卡是否为自定义关卡。
    /// </summary>
    private bool m_isCustomLevel;

    /// <summary>
    /// 当前关卡所属的下标
    /// </summary>
    private int m_index;

    /// <summary>
    /// 刷新回调
    /// </summary>
    private Action m_refreshAction;

    /// <summary>
    /// 方块预制体
    /// </summary>
    private MPGameBlock m_blockPrefab;

    /// <summary>
    /// 顶部的数字提示预制体
    /// </summary>
    private GameObject m_numberHorizontalPrefab;

    /// <summary>
    /// 左侧的数字提示预制体
    /// </summary>
    private GameObject m_numberVerticalPrefab;

    /// <summary>
    /// 像素信息
    /// </summary>
    private Texture2D m_pixel;

    /// <summary>
    /// 大小
    /// </summary>
    private int m_size;

    /// <summary>
    /// 所有的方块
    /// </summary>
    private List<MPGameBlock> m_blocks;

    /// <summary>
    ///  存放射线检测的结果
    /// </summary>
    private List<RaycastResult> m_rayResults = new List<RaycastResult>();

    /// <summary>
    /// 是否是填充模式
    /// </summary>
    private bool m_isFillMode = true;

    /// <summary>
    /// 拖拽的最后一个坐标的位置
    /// </summary>
    private Vector2 m_pointerLastPosition;

    /// <summary>
    /// 检查间隔
    /// </summary>
    private float m_detectionInterval;

    /// <summary>
    /// 当前拖拽下第一个拖拽到的方块
    /// PointerDown
    /// </summary>
    private MPGameBlock m_dragFirstBlock;

    /// <summary>
    /// 当前拖拽下第二个拖拽到的方块
    /// 用来固定拖拽方向
    /// </summary>
    private MPGameBlock m_dragSecondBlock;

    /// <summary>
    /// 固定拖拽方向
    /// </summary>
    private Vector2 m_fixedDragDir = Vector2.zero;

    /// <summary>
    /// 是否可以继续拖拽
    /// </summary>
    private bool m_canDragContinue;

    /// <summary>
    /// 模式切换动画Tween
    /// </summary>
    private Tween m_modeSwitchTween;

    /// <summary>
    /// 横着的数字框容器
    /// </summary>
    private List<MPGameNumberFrameHorizontal> m_numberHorizontalList;

    /// <summary>
    /// 竖着的数字框容器
    /// </summary>
    private List<MPGameNumberFrameVertical> m_numberVerticalList;

    /// <summary>
    /// 网格方块数据
    /// </summary>
    private MPGameBlock[][] m_blockGrid2Array;

    /// <summary>
    /// 操作的最后一个方块
    /// </summary>
    private MPGameBlock m_lastBlock;

    /// <summary>
    /// 行列完成数量
    /// </summary>
    private int m_hvCompleted;



    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPGameViewUIMsgData data = uiMsg as MPGameViewUIMsgData;
        m_isCustomLevel = data.isCustomLevel;
        m_blockInfo = m_isCustomLevel ? data.customLevelInfo.ToMainBlockInfo() : data.blockInfo;
        m_index = data.index;
        m_refreshAction = data.refresh;

        m_blockPrefab = MPLoad.Load<GameObject>("MPGameBlock").GetComponent<MPGameBlock>();

        m_numberHorizontalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameHorizontal");

        m_numberVerticalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameVertical");

        // 初始化生命值
        Transform lovesNode = m_lovesNode == null ? transform.Find("View/Loves") : m_lovesNode;
        m_loves = new List<GameObject>();
        for (int i = 0; i < lovesNode.childCount; i++)
        {
            m_loves.Add(lovesNode.GetChild(i).GetChild(0).gameObject);
        }
        m_lovesCount = m_loves.Count;

        // 获取网格大小
        if (m_isCustomLevel)
        {
            m_size = data.customLevelInfo.Size;
            lovesNode.gameObject.SetActive(false);
            m_props.gameObject.SetActive(false);
        }
        else
        {
            m_pixel = MPLoad.Load<Texture2D>(m_blockInfo.ID);
            m_size = m_pixel.height;
            m_props.gameObject.SetActive(true);
        }

        m_detectionInterval = GRID_SIZE / m_size * (Screen.height / 2338f) * 0.9f;


        StartInitialization();
    }

    private void StartInitialization()
    {
        CreateGrid();

        CreateHorizontalNumber();

        CreateVerticalNumver();

        CreateLine();

        RegisterUI();

        RegisterInput();

        RestoreProgressCache();
    }
}

public class MPGameViewUIMsgData : UIMsgData
{
    public MPMainBlockInfo blockInfo;

    /// <summary>
    /// 自定义关卡数据。
    /// </summary>
    public MPCustomLevelInfo customLevelInfo;

    /// <summary>
    /// 是否打开自定义关卡。
    /// </summary>
    public bool isCustomLevel;

    public int index;

    public Action refresh;
}

