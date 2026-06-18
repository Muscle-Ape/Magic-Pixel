using HQ.UIManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 字段管理
/// </summary>
[Component("MPGameView")]
public partial class MPGameView : AWindow
{
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
    private RectTransform m_blockGrid;

    /// <summary>
    /// 分隔线段节点
    /// </summary>
    [TransformPath("View/Content/Line")] 
    private RectTransform m_lineNode;
}
