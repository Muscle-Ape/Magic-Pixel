using HQ.UIManager;
using System;
using System.Collections.Generic;
using System.Threading;
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
    /// 自定义页面默认选中的颜色。
    /// </summary>
    private const string DEFAULT_CUSTOM_COLOR = "#FF8E64";

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
    /// 填充模式按钮。
    /// </summary>
    [TransformPath("View/FillBtn")]
    private Button m_fillModeBtn;

    /// <summary>
    /// 上色模式按钮。
    /// </summary>
    [TransformPath("View/PenBtn")]
    private Button m_colorModeBtn;

    /// <summary>
    /// 10x10尺寸按钮。
    /// </summary>
    [TransformPath("View/SizeSwitch/Ten")]
    private Button m_sizeTenBtn;

    /// <summary>
    /// 5x5尺寸按钮。
    /// </summary>
    [TransformPath("View/SizeSwitch/Five")]
    private Button m_sizeFiveBtn;

    /// <summary>
    /// 10x10尺寸选中状态节点。
    /// </summary>
    [TransformPath("View/SizeSwitch/Ten/Open")]
    private RectTransform m_sizeTenOpen;

    /// <summary>
    /// 5x5尺寸选中状态节点。
    /// </summary>
    [TransformPath("View/SizeSwitch/Five/Open")]
    private RectTransform m_sizeFiveOpen;

    /// <summary>
    /// 用户手指输入节点
    /// </summary>
    [TransformPath("View/Content/Input")]
    private RectTransform m_input;

    /// <summary>
    /// 保存自定义关卡按钮。
    /// </summary>
    [TransformPath("View/SaveBtn")]
    private Button m_saveBtn;

    /// <summary>
    /// 当前编辑页面的公开上传按钮。Prefab 没有配置时会在运行时根据 SaveBtn 克隆创建。
    /// </summary>
    [TransformPath("View/PublishBtn")]
    private Button m_publishBtn;

    /// <summary>
    /// 公开上传按钮文本，用于显示 Upload 和等待状态。
    /// </summary>
    [TransformPath("View/PublishBtn/Text")]
    private TMP_Text m_publishText;

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
    /// 关卡仓库按钮图标节点。
    /// </summary>
    [TransformPath("View/WarehouseBtn/Icon")]
    private RectTransform m_warehouseIcon;

    /// <summary>
    /// 保存动画节点。
    /// </summary>
    [TransformPath("View/AnimationNode")]
    private RectTransform m_animationNode;

    /// <summary>
    /// 保存动画图片。
    /// </summary>
    [TransformPath("View/AnimationNode/Picture")]
    private Image m_animationPicture;

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
    /// 保存后刷新关卡列表的回调。
    /// </summary>
    private Action m_refreshAction;

    /// <summary>
    /// 当前自定义关卡网格尺寸。
    /// </summary>
    private int m_currentSize = 5;

    /// <summary>
    /// 保存动画节点初始锚点位置。
    /// </summary>
    private Vector2 m_animationNodeStartAnchoredPosition;

    /// <summary>
    /// 仓库图标初始缩放。
    /// </summary>
    private Vector3 m_warehouseIconStartScale = Vector3.one;

    /// <summary>
    /// 保存动画序列。
    /// </summary>
    private DG.Tweening.Sequence m_saveAnimationSequence;

    /// <summary>
    /// 保存动画运行时贴图。
    /// </summary>
    private Texture2D m_saveAnimationTexture;

    /// <summary>
    /// 保存动画运行时精灵。
    /// </summary>
    private Sprite m_saveAnimationSprite;

    /// <summary>
    /// 当前编辑页公开上传操作取消源，界面关闭时用于阻止异步回写已销毁 UI。
    /// </summary>
    private CancellationTokenSource m_publishCancellation;

    /// <summary>
    /// 当前编辑页是否正在执行公开上传，避免连续点击重复发布同一份关卡。
    /// </summary>
    private bool m_isPublishActionRunning;



    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPLoad.ReleaseAll(this);
        MPCustomViewUIMsgData data = uiMsg as MPCustomViewUIMsgData;
        m_refreshAction = data?.refresh;

        m_blockPrefab = MPLoad.Load<GameObject>("MPCustomBlock", this).GetComponent<MPCustomBlock>();

        m_blockPool = new ObjectPool<MPCustomBlock>(PoolCreate, PoolGet, PoolRelease, defaultCapacity: 25, maxSize: 100);

        m_blocks = new List<MPCustomBlock>();

        m_currentDragBlocks = new List<MPCustomBlock>();


        StartInitialization();
    }

    public override void OnFocus(bool focus)
    {
        if(focus)
        {
            RefreshUI();
            RefreshPublishButtonState();
        }
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

