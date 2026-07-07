using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 瀛楁绠＄悊
    /// </summary>
[Component("MPGameView")]
public partial class MPGameView : AWindow
{
    /// <summary>
    /// 缃戞牸鍖哄煙鍥哄畾澶у皬
    /// </summary>
    private const int GRID_SIZE = 800;

    /// <summary>
    /// 绔栫潃鐨勬暟瀛楁彁绀虹埗鑺傜偣
    /// </summary>
    [TransformPath("View/Content/Vertical")]
    private RectTransform m_numberVertical;

    /// <summary>
    /// 妯潃鐨勬暟瀛楁彁绀虹埗鑺傜偣
    /// </summary>
    [TransformPath("View/Content/Horizontal")]
    private RectTransform m_numberHorizontal;

    /// <summary>
    /// 鍍忕礌缃戞牸
    /// </summary>
    [TransformPath("View/Content/Grid")]
    private GridLayoutGroup m_blockGrid;

    /// <summary>
    /// 鍒嗛殧绾挎鑺傜偣
    /// </summary>
    [TransformPath("View/Content/Line")]
    private RectTransform m_lineNode;

    /// <summary>
    /// 杈撳叆鎺у埗鑺傜偣
    /// </summary>
    [TransformPath("View/Content/Input")]
    private RectTransform m_input;

    /// <summary>
    /// 妯″紡鍒囨崲鎸夐挳
    /// </summary>
    [TransformPath("View/ModeSwitch")]
    private Button m_modeSwitchFrame;

    /// <summary>
    /// 婊戝姩鐨勬寜閽?
    /// </summary>
    [TransformPath("View/ModeSwitch/Btn")]
    private RectTransform m_modeSwitchBtn;

    /// <summary>
    /// 濉厖妯″紡鍥炬爣
    /// </summary>
    [TransformPath("View/ModeSwitch/Btn/Fill")]
    private Image m_modeSwitchFill;

    /// <summary>
    /// 绌虹櫧妯″紡鍥剧墖
    /// </summary>
    [TransformPath("View/ModeSwitch/Btn/Blank")]
    private Image m_modeSwitchBlank;

    /// <summary>
    /// 杩斿洖鎸夐挳
    /// </summary>
    [TransformPath("View/Up/BackBtn")]
    private Button m_backBtn;

    /// <summary>
    /// 璁剧疆鎸夐挳
    /// </summary>
    [TransformPath("View/Up/SettingBtn")]
    private Button m_settingBtn;

    /// <summary>
    /// 鏂瑰潡淇℃伅
    /// </summary>
    private MPMainBlockInfo m_blockInfo;

    /// <summary>
    /// 当前关卡是否为自定义关卡。
    /// </summary>
    private bool m_isCustomLevel;

    /// <summary>
    /// 褰撳墠鍏冲崱鎵€灞炵殑涓嬫爣
    /// </summary>
    private int m_index;

    /// <summary>
    /// 鍒锋柊鍥炶皟
    /// </summary>
    private Action m_refreshAction;

    /// <summary>
    /// 鏂瑰潡棰勫埗浣?
    /// </summary>
    private MPGameBlock m_blockPrefab;

    /// <summary>
    /// 椤堕儴鐨勬暟瀛楁彁绀洪鍒朵綋
    /// </summary>
    private GameObject m_numberHorizontalPrefab;

    /// <summary>
    /// 宸︿晶鐨勬暟瀛楁彁绀洪鍒朵綋
    /// </summary>
    private GameObject m_numberVerticalPrefab;

    /// <summary>
    /// 鍍忕礌淇℃伅
    /// </summary>
    private Texture2D m_pixel;

    /// <summary>
    /// 澶у皬
    /// </summary>
    private int m_size;

    /// <summary>
    /// 鎵€鏈夌殑鏂瑰潡
    /// </summary>
    private List<MPGameBlock> m_blocks;

    /// <summary>
    ///  瀛樻斁灏勭嚎妫€娴嬬殑缁撴灉
    /// </summary>
    private List<RaycastResult> m_rayResults = new List<RaycastResult>();

    /// <summary>
    /// 鏄惁鏄～鍏呮ā寮?
    /// </summary>
    private bool m_isFillMode = true;

    /// <summary>
    /// 鎷栨嫿鐨勬渶鍚庝竴涓潗鏍囩殑浣嶇疆
    /// </summary>
    private Vector2 m_pointerLastPosition;

    /// <summary>
    /// 妫€鏌ラ棿闅?
    /// </summary>
    private float m_detectionInterval;

    /// <summary>
    /// 褰撳墠鎷栨嫿涓嬬涓€涓嫋鎷藉埌鐨勬柟鍧?
    /// PointerDown
    /// </summary>
    private MPGameBlock m_dragFirstBlock;

    /// <summary>
    /// 褰撳墠鎷栨嫿涓嬬浜屼釜鎷栨嫿鍒扮殑鏂瑰潡
    /// 鐢ㄦ潵鍥哄畾鎷栨嫿鏂瑰悜
    /// </summary>
    private MPGameBlock m_dragSecondBlock;

    /// <summary>
    /// 鍥哄畾鎷栨嫿鏂瑰悜
    /// </summary>
    private Vector2 m_fixedDragDir = Vector2.zero;

    /// <summary>
    /// 鏄惁鍙互缁х画鎷栨嫿
    /// </summary>
    private bool m_canDragContinue;

    /// <summary>
    /// 妯″紡鍒囨崲鍔ㄧ敾Tween
    /// </summary>
    private Tween m_modeSwitchTween;

    /// <summary>
    /// 妯潃鐨勬暟瀛楁瀹瑰櫒
    /// </summary>
    private List<MPGameNumberFrameHorizontal> m_numberHorizontalList;

    /// <summary>
    /// 绔栫潃鐨勬暟瀛楁瀹瑰櫒
    /// </summary>
    private List<MPGameNumberFrameVertical> m_numberVerticalList;

    /// <summary>
    /// 缃戞牸鏂瑰潡鏁版嵁
    /// </summary>
    private MPGameBlock[][] m_blockGrid2Array;

    /// <summary>
    /// 鎿嶄綔鐨勬渶鍚庝竴涓柟鍧?
    /// </summary>
    private MPGameBlock m_lastBlock;

    /// <summary>
    /// 琛屽垪瀹屾垚鏁伴噺
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

        if (m_isCustomLevel)
        {
            m_size = data.customLevelInfo.Size;
        }
        else
        {
            m_pixel = MPLoad.Load<Texture2D>(m_blockInfo.ID);
            m_size = m_pixel.height;
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

