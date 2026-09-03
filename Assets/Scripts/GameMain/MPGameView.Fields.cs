using HQ.UIManager;
using System;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

/// <summary>
/// 主游戏模式页面实现。
/// 普通关卡和自定义关卡共用 MPGameView.prefab，并在此实现主游戏专属数据与资源规则。
/// </summary>
[Component("MPGameView")]
public partial class MPGameView : MPGameViewBase
{
    /// <summary>
    /// 方块信息
    /// </summary>
    private MPMainBlockInfo m_blockInfo;

    /// <summary>
    /// 当前关卡是否为自定义关卡。
    /// </summary>
    private bool m_isCustomLevel;

    private bool m_progressCacheValidated;
    private MPLevelProgressCacheInfo m_entryProgressCache;

    /// <summary>
    /// 当前自定义关卡数据，用于结算时读取本地缓存的完成图片。
    /// </summary>
    private MPCustomLevelInfo m_customLevelInfo;

    /// <summary>
    /// 方块预制体
    /// </summary>
    private GameObject m_blockPrefab;

    /// <summary>
    /// 游戏网格使用的四角外框图片。
    /// </summary>
    private Sprite m_blockLeftTopSprite;
    private Sprite m_blockRightTopSprite;
    private Sprite m_blockLeftDownSprite;
    private Sprite m_blockRightDownSprite;

    /// <summary>
    /// 数字提示框使用的角部外框图片。
    /// </summary>
    private Sprite m_numberLeftTopSprite;
    private Sprite m_numberRightTopSprite;
    private Sprite m_numberLeftDownSprite;

    /// <summary>
    /// 当前像素图是否为运行时读取的本地自定义图片，页面释放时需要主动销毁。
    /// </summary>
    private bool m_isRuntimePixelTexture;

    /// <summary>
    /// 所有的方块
    /// </summary>
    private List<MPGameBlock> m_blocks;

    /// <summary>
    /// 是否是填充模式
    /// </summary>
    private bool m_isFillMode = true;

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

    /// <summary>普通主线关卡使用生命值，自定义关卡不使用生命值。</summary>
    protected override bool UsesLives => !m_isCustomLevel;

    /// <summary>主游戏当前是否为填充模式。</summary>
    protected override bool IsFillMode => m_isFillMode;

    /// <summary>主游戏页面标题。</summary>
    protected override string LevelTitle => $"Level {m_index + 1}";

    /// <summary>
    /// 解析主游戏页面消息，并兼容普通主线关卡与自定义关卡两种数据来源。
    /// </summary>
    protected override void LoadLevelData(UIMsgData uiMsg)
    {
        if (!(uiMsg is MPGameViewUIMsgData data))
        {
            throw new ArgumentException($"{nameof(MPGameView)} 需要 {nameof(MPGameViewUIMsgData)}。", nameof(uiMsg));
        }

        m_isCustomLevel = data.isCustomLevel;
        m_progressCacheValidated = data.progressCacheValidated;
        m_entryProgressCache = data.progressCache;
        m_customLevelInfo = m_isCustomLevel ? data.customLevelInfo : null;
        if (m_isCustomLevel && m_customLevelInfo == null)
        {
            throw new InvalidOperationException("自定义关卡数据不能为空。");
        }

        m_blockInfo = m_isCustomLevel ? m_customLevelInfo.ToMainBlockInfo() : data.blockInfo;
        if (m_blockInfo == null)
        {
            throw new InvalidOperationException("主游戏关卡数据不能为空。");
        }

        m_index = data.index;
        m_refreshAction = data.refresh;
        m_isFillMode = true;
        m_isRuntimePixelTexture = false;
    }

    /// <summary>加载主游戏方块预制体和当前关卡像素图。</summary>
    protected override void LoadLevelAssets()
    {
        m_blockPrefab = MPLoad.Load<GameObject>("MPGameBlock", this);
        m_blockLeftTopSprite = LoadOptionalFrameSprite("game_block_lt");
        m_blockRightTopSprite = LoadOptionalFrameSprite("game_block_rt");
        m_blockLeftDownSprite = LoadOptionalFrameSprite("game_block_ld");
        m_blockRightDownSprite = LoadOptionalFrameSprite("game_block_rd");
        m_numberLeftTopSprite = LoadOptionalFrameSprite("game_number_lt");
        m_numberRightTopSprite = LoadOptionalFrameSprite("game_number_rt");
        m_numberLeftDownSprite = LoadOptionalFrameSprite("game_number_ld");

        if (m_isCustomLevel)
        {
            m_size = m_customLevelInfo.Size;
            m_pixel = MPUser.instance.LoadCustomLevelImageTexture(m_customLevelInfo);
            m_isRuntimePixelTexture = m_pixel != null;
        }
        else
        {
            m_pixel = MPLoad.Load<Texture2D>(m_blockInfo.ID, this);
            m_isRuntimePixelTexture = false;
            m_size = m_pixel == null ? 0 : m_pixel.height;
        }
    }

    /// <summary>
    /// 加载可选外框图片。资源尚未加入 YooAsset 清单时保留预制体默认图片，避免阻断页面创建。
    /// </summary>
    private Sprite LoadOptionalFrameSprite(string location)
    {
        if (!YooAssets.CheckLocationValid(location))
        {
            Debug.LogWarning($"游戏外框资源不存在或尚未加入 YooAsset 清单：{location}");
            return null;
        }

        return MPLoad.Load<Sprite>(location, this);
    }

    /// <summary>释放自定义关卡运行时创建的像素纹理。</summary>
    protected override void ReleaseModeSpecificResources()
    {
        ReleaseRuntimePixelTexture();
    }
}

/// <summary>打开主游戏页面时使用的消息数据。</summary>
public class MPGameViewUIMsgData : UIMsgData
{
    /// <summary>普通主线关卡数据。</summary>
    public MPMainBlockInfo blockInfo;

    /// <summary>
    /// 自定义关卡数据。
    /// </summary>
    public MPCustomLevelInfo customLevelInfo;

    /// <summary>
    /// 是否打开自定义关卡。
    /// </summary>
    public bool isCustomLevel;

    /// <summary>进入选择弹窗校验后的只读缓存；已选择重新开始时为 null。</summary>
    public MPLevelProgressCacheInfo progressCache;
    public bool progressCacheValidated;

    /// <summary>当前关卡在主线或自定义列表中的下标。</summary>
    public int index;

    /// <summary>退出或完成关卡后刷新关卡列表的回调。</summary>
    public Action refresh;
}
