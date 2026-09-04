using HQ.UIManager;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YooAsset;

/// <summary>
/// 大图模式字段与关卡资源管理。
/// 控制器复用 MPGameView.prefab，差异逻辑由 MPGameViewBase 的抽象接口隔离。
/// </summary>
[Component("MPGameView")]
public partial class MPLargeImageGameView : MPGameViewBase
{
    /// <summary>完整大图中每个格子的持久化状态。</summary>
    private enum BlockStatue
    {
        Empty,
        Fill,
        Blank,
    }

    /// <summary>
    /// 大图模式每次展示的固定行列数量。
    /// </summary>
    private const int FIXED_SIZE = 10;

    /// <summary>当前大图关卡配置。</summary>
    private MPLargeImageBlockInfo m_blockInfo;

    private bool m_progressCacheValidated;
    private MPLevelProgressCacheInfo m_entryProgressCache;

    /// <summary>主游戏与大图模式共用的格子预制体。</summary>
    private GameObject m_blockPrefab;

    /// <summary>可视网格固定为 10×10，格子与顶部、左侧数字框共用 game_block_10。</summary>
    private Sprite m_blockFrameSprite;

    /// <summary>大图可视区域固定使用 game_block_fill_10，填充色由玩家设置决定。</summary>
    private Sprite m_blockFillSprite;

    /// <summary>当前 10×10 可视区域中的格子列表。</summary>
    private List<MPLargeImageGameBlock> m_blocks;

    /// <summary>当前是否为填充模式。</summary>
    private bool m_isFill = true;

    /// <summary>一次拖拽操作命中的第一个格子。</summary>
    private MPLargeImageGameBlock m_dragFirstBlock;

    /// <summary>一次拖拽操作命中的第二个格子，用于锁定拖拽方向。</summary>
    private MPLargeImageGameBlock m_dragSecondBlock;

    /// <summary>顶部横向数字提示列表。</summary>
    private List<MPLargeImageGameNumberFrameHorizontal> m_numberHorizontalList;

    /// <summary>左侧纵向数字提示列表。</summary>
    private List<MPLargeImageGameNumberFrameVertical> m_numberVerticalList;

    /// <summary>当前可视区域对应的二维格子数组。</summary>
    private MPLargeImageGameBlock[][] m_blockGrid2Array;

    /// <summary>最近一次操作的格子。</summary>
    private MPLargeImageGameBlock m_lastBlock;

    /// <summary>完整大图所有格子的状态数组，不随可视窗口移动而丢失。</summary>
    private BlockStatue[][] m_blockStatues;

    /// <summary>当前 10×10 可视窗口在完整大图中的左上角坐标。</summary>
    private Vector2Int m_blockStatueHead;

    /// <summary>数字栏拖拽尚未消耗的屏幕位移。</summary>
    private Vector2 m_numberFrameDragOffset;

    /// <summary>大图模式使用共用生命值和道具逻辑。</summary>
    protected override bool UsesLives => true;

    /// <summary>大图模式当前是否为填充模式。</summary>
    protected override bool IsFillMode => m_isFill;

    /// <summary>大图模式页面标题。</summary>
    protected override string LevelTitle => $"Big Level {m_index + 1}";

    /// <summary>解析大图关卡页面消息并重置输入模式。</summary>
    protected override void LoadLevelData(UIMsgData uiMsg)
    {
        if (!(uiMsg is MPLargeImageGameViewUIMsgData data))
        {
            throw new ArgumentException($"{nameof(MPLargeImageGameView)} 需要 {nameof(MPLargeImageGameViewUIMsgData)}。", nameof(uiMsg));
        }

        if (data.blockInfo == null)
        {
            throw new InvalidOperationException("大图关卡数据不能为空。");
        }

        m_blockInfo = data.blockInfo;
        m_progressCacheValidated = data.progressCacheValidated;
        m_entryProgressCache = data.progressCache;
        m_index = data.index;
        m_refreshAction = data.refresh;
        m_isFill = true;
    }

    /// <summary>
    /// 加载大图方块和完整像素图，并创建独立于可视窗口的完整状态数组。
    /// </summary>
    protected override void LoadLevelAssets()
    {
        m_blockPrefab = MPLoad.Load<GameObject>("MPGameBlock", this);
        m_blockFrameSprite = LoadOptionalBlockSprite("game_block_10");
        m_blockFillSprite = LoadOptionalBlockSprite("game_block_fill_10");
        m_pixel = MPLoad.Load<Texture2D>(m_blockInfo.ID, this);
        m_size = m_pixel == null ? 0 : m_pixel.height;

        if (m_size < FIXED_SIZE)
        {
            throw new InvalidOperationException($"大图关卡尺寸不能小于 {FIXED_SIZE}，当前尺寸：{m_size}。");
        }

        m_blockStatues = Enumerable.Range(0, m_size).Select(_ => new BlockStatue[m_size]).ToArray();
        m_blockStatueHead = Vector2Int.zero;
    }

    /// <summary>
    /// 加载可选外框或填充图片。资源尚未加入 YooAsset 清单时保留预制体默认图片，避免阻断页面创建。
    /// </summary>
    private Sprite LoadOptionalBlockSprite(string location)
    {
        if (!YooAssets.CheckLocationValid(location))
        {
            Debug.LogWarning($"游戏格子图片不存在或尚未加入 YooAsset 清单：{location}");
            return null;
        }

        return MPLoad.Load<Sprite>(location, this);
    }
}

/// <summary>打开大图模式游戏页时使用的消息数据。</summary>
public class MPLargeImageGameViewUIMsgData : UIMsgData
{
    public MPLevelProgressCacheInfo progressCache;
    public bool progressCacheValidated;

    /// <summary>当前大图关卡配置。</summary>
    public MPLargeImageBlockInfo blockInfo;

    /// <summary>当前关卡在大图关卡列表中的下标。</summary>
    public int index;

    /// <summary>退出或完成关卡后刷新大图关卡列表的回调。</summary>
    public Action refresh;
}
