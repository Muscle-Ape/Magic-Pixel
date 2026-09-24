using System;
using System.Globalization;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>进入主线/大图前选择继续缓存或重新开始；关闭不会修改缓存。</summary>
[Component("MPNewGamePop")]
public sealed class MPNewGamePop : AWindow
{
    // 与主游戏、大图模式及缓存校验的三点生命上限一致。
    private const int MAX_LIVES = 3;
    [TransformPath("View/Window/Level/Text")] private TMP_Text m_levelText;
    [TransformPath("View/Window/Items/Mode/Info")] private TMP_Text m_mode;
    [TransformPath("View/Window/Items/Progress/Info")] private TMP_Text m_progress;
    [TransformPath("View/Window/Items/RemainingHP/Info")] private TMP_Text m_remainingHP;
    [TransformPath("View/Window/Items/Pey/Info")] private TMP_Text m_pet;
    [TransformPath("View/Window/Items/SkillNumber/Info")] private TMP_Text m_skillNumber;
    [TransformPath("View/Window/ContinueBtn")] private Button m_continueBtn;
    [TransformPath("View/Window/RestartBtn")] private Button m_restartBtn;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeBtn;

    private static MPNewGamePop s_active;
    private MPNewGamePopUIMsgData m_data;
    private bool m_resolved;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public override void OnCreate()
    {
        m_continueBtn.onClick.AddListener(OnContinue);
        m_restartBtn.onClick.AddListener(OnRestart);
        m_closeBtn.onClick.AddListener(OnClose);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg as MPNewGamePopUIMsgData;
        s_active = this;
        m_resolved = false;
        RefreshDetails();
        SetButtons(m_data != null && m_data.cache != null);
        m_continueBtn.interactable &= m_data?.continueAction != null;
        m_restartBtn.interactable &= m_data?.restartAction != null;
        m_closeBtn.interactable = true;
    }

    public static void EnterMainLevel(MPGameViewUIMsgData data, AWindow sourceWindow = null, bool closeSource = false)
    {
        if (data == null || data.blockInfo == null || (s_active != null && !s_active.IsDestoried))
            return;
        // VIP 只在真正进入某个锁定关卡时永久解锁该关，不会一次性解锁全部关卡。
        if (!MPUser.instance.TryUnlockMainLevelWithVip(data.blockInfo.ID))
            return;
        if (!MPNoNetworkPop.CheckLevelEntry(sourceWindow, () => EnterMainLevel(data, sourceWindow, closeSource)))
            return;

        MPLevelProgressCacheInfo cache = ReadValidCache(data.blockInfo.ID, false, out int size);
        Action enter = () => Enter<MPGameView>(data, sourceWindow, closeSource);
        data.progressCacheValidated = true;
        data.progressCache = cache;
        if (cache == null)
        {
            enter();
            return;
        }

        ShowChoice(new MPNewGamePopUIMsgData
        {
            levelTitle = $"Level {data.index + 1}", size = size, cache = cache,
            continueAction = enter,
            restartAction = () => Enter<MPGameView>(data, sourceWindow, closeSource, () =>
            {
                MPUser.instance.ClearMainLevelProgressCache(data.blockInfo.ID);
                data.progressCache = null;
            }),
            cancelAction = closeSource && sourceWindow != null ? sourceWindow.DestroyWindow : (Action)null,
        });
    }

    public static void EnterLargeImageLevel(MPLargeImageGameViewUIMsgData data, AWindow sourceWindow = null, bool closeSource = false)
    {
        if (data == null || data.blockInfo == null || (s_active != null && !s_active.IsDestoried))
            return;
        if (!MPUser.instance.TryUnlockLargeImageLevelWithVip(data.blockInfo.ID))
            return;
        if (!MPNoNetworkPop.CheckLevelEntry(sourceWindow, () => EnterLargeImageLevel(data, sourceWindow, closeSource)))
            return;

        MPLevelProgressCacheInfo cache = ReadValidCache(data.blockInfo.ID, true, out int size);
        Action enter = () => Enter<MPLargeImageGameView>(data, sourceWindow, closeSource);
        data.progressCacheValidated = true;
        data.progressCache = cache;
        if (cache == null)
        {
            enter();
            return;
        }

        ShowChoice(new MPNewGamePopUIMsgData
        {
            levelTitle = data.blockInfo.Name, size = size, cache = cache, isLargeImage = true,
            continueAction = enter,
            restartAction = () => Enter<MPLargeImageGameView>(data, sourceWindow, closeSource, () =>
            {
                MPUser.instance.ClearLargeImageLevelProgressCache(data.blockInfo.ID);
                data.progressCache = null;
            }),
            cancelAction = closeSource && sourceWindow != null ? sourceWindow.DestroyWindow : (Action)null,
        });
    }

    /// <summary>自定义/社区没有未完成缓存，也需要走相同的关卡联网检查。</summary>
    public static void EnterCustomLevel(MPGameViewUIMsgData data, AWindow sourceWindow = null, bool closeSource = false)
    {
        if (data == null || data.customLevelInfo == null || !data.isCustomLevel)
            return;
        Enter<MPGameView>(data, sourceWindow, closeSource);
    }

