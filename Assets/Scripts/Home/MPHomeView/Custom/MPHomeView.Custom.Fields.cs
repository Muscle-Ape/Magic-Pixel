using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;
using YooAsset;

public partial class MPHomeView
{
    private const int CUSTOM_GRID_SIZE = 800;
    private const string DEFAULT_CUSTOM_COLOR = "#FF8E64";
    private const float CUSTOM_PALETTE_ANIMATION_DURATION = 0.22f;

    // 用户提供的第四个颜色少一位，这里按相邻配色补全为 #76B443。
    private static readonly string[] CUSTOM_QUICK_COLOR_HEXES =
    {
        "#EF4825",
        "#F88E07",
        "#F4C63B",
        "#76B443",
        "#3CB8F3",
        "#7446D6",
        "#E5427B",
        "#7C4D3B",
    };

    [TransformPath("View/Center/Custom")]
    private RectTransform m_customPage;

    [TransformPath("View/Center/Custom/Content/Grid")]
    private GridLayoutGroup m_customBlockGrid;

    [TransformPath("View/Center/Custom/Content/Input")]
    private RectTransform m_customInput;

    [TransformPath("View/Center/Custom/Content/Frame/Five")]
    private Button m_customSizeFiveBtn;

    [TransformPath("View/Center/Custom/Content/Frame/Five/Frame")]
    private RectTransform m_customSizeFiveFrame;

    [TransformPath("View/Center/Custom/Content/Frame/Ten")]
    private Button m_customSizeTenBtn;

    [TransformPath("View/Center/Custom/Content/Frame/Ten/Frame")]
    private RectTransform m_customSizeTenFrame;

    [TransformPath("View/Center/Custom/Title")]
    private TMP_InputField m_customTitleInput;

    [TransformPath("View/Center/Custom/Btns")]
    private CanvasGroup m_customBtnsCanvasGroup;

    [TransformPath("View/Center/Custom/Btns/PenBtn")]
    private Button m_customPenBtn;

    [TransformPath("View/Center/Custom/Btns/PenBtn/Open")]
    private RectTransform m_customPenOpen;

    [TransformPath("View/Center/Custom/Btns/PenBtn/Text")]
    private TMP_Text m_customPenText;

    [TransformPath("View/Center/Custom/Btns/FillBtn")]
    private Button m_customFillBtn;

    [TransformPath("View/Center/Custom/Btns/FillBtn/Open")]
    private RectTransform m_customFillOpen;

    [TransformPath("View/Center/Custom/Btns/FillBtn/Text")]
    private TMP_Text m_customFillText;

    [TransformPath("View/Center/Custom/Btns/ColorFrame/PaletteBtn")]
    private Button m_customPaletteBtn;

    [TransformPath("View/Center/Custom/Btns/ColorFrame/CurrentColor")]
    private Image m_customCurrentColorImage;

    [TransformPath("View/Center/Custom/PalettePanel")]
    private CanvasGroup m_customPalettePanelCanvasGroup;

    [TransformPath("View/Center/Custom/PalettePanel/CompletedBtn")]
    private Button m_customPaletteCompletedBtn;

    [TransformPath("View/Center/Custom/Pointer")]
    private Image m_customPalettePointer;

    [TransformPath("View/Center/Custom/Btns/SaveBtn")]
    private Button m_customSaveBtn;

    [TransformPath("View/Center/Custom/Btns/PublishBtn")]
    private Button m_customPublishBtn;

    [TransformPath("View/Center/Custom/Btns/PublishBtn/Text")]
    private TMP_Text m_customPublishText;

    [TransformPath("View/Center/Custom/Btns/WarehouseBtn")]
    private Button m_customWarehouseBtn;

    [TransformPath("View/Center/Custom/Btns/CommunityBtn")]
    private Button m_customCommunityBtn;

    [TransformPath("View/Center/Custom/AnimationNode")]
    private RectTransform m_customAnimationNode;

    [TransformPath("View/Center/Custom/AnimationNode/Picture")]
    private Image m_customAnimationPicture;

    private MPCustomBlock m_customBlockPrefab;
    private ObjectPool<MPCustomBlock> m_customBlockPool;
    private List<MPCustomBlock> m_customBlocks;
    private readonly List<RaycastResult> m_customRayResults = new List<RaycastResult>();
    private readonly List<MPCustomBlock> m_customDragBlocks = new List<MPCustomBlock>();
    private readonly List<CustomQuickColorBinding> m_customQuickColorBindings = new List<CustomQuickColorBinding>();

