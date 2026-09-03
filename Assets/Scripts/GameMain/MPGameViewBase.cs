using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 数织游戏页面公共基类。
/// 负责共享 Prefab 节点、页面生命周期、基础按钮、生命值、道具和失败流程；
/// 主线与大图模式分别实现网格、输入、提示目标、进度缓存和重开逻辑。
/// </summary>
public abstract class MPGameViewBase : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>
    /// 网格区域固定大小。
    /// </summary>
    protected const int GRID_SIZE = 800;

    // 以下字段统一绑定 MPGameView.prefab 中主游戏和大图模式共用的 UGUI 节点。
    [TransformPath("View")]
    protected CanvasGroup m_viewCanvasGroup;

    [TransformPath("View/Content/Vertical")]
    protected RectTransform m_numberVertical;

    [TransformPath("View/Content/Horizontal")]
    protected RectTransform m_numberHorizontal;

    [TransformPath("View/Content/Grid")]
    protected GridLayoutGroup m_blockGrid;

    [TransformPath("View/Content/Frame")]
    protected Image m_contentFrame;

    [TransformPath("View/Content/CompletedFrame")]
    protected Image m_completedFrame;

    [TransformPath("View/Content/Line")]
    protected RectTransform m_lineNode;

    [TransformPath("View/Content/Input")]
    protected RectTransform m_input;

    [TransformPath("View/ModeSwitch")]
    protected Button m_modeSwitchFrame;

    [TransformPath("View/ModeSwitch/Btn")]
    protected RectTransform m_modeSwitchBtn;

    [TransformPath("View/ModeSwitch/Btn/Fill")]
    protected Image m_modeSwitchFill;

    [TransformPath("View/ModeSwitch/Btn/Blank")]
    protected Image m_modeSwitchBlank;

    [TransformPath("View/Head/BackBtn")]
    protected Button m_backBtn;

    [TransformPath("View/Head/SettingBtn")]
    protected Button m_settingBtn;

    [TransformPath("View/Props")]
    protected RectTransform m_props;

    [TransformPath("View/Props/HintBtn")]
    protected Button m_hintPropBtn;

    [TransformPath("View/Props/HintBtn/CountFrame/Count")]
    protected TMP_Text m_hintPropCountText;

    [TransformPath("View/Props/RecoverBtn")]
    protected Button m_loveRecoverPropBtn;

    [TransformPath("View/Props/RecoverBtn/CountFrame/Count")]
    protected TMP_Text m_loveRecoverPropCountText;

    [TransformPath("View/PetSkillBtn")]
    protected Button m_petSkillBtn;

    [TransformPath("View/PetSkillBtn/Icon")]
    protected Image m_petSkillIcon;

    [TransformPath("View/PetSkillBtn/Count")]
    protected TMP_Text m_petSkillCountText;

    /// <summary>进入本关时选中的宠物配置。</summary>
    protected MPPetConfig m_activePetConfig;

    /// <summary>当前关卡剩余的宠物免费技能次数。</summary>
    protected int m_petSkillRemainingUses;

    [TransformPath("View/Title")]
    protected TMP_Text m_titleText;

    [TransformPath("View/Head/Coin/Count")]
    protected TMP_Text m_coinText;

    [TransformPath("View/Head/Diamond/Count")]
    protected TMP_Text m_diamondText;

    [TransformPath("View/Head/PlayerName")]
    protected TMP_Text m_playerNameText;

    [TransformPath("View/Head/Level/Text")]
    protected TMP_Text m_playerLevelText;

    [TransformPath("View/Head/Level/Mask/Fill")]
    protected Image m_playerLevelFill;

    [TransformPath("View/Loves")]
    protected RectTransform m_lovesNode;

    /// <summary>生命图标列表。</summary>
    protected List<GameObject> m_loves;

    /// <summary>当前剩余生命数量。</summary>
    protected int m_lovesCount;

    /// <summary>是否已经打开失败弹窗，避免重复弹出。</summary>
    protected bool m_isFailPopShowing;

    /// <summary>当前关卡在所属列表中的下标。</summary>
    protected int m_index;

    /// <summary>关卡列表刷新回调。</summary>
    protected Action m_refreshAction;

    /// <summary>横向数字提示预制体。</summary>
    protected GameObject m_numberHorizontalPrefab;

    /// <summary>纵向数字提示预制体。</summary>
    protected GameObject m_numberVerticalPrefab;

    /// <summary>当前关卡对应的像素图。</summary>
    protected Texture2D m_pixel;

    /// <summary>完整关卡的行列尺寸。</summary>
    protected int m_size;

    /// <summary>输入射线检测结果缓存，避免拖拽过程中重复创建列表。</summary>
    protected readonly List<RaycastResult> m_rayResults = new List<RaycastResult>();

    /// <summary>上一次指针位置。</summary>
    protected Vector2 m_pointerLastPosition;

    /// <summary>拖拽过程中触发下一次格子检测所需的最小距离。</summary>
    protected float m_detectionInterval;

    /// <summary>一次连续拖拽锁定的横向或纵向方向。</summary>
    protected Vector2 m_fixedDragDir = Vector2.zero;

    /// <summary>当前连续拖拽是否允许继续处理后续格子。</summary>
    protected bool m_canDragContinue;

    /// <summary>填充/标记模式切换动画。</summary>
    protected Tween m_modeSwitchTween;

    /// <summary>已经完成的行列总数。</summary>
    protected int m_hvCompleted;

    /// <summary>是否正在恢复缓存，恢复期间不触发通关结算。</summary>
    protected bool m_isRestoringProgress;

    /// <summary>当前关卡是否已经完成，完成后不再保存进度。</summary>
    protected bool m_hasCompleted;

    private MPSecondConfirmationPop m_exitConfirmation;
    private MPGameFailPop m_failPop;
    private bool m_isReturningToLevelList;

    /// <summary>模式切换滑块相对中心点的移动距离。</summary>
    private float m_modeSwitchDistance = 78;

    /// <summary>
    /// 当前模式是否使用生命值。自定义关卡会关闭生命与道具。
    /// </summary>
    protected abstract bool UsesLives { get; }

    /// <summary>
    /// 当前是否处于填充模式。
    /// </summary>
    protected abstract bool IsFillMode { get; }

    /// <summary>
    /// 页面标题。
    /// </summary>
    protected abstract string LevelTitle { get; }

    /// <summary>
    /// 当前模式是否显示并允许使用道具，默认与生命值规则保持一致。
    /// </summary>
    protected virtual bool UsesProps => UsesLives;

    /// <summary>退出提示必须与模式实际的缓存能力一致。</summary>
    protected virtual string ExitProgressNotice =>
        "Your puzzle progress, remaining lives and pet skill usage will be saved. You can continue this level later.";

    /// <summary>解析页面消息并保存当前模式的关卡数据。</summary>
    protected abstract void LoadLevelData(UIMsgData uiMsg);

    /// <summary>加载当前模式专属的方块预制体和关卡图片。</summary>
    protected abstract void LoadLevelAssets();

    /// <summary>创建当前模式的可操作网格。</summary>
    protected abstract void CreateGrid();

    /// <summary>创建顶部横向数字提示。</summary>
    protected abstract void CreateHorizontalNumber();

    /// <summary>创建左侧纵向数字提示。</summary>
    protected abstract void CreateVerticalNumber();

    /// <summary>创建网格分隔线。</summary>
    protected abstract void CreateLine();

    /// <summary>注册当前模式的格子输入事件。</summary>
    protected abstract void RegisterInput();

    /// <summary>恢复当前关卡的模式专属进度缓存。</summary>
    protected abstract void RestoreProgressCache();

    /// <summary>保存当前关卡的模式专属进度缓存。</summary>
    protected abstract void SaveProgressCache();

    /// <summary>清理当前关卡的模式专属进度缓存。</summary>
    protected abstract void ClearProgressCache();

    /// <summary>判断当前模式是否存在可以使用提示道具的目标。</summary>
    protected abstract bool HasHintTarget();

    /// <summary>按照当前模式规则完成一个提示目标。</summary>
    protected abstract void CompleteHintTarget();

    /// <summary>切换当前模式的填充/标记状态。</summary>
    protected abstract void ToggleInputMode();

    /// <summary>把最新输入模式同步给当前可操作方块。</summary>
    protected abstract void ApplyInputModeToBlocks();

    /// <summary>按照当前模式的数据类型重新打开本关。</summary>
    protected abstract void RestartLevel();

    protected abstract void ApplyFillColorToBlocks(Color color);

    /// <summary>
    /// 模式专属 UI 注册入口。大图模式在这里注册数字栏拖拽。
    /// </summary>
    protected virtual void RegisterModeSpecificUI()
    {
    }

    /// <summary>
    /// 模式专属布局刷新入口。自定义关卡用它居中模式切换按钮。
    /// </summary>
    protected virtual void RefreshModeSpecificLayout()
    {
    }

    /// <summary>
    /// 返回上一层后执行的模式专属操作。
    /// </summary>
    protected virtual void OnReturnedToLevelList()
    {
    }

    /// <summary>
    /// 失败退出后执行的模式专属操作。
    /// </summary>
    protected virtual void OnFailExited()
    {
    }

    /// <summary>
    /// 页面释放时清理模式专属运行时资源。
    /// </summary>
    protected virtual void ReleaseModeSpecificResources()
    {
    }

    /// <summary>
    /// 游戏页统一初始化模板。固定公共执行顺序，差异步骤交由派生类实现。
    /// </summary>
    public sealed override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPLoad.ReleaseAll(this);
        m_hasCompleted = false;
        m_isRestoringProgress = false;
        m_isFailPopShowing = false;
        m_isReturningToLevelList = false;
        m_hvCompleted = 0;

        LoadLevelData(uiMsg);
        InitializePetSkillSession();
        LoadSharedAssets();
        InitializeLives();
        LoadLevelAssets();

        if (m_size <= 0)
        {
            throw new InvalidOperationException($"{GetType().Name} 关卡尺寸无效：{m_size}");
        }

        m_detectionInterval = GRID_SIZE / (float)m_size * (Screen.height / 2338f) * 0.9f;

        CreateGrid();
        CreateHorizontalNumber();
        CreateVerticalNumber();
        CreateLine();
        RegisterCommonUI();
        RegisterInput();
        RestoreProgressCache();

        MPAudioManager.Instance.StopBGM(MPMusic.MPBGMMain);
        if (UsesLives && m_lovesCount <= 0)
            OpenFailPop();
    }

    /// <summary>加载两个模式共用的数字提示预制体。</summary>
    private void LoadSharedAssets()
    {
        m_numberHorizontalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameHorizontal", this);
        m_numberVerticalPrefab = MPLoad.Load<GameObject>("MPGameNumberFrameVertical", this);
    }

    /// <summary>从共享 Prefab 收集生命图标，并按模式决定是否显示生命和道具。</summary>
    private void InitializeLives()
    {
        m_loves = new List<GameObject>();
        if (m_lovesNode != null)
        {
            for (int i = 0; i < m_lovesNode.childCount; i++)
            {
                Transform loveRoot = m_lovesNode.GetChild(i);
                if (loveRoot.childCount > 0)
                {
                    m_loves.Add(loveRoot.GetChild(0).gameObject);
                }
            }

            m_lovesNode.gameObject.SetActive(UsesLives);
        }

        m_lovesCount = m_loves.Count;
        if (m_props != null)
        {
            m_props.gameObject.SetActive(UsesProps);
        }
    }

    /// <summary>注册共享按钮事件并刷新共享界面数据。</summary>
    private void RegisterCommonUI()
    {
        if (m_modeSwitchFrame != null)
        {
            RectTransform modeSwitchRect = m_modeSwitchFrame.transform as RectTransform;
            // m_modeSwitchDistance = modeSwitchRect == null ? 0f : modeSwitchRect.rect.width / 4f;
            m_modeSwitchFrame.onClick.RemoveListener(OnModeSwitchClick);
            m_modeSwitchFrame.onClick.AddListener(OnModeSwitchClick);
        }

        RegisterButton(m_backBtn, OnBackClick);
        RegisterButton(m_settingBtn, OnSettingClick);
        RegisterButton(m_hintPropBtn, OnHintPropClick);
        RegisterButton(m_loveRecoverPropBtn, OnLoveRecoverPropClick);
        RegisterButton(m_petSkillBtn, OnPetSkillClick);

        RefreshModeSpecificLayout();
        RegisterModeSpecificUI();
        RefreshHead();
        RefreshPropButtons();

        if (m_titleText != null)
        {
            m_titleText.text = LevelTitle;
        }
    }

    private static void RegisterButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    /// <summary>刷新顶部玩家信息、主线进度和资源数量。</summary>
    protected void RefreshHead()
    {
        if (m_coinText != null)
        {
            m_coinText.text = MPUser.instance.GetCoins().ToString();
        }

        if (m_diamondText != null)
        {
            m_diamondText.text = MPUser.instance.GetDiamond().ToString();
        }

        if (m_playerNameText != null)
        {
            string playerName = MPLoginManager.Instance.PlayerName;
            m_playerNameText.text = string.IsNullOrWhiteSpace(playerName)
                ? "Player"
                : playerName;
        }

        int levelCount = MPDataManager.Instance.m_mainLevelModel?.blockInfos?.Count ?? 0;
        int latestLevelIndex = levelCount > 0
            ? Mathf.Clamp(MPUser.instance.GetMainLevlPassIndex(), 0, levelCount - 1)
            : 0;
        if (m_playerLevelText != null)
        {
            m_playerLevelText.text = $"LEVEL {latestLevelIndex + 1}";
        }

        if (m_playerLevelFill != null)
        {
            m_playerLevelFill.fillAmount = levelCount <= 1
                ? 0f
                : latestLevelIndex / (float)(levelCount - 1);
        }
    }

    public override void OnFocus(bool focus)
    {
        if (focus)
        {
            RefreshHead();
        }
    }

    /// <summary>刷新提示道具和生命恢复道具数量。</summary>
    protected void RefreshPropButtons()
    {
        if (m_hintPropCountText != null)
        {
            m_hintPropCountText.text = MPUser.instance.GetHintProps().ToString();
        }

        if (m_loveRecoverPropCountText != null)
        {
            m_loveRecoverPropCountText.text = MPUser.instance.GetLoveRecoverProps().ToString();
        }

        RefreshPetSkillButton();
    }

    private void InitializePetSkillSession()
    {
        m_activePetConfig = MPUser.instance.GetSelectedPetConfig();
        m_petSkillRemainingUses = m_activePetConfig == null
            ? 0
            : m_activePetConfig.SkillUseCount;

        if (m_petSkillIcon != null)
            m_petSkillIcon.sprite = null;
    }

    private void RefreshPetSkillButton()
    {
        if (m_petSkillBtn == null)
            return;

        bool hasSkill = m_activePetConfig != null
            && !string.IsNullOrEmpty(m_activePetConfig.Option)
            && m_activePetConfig.SkillUseCount > 0;
        m_petSkillBtn.gameObject.SetActive(hasSkill);
        if (!hasSkill)
            return;

        if (m_petSkillCountText != null)
            m_petSkillCountText.text = Mathf.Max(0, m_petSkillRemainingUses).ToString();

        m_petSkillBtn.interactable = !m_hasCompleted
            && m_petSkillRemainingUses > 0
            && CanExecutePetSkill();
        if (m_petSkillIcon == null
            || m_petSkillIcon.sprite != null
            || string.IsNullOrWhiteSpace(m_activePetConfig.Icon))
            return;

        try
        {
            Sprite sprite = MPLoad.Load<Sprite>(m_activePetConfig.Icon, this);
            if (sprite != null)
            {
                m_petSkillIcon.sprite = sprite;
                m_petSkillIcon.preserveAspect = true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"局内宠物图标加载失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 消费当前宠物提供的本局技能次数。
    /// </summary>
    private bool TryConsumePetSkill()
    {
        if (m_activePetConfig == null || m_petSkillRemainingUses <= 0)
            return false;

        m_petSkillRemainingUses--;
        return true;
    }

    private bool CanExecutePetSkill()
    {
        if (m_activePetConfig == null)
            return false;

        switch (m_activePetConfig.Option)
        {
            case MPPetSkillOption.Hint:
                return HasHintTarget();
            case MPPetSkillOption.RecoverLife:
                return UsesLives && m_loves != null && m_lovesCount < m_loves.Count;
            default:
                return false;
        }
    }

    /// <summary>从关卡缓存恢复本次游戏已经使用的宠物技能次数。</summary>
    protected void RestorePetSkillUsage(string petId, int usedCount)
    {
        // 继续旧局时使用缓存中的宠物，而非主页后来选中的宠物，避免技能次数被重置。
        m_activePetConfig = MPDataManager.Instance.m_petsModel?.petConfigs?.Find(
            config => config != null && config.ID == petId);
        if (m_petSkillIcon != null)
            m_petSkillIcon.sprite = null;
        int totalUses = m_activePetConfig == null ? 0 : m_activePetConfig.SkillUseCount;
        m_petSkillRemainingUses = totalUses - Mathf.Clamp(usedCount, 0, totalUses);
        RefreshPropButtons();
    }

    /// <summary>把当前宠物及已使用次数写入关卡缓存。</summary>
    protected void WritePetSkillUsage(MPLevelProgressCacheInfo cacheInfo)
    {
        if (cacheInfo == null || m_activePetConfig == null)
            return;

        cacheInfo.PetId = m_activePetConfig.ID;
        cacheInfo.UsedPetSkillCount = Mathf.Clamp(
            m_activePetConfig.SkillUseCount - m_petSkillRemainingUses,
            0,
            m_activePetConfig.SkillUseCount);
    }

    /// <summary>扣除一点生命，并在生命耗尽时打开失败弹窗。</summary>
    protected void SubLoves()
    {
        if (!UsesLives || m_loves == null || m_lovesCount <= 0)
            return;

        m_lovesCount = Mathf.Max(0, m_lovesCount - 1);
        GameObject love = m_loves[m_lovesCount];
        love.transform.DOKill();
        love.transform.localScale = Vector3.one;
        love.SetActive(false);

        SaveProgressCache();
        RefreshPropButtons();

        if (m_lovesCount <= 0)
        {
            OpenFailPop();
        }
    }

    /// <summary>恢复一点生命并播放生命图标动画。</summary>
    protected void AddLoves()
    {
        if (!UsesLives || m_loves == null || m_lovesCount >= m_loves.Count)
            return;

        GameObject love = m_loves[m_lovesCount];
        love.transform.DOKill();
        love.transform.localScale = Vector3.zero;
        love.SetActive(true);
        love.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetLink(love);
        m_lovesCount++;

        SaveProgressCache();
        RefreshPropButtons();
    }

    /// <summary>根据缓存中已消耗的生命数量恢复生命显示。</summary>
    protected void RestoreLoves(int usedLoves)
    {
        if (m_loves == null)
            return;

        usedLoves = Mathf.Clamp(usedLoves, 0, m_loves.Count);
        m_lovesCount = m_loves.Count - usedLoves;

        for (int i = 0; i < m_loves.Count; i++)
        {
            m_loves[i].transform.DOKill();
            m_loves[i].transform.localScale = Vector3.one;
            m_loves[i].SetActive(i < m_lovesCount);
        }
    }

    private void OnHintPropClick()
    {
        if (!UsesProps || !HasHintTarget())
        {
            RefreshPropButtons();
            return;
        }

        if (!MPUser.instance.UseHintProp())
        {
            RefreshPropButtons();
            return;
        }

        CompleteHintTarget();
        SaveProgressCache();
        RefreshPropButtons();
    }

    /// <summary>
    /// 独立宠物技能按钮。宠物次数与提示、生命恢复道具完全分开计算。
    /// </summary>
    private void OnPetSkillClick()
    {
        if (m_activePetConfig == null || m_petSkillRemainingUses <= 0 || m_hasCompleted)
        {
            RefreshPropButtons();
            return;
        }

        switch (m_activePetConfig.Option)
        {
            case MPPetSkillOption.Hint:
                if (!HasHintTarget() || !TryConsumePetSkill())
                {
                    RefreshPropButtons();
                    return;
                }

                CompleteHintTarget();
                SaveProgressCache();
                break;
            case MPPetSkillOption.RecoverLife:
                if (!UsesLives
                    || m_loves == null
                    || m_lovesCount >= m_loves.Count
                    || !TryConsumePetSkill())
                {
                    RefreshPropButtons();
                    return;
                }

                AddLoves();
                break;
            default:
                Debug.LogWarning($"未实现的宠物技能 option：{m_activePetConfig.Option}");
                break;
        }

        RefreshPropButtons();
    }

    private void OnLoveRecoverPropClick()
    {
        if (!UsesProps || m_loves == null || m_lovesCount >= m_loves.Count)
        {
            RefreshPropButtons();
            return;
        }

        if (!MPUser.instance.UseLoveRecoverProp())
        {
            RefreshPropButtons();
            return;
        }

        AddLoves();
    }

    /// <summary>打开共用的游戏失败弹窗。</summary>
    private void OpenFailPop()
    {
        if (!UsesLives || m_isFailPopShowing || m_hasCompleted)
            return;

        m_isFailPopShowing = true;
        MPGameFailPopUIMsgData data = new MPGameFailPopUIMsgData()
        {
            exitAction = OnFailExitClick,
            replayAction = OnFailReplayClick,
            restoreLifeAction = OnFailRestoreLifeClick,
        };

        m_failPop = UIManager.Inst.ShowWindow<MPGameFailPop>(data, true, UILayer.Top);
    }

    private void OnFailExitClick()
    {
        if (this == null || IsDestoried || m_isReturningToLevelList)
            return;

        // 失败弹窗已经完成退出确认，这里只执行一次原有的弃局逻辑。
        m_isReturningToLevelList = true;
        m_failPop = null;
        m_isFailPopShowing = false;
        m_hasCompleted = true;
        ClearProgressCache();
        DestroyWindow();
        OnFailExited();
    }

    private void OnFailReplayClick()
    {
        if (this == null || IsDestoried || m_isReturningToLevelList)
            return;
        if (!MPNoNetworkPop.CheckLevelEntry(this, OnFailReplayClick))
            return;

        m_isReturningToLevelList = true;
        m_failPop = null;
        m_isFailPopShowing = false;
        m_hasCompleted = true;
        ClearProgressCache();
        RestartLevel();
    }

    private bool OnFailRestoreLifeClick()
    {
        if (!UsesLives || m_loves == null || m_lovesCount >= m_loves.Count)
        {
            RefreshPropButtons();
            return false;
        }

        if (!MPUser.instance.UseLoveRecoverProp())
        {
            RefreshPropButtons();
            return false;
        }

        AddLoves();
        m_failPop = null;
        m_isFailPopShowing = false;
        return true;
    }

    /// <summary>切换填充/标记模式，并把结果同步给派生类方块。</summary>
    private void OnModeSwitchClick()
    {
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);

        ToggleInputMode();
        m_modeSwitchTween?.Kill();
        if (m_modeSwitchBtn != null)
        {
            m_modeSwitchTween = m_modeSwitchBtn
                .DOAnchorPosX(IsFillMode ? m_modeSwitchDistance : -m_modeSwitchDistance, 0.1f)
                .SetEase(Ease.Linear);
        }

        if (m_modeSwitchFill != null)
        {
            m_modeSwitchFill.gameObject.SetActive(IsFillMode);
        }

        if (m_modeSwitchBlank != null)
        {
            m_modeSwitchBlank.gameObject.SetActive(!IsFillMode);
        }

        ApplyInputModeToBlocks();
    }

    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(new MPSettingPopUIMsgData
        {
            isInGame = !m_hasCompleted,
            levelTitle = LevelTitle,
            replayAction = RestartFromSettings,
            fillColorChanged = color =>
            {
                if (this != null && !IsDestoried && !m_hasCompleted)
                    ApplyFillColorToBlocks(color);
            },
        }, true, UILayer.Top);
    }

    private void RestartFromSettings()
    {
        if (this == null || IsDestoried || m_hasCompleted)
            return;
        if (!MPNoNetworkPop.CheckLevelEntry(this, RestartFromSettings))
            return;

        // OnRelease 不能把旧局再次存回刚清除的缓存。
        ClearProgressCache();
        m_hasCompleted = true;
        m_isFailPopShowing = false;
        RestartLevel();
    }

    /// <summary>先确认退出，取消时保持当前局与缓存不变。</summary>
    private void OnBackClick()
    {
        if (this == null || IsDestoried || m_hasCompleted || m_isFailPopShowing || m_isReturningToLevelList
            || (m_exitConfirmation != null && !m_exitConfirmation.IsDestoried))
            return;

        m_exitConfirmation = MPSecondConfirmationPop.Show(
            "Leave level?",
            $"{LevelTitle}\n{ExitProgressNotice}",
            "Exit",
            token =>
            {
                if (token.IsCancellationRequested || this == null || IsDestoried
                    || m_hasCompleted || m_isReturningToLevelList)
                    return Task.FromResult(false);

                // 存档失败时由确认弹窗保持显示，不能先切走页面。
                SaveProgressCache();
                return Task.FromResult(true);
            },
            onCancel: () => m_exitConfirmation = null,
            cancelText: "Continue playing",
            onConfirmed: ReturnToLevelList);
    }

    /// <summary>确认弹窗完全关闭之后再转场，防止两个关闭动画与页面焦点冲突。</summary>
    private void ReturnToLevelList()
    {
        m_exitConfirmation = null;
        if (this == null || IsDestoried || m_hasCompleted || m_isReturningToLevelList)
            return;

        m_isReturningToLevelList = true;
        if (m_viewCanvasGroup != null)
            m_viewCanvasGroup.interactable = false;

        MPTransitionView.Play(() =>
        {
            if (this == null || IsDestoried)
                return;

            DestroyWindow();
            OnReturnedToLevelList();
            MPAudioManager.Instance.PlayBGM(MPMusic.MPBGMMain);
        });
    }

    /// <summary>页面释放时保存进度、清理 Tween 和当前页面持有的资源。</summary>
    public override void OnRelease()
    {
        MPNoNetworkPop.DismissLevelEntry(this);
        m_isReturningToLevelList = true;
        if (m_exitConfirmation != null && !m_exitConfirmation.IsDestoried)
            m_exitConfirmation.DestroyWindow();
        m_exitConfirmation = null;
        if (m_failPop != null && !m_failPop.IsDestoried)
            m_failPop.DestroyWindow();
        m_failPop = null;
        SaveProgressCache();
        m_modeSwitchTween?.Kill();
        UnregisterCommonUI();
        ReleaseModeSpecificResources();
        MPLoad.ReleaseAll(this);
    }

    /// <summary>移除公共按钮事件，避免页面关闭后保留无效回调。</summary>
    private void UnregisterCommonUI()
    {
        if (m_modeSwitchFrame != null)
        {
            m_modeSwitchFrame.onClick.RemoveListener(OnModeSwitchClick);
        }

        UnregisterButton(m_backBtn, OnBackClick);
        UnregisterButton(m_settingBtn, OnSettingClick);
        UnregisterButton(m_hintPropBtn, OnHintPropClick);
        UnregisterButton(m_loveRecoverPropBtn, OnLoveRecoverPropClick);
        UnregisterButton(m_petSkillBtn, OnPetSkillClick);
    }

    private static void UnregisterButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(callback);
        }
    }

    /// <summary>游戏进入后台时保存当前关卡进度。</summary>
    protected virtual void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            SaveProgressCache();
        }
    }

    /// <summary>应用退出前保存当前关卡进度。</summary>
    protected virtual void OnApplicationQuit()
    {
        SaveProgressCache();
    }
}