    private static MPLevelProgressCacheInfo ReadValidCache(string levelId, bool largeImage, out int size)
    {
        size = 0;
        MPLevelProgressCacheInfo cache = largeImage
            ? MPUser.instance.GetLargeImageLevelProgressCache(levelId)
            : MPUser.instance.GetMainLevelProgressCache(levelId);
        if (cache == null)
            return null;

        try
        {
            using (MPAssetLoadLease<Texture2D> texture = MPLoad.LoadLease<Texture2D>(levelId))
            {
                size = texture.Asset == null ? 0 : texture.Asset.height;
            }
            return cache.GetValidIncompleteCopy(size, largeImage, MAX_LIVES);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPNewGamePop] 无法校验关卡缓存：{exception.Message}");
            return null;
        }
    }

    private static void ShowChoice(MPNewGamePopUIMsgData data)
    {
        s_active = UIManager.Inst.ShowWindow<MPNewGamePop>(data, true, UILayer.Top);
    }

    private static void Enter<T>(UIMsgData data, AWindow source, bool closeSource, Action beforeEnter = null) where T : AWindow
    {
        // 选择继续/重开时可能已经断网，必须在清理缓存、关闭原页面之前再次检查。
        if (!MPNoNetworkPop.CheckLevelEntry(source, () => Enter<T>(data, source, closeSource, beforeEnter)))
            return;

        T targetWindow = null;
        MPTransitionView.Play(
            () =>
            {
                if (!ReferenceEquals(source, null) && (source == null || source.IsDestoried))
                    return;

                beforeEnter?.Invoke();
                if (closeSource && source != null)
                    source.DestroyWindow();
                targetWindow = UIManager.Inst.ShowWindow<T>(data, true);
                if (!closeSource && source != null && !source.IsDestoried)
                    source.LostFocus(false);
            },
            () =>
            {
                // 完全移除过渡页后再通知主游戏开始入场动画，不依赖固定等待时间。
                if (targetWindow is MPGameViewBase gameView && !gameView.IsDestoried)
                    gameView.PlayEnterAnimationAfterTransition();
            });
    }

    /// <summary>显示缓存对应的模式、进度、剩余生命、宠物及剩余技能次数。</summary>
    private void RefreshDetails()
    {
        MPLevelProgressCacheInfo cache = m_data?.cache;
        MPPetConfig pet = MPDataManager.Instance.m_petsModel?.petConfigs?.Find(
            item => item != null && item.ID == cache?.PetId);
        float size = Mathf.Max(1, m_data?.size ?? 1);
        float rate = Mathf.Clamp01((cache?.CompletedBlocks?.Count ?? 0) / (size * size));
        m_levelText.text = m_data?.levelTitle ?? string.Empty;
        m_mode.text = m_data == null ? "None" : m_data.isLargeImage ? "Big Maps" : "Main Level";
        m_progress.text = Mathf.RoundToInt(rate * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        int remainingHP = cache == null ? 0 : MAX_LIVES - Mathf.Clamp(cache.UsedLoves, 0, MAX_LIVES);
        m_remainingHP.text = remainingHP.ToString(CultureInfo.InvariantCulture);
        m_pet.text = pet == null ? "None" : pet.Name;
        int totalSkills = pet?.SkillUseCount ?? 0;
        int remainingSkills = totalSkills - Mathf.Clamp(cache?.UsedPetSkillCount ?? 0, 0, totalSkills);
        m_skillNumber.text = remainingSkills.ToString(CultureInfo.InvariantCulture);
    }

    private void OnContinue()
    {
        if (m_data?.cache != null && m_data.continueAction != null) Resolve(m_data.continueAction);
    }
    private void OnRestart()
    {
        if (m_data?.cache != null && m_data.restartAction != null) Resolve(m_data.restartAction);
    }
    private void OnClose() => Resolve(m_data?.cancelAction);

    private void Resolve(Action callback)
    {
        if (m_resolved || IsDestoried)
            return;
        m_resolved = true;
        SetButtons(false);
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null)
            animation.Close(callback);
        else
        {
            DestroyWindow();
            callback?.Invoke();
        }
    }

    private void SetButtons(bool enabled)
    {
        m_continueBtn.interactable = enabled;
        m_restartBtn.interactable = enabled;
        m_closeBtn.interactable = enabled;
    }

    public override void OnRelease()
    {
        m_continueBtn.onClick.RemoveListener(OnContinue);
        m_restartBtn.onClick.RemoveListener(OnRestart);
        m_closeBtn.onClick.RemoveListener(OnClose);
        m_data = null;
        if (s_active == this)
            s_active = null;
    }
}

public sealed class MPNewGamePopUIMsgData : UIMsgData
{
    public string levelTitle;
    public int size;
    public bool isLargeImage;
    public MPLevelProgressCacheInfo cache;
    public Action continueAction;
    public Action restartAction;
    public Action cancelAction;
}