    private EventTrigger m_customInputEventTrigger;
    private readonly List<EventTrigger.Entry> m_customInputEventEntries = new List<EventTrigger.Entry>();
    private MPPalette m_customPalette;
    private Button m_customPalettePointerBtn;
    private Sequence m_customPaletteSequence;
    private Sequence m_customSaveAnimationSequence;
    private CancellationTokenSource m_customPublishCancellation;
    private MPCustomLevelInfo m_customPendingPublishLevelInfo;
    private Texture2D m_customSaveAnimationTexture;
    private Sprite m_customSaveAnimationSprite;
    private Sprite m_customBlockLeftTopSprite;
    private Sprite m_customBlockRightTopSprite;
    private Sprite m_customBlockLeftDownSprite;
    private Sprite m_customBlockRightDownSprite;
    private Color m_customCurrentColor = Color.white;
    private Vector2 m_customAnimationNodeStartPosition;
    private Vector3 m_customWarehouseStartScale = Vector3.one;
    private int m_customCurrentSize = 5;
    private bool m_customIsFillMode;
    private bool m_customIsTenSize;
    private bool m_customIsClear;
    private bool m_customPaletteOpen;
    private bool m_customPublishActionRunning;
    private bool m_customInitialized;

    private sealed class CustomQuickColorBinding
    {
        public Button Button;
        public UnityEngine.Events.UnityAction Callback;
    }

    private void InitializeCustomEditor()
    {
        if (m_customInitialized)
            return;

        try
        {
            GameObject prefab = MPLoad.Load<GameObject>("MPCustomBlock", this);
            m_customBlockPrefab = prefab == null ? null : prefab.GetComponent<MPCustomBlock>();
        }
        catch (Exception exception)
        {
            Debug.LogError($"主页自定义方块预制体加载失败：{exception.Message}");
            return;
        }

        if (m_customBlockPrefab == null || m_customBlockGrid == null || m_customInput == null)
        {
            Debug.LogError("MPHomeView 的 Custom 页面缺少方块预制体、Grid 或 Input 节点。");
            return;
        }

        m_customBlockLeftTopSprite = LoadOptionalCustomBlockFrameSprite("game_block_lt");
        m_customBlockRightTopSprite = LoadOptionalCustomBlockFrameSprite("game_block_rt");
        m_customBlockLeftDownSprite = LoadOptionalCustomBlockFrameSprite("game_block_ld");
        m_customBlockRightDownSprite = LoadOptionalCustomBlockFrameSprite("game_block_rd");

        m_customInitialized = true;
        m_customBlocks = new List<MPCustomBlock>();
        m_customBlockPool = new ObjectPool<MPCustomBlock>(
            CreateCustomBlock,
            GetCustomBlock,
            ReleaseCustomBlock,
            DestroyCustomBlock,
            collectionCheck: false,
            defaultCapacity: 25,
            maxSize: 100);

        m_customIsFillMode = false;
        m_customIsTenSize = false;
        m_customPublishActionRunning = false;
        m_customPendingPublishLevelInfo = null;
        m_customCurrentSize = 5;
        if (m_customTitleInput != null)
            m_customTitleInput.text = MPUser.instance.GetDefaultCustomLevelTitle();

        RegisterCustomUI();
        RegisterCustomInput();
        InitializeCustomPalette();
        InitializeCustomSaveAnimation();
        CreateCustomGrid(5);
        RefreshCustomModeState();
        RefreshCustomSizeState();
        RefreshCustomPublishButtonState();
        CloseCustomPalette(false);
    }

    private void RefreshCustomEditorFocus()
    {
        if (!m_customInitialized)
            return;

        RefreshCustomPublishButtonState();
    }

    private void ReleaseCustomEditor()
    {
        if (!m_customInitialized)
            return;

        m_customInitialized = false;
        UnregisterCustomUI();
        UnregisterCustomInput();
        CancelCustomPublishOperation();
        m_customPublishActionRunning = false;
        m_customPendingPublishLevelInfo = null;
        CloseCustomPalette(false);
        ClearCustomSaveAnimation();
        ClearCustomGrid();
        m_customBlockPool?.Clear();
        m_customBlockPool = null;
        m_customBlocks = null;
        m_customBlockPrefab = null;
        m_customBlockLeftTopSprite = null;
        m_customBlockRightTopSprite = null;
        m_customBlockLeftDownSprite = null;
        m_customBlockRightDownSprite = null;
        m_customDragBlocks.Clear();
        m_customRayResults.Clear();
    }

    private Sprite LoadOptionalCustomBlockFrameSprite(string location)
    {
        if (!YooAssets.CheckLocationValid(location))
        {
            Debug.LogWarning($"自定义编辑器外框资源不存在或尚未加入 YooAsset 清单：{location}");
            return null;
        }

        return MPLoad.Load<Sprite>(location, this);
    }
}
