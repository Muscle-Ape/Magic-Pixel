using HQ.UIManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 字段管理
/// </summary>
[Component("MPGameView")]
public partial class MPGameView : AWindow
{
    /// <summary>
    /// 网格区域固定大小
    /// </summary>
    private const int GRID_SIZE = 800;

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
    /// 方块信息
    /// </summary>
    private MPMainBlockInfo m_blockInfo;

    /// <summary>
    /// 方块预制体
    /// </summary>
    private MPGameBlock m_blockPrefab;

    /// <summary>
    /// 顶部的数字提示预制体
    /// </summary>
    private MPGameNumberFrameHorizontal m_numberHorizontalPrefab;

    /// <summary>
    /// 左侧的数字提示预制体
    /// </summary>
    private MPGameNumberFrameVertical m_numberVerticalPrefab;

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



    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_blockInfo = (uiMsg as UIMsgDataGeneric).Arg1 as MPMainBlockInfo;

        m_blockPrefab = MPLoad.Load<GameObject>("MPGameBlock").GetComponent<MPGameBlock>();

        m_numberHorizontalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameHorizontal").GetComponent<MPGameNumberFrameHorizontal>();

        m_numberVerticalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameVertical").GetComponent<MPGameNumberFrameVertical>();

        m_pixel = MPLoad.Load<Texture2D>(m_blockInfo.ID);

        m_size = m_pixel.height;

        m_detectionInterval = GRID_SIZE / m_size * (Screen.height / 2338f) * 0.9f;


        StartInitialization();
    }

    private void StartInitialization()
    {
        CreateGrid();

        CreateHorizontalNumber();

        CreateVerticalNumver();


        RegisterInput();
    }
}
