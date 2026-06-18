using HQ.UIManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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



    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_blockInfo = (uiMsg as UIMsgDataGeneric).Arg1 as MPMainBlockInfo;

        m_blockPrefab = MPLoad.Load<GameObject>("MPGameBlock").GetComponent<MPGameBlock>();

        m_numberHorizontalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameHorizontal").GetComponent<MPGameNumberFrameHorizontal>();

        m_numberVerticalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameVertical").GetComponent<MPGameNumberFrameVertical>();

        m_pixel = MPLoad.Load<Texture2D>(m_blockInfo.ID);

        m_size = m_pixel.height;


        StartInitialization();
    }

    private void StartInitialization()
    {
        CreateGrid();

        CreateHorizontalNumber();

        CreateVerticalNumver();
    }
}
