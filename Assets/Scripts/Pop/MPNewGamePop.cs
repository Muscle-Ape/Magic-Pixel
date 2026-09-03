using System;
using System.Text;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>进入主线/大图前选择继续缓存或重新开始；关闭不会修改缓存。</summary>
[Component("MPNewGamePop")]
public sealed class MPNewGamePop : AWindow
{
    [TransformPath("View/Window/Title")] private TMP_Text m_title;
    [TransformPath("View/Window/Desc")] private TMP_Text m_desc;
    [TransformPath("View/Window/Details")] private TMP_Text m_details;
    [TransformPath("View/Window/ContinueBtn")] private Button m_continueBtn;
    [TransformPath("View/Window/RestartBtn")] private Button m_restartBtn;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeBtn;

    private static MPNewGamePop s_active;
    private MPNewGamePopUIMsgData m_data;
    private bool m_resolved;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg as MPNewGamePopUIMsgData;
        s_active = this;
        m_resolved = false;
        m_continueBtn.onClick.AddListener(OnContinue);
        m_restartBtn.onClick.AddListener(OnRestart);
        m_closeBtn.onClick.AddListener(OnClose);
        m_title.text = "Saved Game";
        m_desc.text = m_data == null ? "Return to the level list." : m_data.levelTitle;
        m_details.text = BuildDetails(m_data);
        SetButtons(m_data != null && m_data.cache != null);
        m_closeBtn.interactable = true;
    }

    public static void EnterMainLevel(MPGameViewUIMsgData data, AWindow sourceWindow = null, bool closeSource = false)
    {
        if (data == null || data.blockInfo == null || (s_active != null && !s_active.IsDestoried))
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
            levelTitle = $"Main Level {data.index + 1}", size = size, cache = cache,
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
            levelTitle = $"Big Level {data.index + 1}", size = size, cache = cache, isLargeImage = true,
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
            return cache.GetValidIncompleteCopy(size, largeImage);
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
        MPTransitionView.Play(() =>
        {
            if (!ReferenceEquals(source, null) && (source == null || source.IsDestoried))
                return;

            beforeEnter?.Invoke();
            if (closeSource && source != null)
                source.DestroyWindow();
            UIManager.Inst.ShowWindow<T>(data, true);
            if (!closeSource && source != null && !source.IsDestoried)
                source.LostFocus(false);
        });
    }

    private static string BuildDetails(MPNewGamePopUIMsgData data)
    {
        if (data == null || data.cache == null)
            return string.Empty;

        MPLevelProgressCacheInfo cache = data.cache;
        MPPetConfig pet = MPDataManager.Instance.m_petsModel?.petConfigs?.Find(item => item != null && item.ID == cache.PetId);
        float rate = cache.CompletedBlocks.Count / (float)Mathf.Max(1, data.size * data.size);
        StringBuilder text = new StringBuilder();
        text.AppendLine($"Progress: {rate:P0}");
        text.AppendLine($"Lives used: {cache.UsedLoves}");
        text.AppendLine($"Pet: {(pet == null ? "None" : pet.Name)}");
        text.AppendLine($"Pet skills used: {cache.UsedPetSkillCount}");
        if (data.isLargeImage)
            text.AppendLine($"View: Row {cache.ViewX + 1}, Column {cache.ViewY + 1}");
        if (cache.SavedAtUtc > 0)
            text.AppendLine($"Saved: {DateTimeOffset.FromUnixTimeSeconds(cache.SavedAtUtc).LocalDateTime:yyyy-MM-dd HH:mm}");
        return text.ToString();
    }

    private void OnContinue() => Resolve(m_data?.continueAction);
    private void OnRestart() => Resolve(m_data?.restartAction);
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
