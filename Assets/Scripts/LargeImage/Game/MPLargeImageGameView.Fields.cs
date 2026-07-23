using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 字段管理
/// </summary>
[Component("MPLargeImageGameView")]
public partial class MPLargeImageGameView : AWindow
{
    /// <summary>
    /// 方块状态
    /// </summary>
    private enum BlockStatue
    {
        /// <summary>
        /// 未填充
        /// </summary>
        Empty,
        /// <summary>
        /// 已填充（颜色）
        /// </summary>
        Fill,
        /// <summary>
        /// 已填充（X）
        /// </summary>
        Blank,
    }

    /// <summary>
    /// 网格区域固定大小
    /// </summary>
    private const int GRID_SIZE = 800;

    /// <summary>
    /// 固定行列数量
    /// </summary>
    private const int FIXED_SIZE = 10;

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
    /// 游戏内容边框。
    /// </summary>
    [TransformPath("View/Content/Frame")]
    private Image m_contentFrame;

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
    /// 移动方向按钮（上）
    /// </summary>
    [TransformPath("View/Move/Up")]
    private RectTransform m_moveUp;

    /// <summary>
    /// 移动方向按钮（下）
    /// </summary>
    [TransformPath("View/Move/Down")]
    private RectTransform m_moveDown;

    /// <summary>
    /// 移动方向按钮（左）
    /// </summary>
    [TransformPath("View/Move/Left")]
    private RectTransform m_moveLeft;

    /// <summary>
    /// 移动方向按钮（右）
    /// </summary>
    [TransformPath("View/Move/Right")]
    private RectTransform m_moveRight;

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
    /// 标题文本
    /// </summary>
    [TransformPath("View/Title")]
    private RectTransform m_title;

    /// <summary>
    /// 标题文本内容。
    /// </summary>
    [TransformPath("View/Title/Text")]
    private TMP_Text m_titleText;

    /// <summary>
    /// 生命值节点。
    /// </summary>
    [TransformPath("View/Loves")]
    private RectTransform m_lovesNode;

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
    private MPLargeImageBlockInfo m_blockInfo;

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
    private MPLargeImageGameBlock m_blockPrefab;

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
    private List<MPLargeImageGameBlock> m_blocks;

    /// <summary>
    ///  存放射线检测的结果
    /// </summary>
    private List<RaycastResult> m_rayResults = new List<RaycastResult>();

    /// <summary>
    /// 是否是填充模式
    /// </summary>
    private bool m_isFill = true;

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
    private MPLargeImageGameBlock m_dragFirstBlock;

    /// <summary>
    /// 当前拖拽下第二个拖拽到的方块
    /// 用来固定拖拽方向
    /// </summary>
    private MPLargeImageGameBlock m_dragSecondBlock;

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
    private List<MPLargeImageGameNumberFrameHorizontal> m_numberHorizontalList;

    /// <summary>
    /// 竖着的数字框容器
    /// </summary>
    private List<MPLargeImageGameNumberFrameVertical> m_numberVerticalList;

    /// <summary>
    /// 网格方块数据
    /// </summary>
    private MPLargeImageGameBlock[][] m_blockGrid2Array;

    /// <summary>
    /// 操作的最后一个方块
    /// </summary>
    private MPLargeImageGameBlock m_lastBlock;

    /// <summary>
    /// 行列完成数量
    /// </summary>
    private int m_hvCompleted;

    /// <summary>
    /// 所有方块的状态信息
    /// </summary>
    private BlockStatue[][] m_blockStatues;

    /// <summary>
    /// 标记方块信息数组当前的头的下标位置
    /// </summary>
    private Vector2Int m_blockStatueHead;

    /// <summary>
    /// 格子移动携程
    /// </summary>
    private Coroutine m_moveCoroutine;

    /// <summary>
    /// 数字栏拖拽累计偏移。
    /// </summary>
    private Vector2 m_numberFrameDragOffset;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPLargeImageGameViewUIMsgData data = uiMsg as MPLargeImageGameViewUIMsgData;
        m_blockInfo = data.blockInfo;
        m_index = data.index;
        m_refreshAction = data.refresh;

        m_blockPrefab = MPLoad.Load<GameObject>("MPLargeImageGameBlock").GetComponent<MPLargeImageGameBlock>();

        m_numberHorizontalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameHorizontal");

        m_numberVerticalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameVertical");

        // 初始化生命值
        Transform lovesNode = transform.Find("View/Loves");
        m_loves = new List<GameObject>();
        for (int i = 0; i < lovesNode.childCount; i++)
        {
            m_loves.Add(lovesNode.GetChild(i).GetChild(0).gameObject);
        }
        m_lovesCount = m_loves.Count;
        if (m_props != null)
        {
            m_props.gameObject.SetActive(true);
        }

        m_pixel = MPLoad.Load<Texture2D>(m_blockInfo.ID);

        m_size = m_pixel.height;

        m_blockStatues = Enumerable.Range(0, m_size).Select(i => new BlockStatue[m_size]).ToArray();

        m_blockStatueHead = Vector2Int.zero;

        m_detectionInterval = GRID_SIZE / m_size * (Screen.height / 2338f) * 0.9f;

        StartInitialization();

        MPAudioManager.Instance.StopBGM(MPMusic.MPBGMMain);
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

public class MPLargeImageGameViewUIMsgData : UIMsgData
{
    public MPLargeImageBlockInfo blockInfo;

    public int index;

    public Action refresh;
}
