using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MPPetItem : MonoBehaviour
{
    /// <summary>
    /// 单个 Item 最多展示的奖励数量，需要和 prefab 中 Award1-Award3 对齐。
    /// </summary>
    private const int MAX_REWARD_COUNT = 3;

    /// <summary>
    /// Item 内部倒计时刷新间隔。倒计时由 Item 自己刷新，避免 ContentGrid 每秒整体刷新。
    /// </summary>
    private const float TIMER_REFRESH_INTERVAL = 1f;

    /// <summary>
    /// 宠物图标，解锁和未解锁状态都需要显示。
    /// </summary>
    private Image m_petIcon;

    /// <summary>
    /// 宠物等级文本。
    /// </summary>
    private TMP_Text m_level;

    /// <summary>
    /// 奖励领取倒计时文本。
    /// </summary>
    private TMP_Text m_timer;

    /// <summary>
    /// 奖励生产进度填充图。
    /// </summary>
    private Image m_progressFill;

    /// <summary>
    /// 当前选中标识。
    /// </summary>
    private GameObject m_selected;

    /// <summary>
    /// 已解锁信息区域。
    /// </summary>
    private GameObject m_info;

    /// <summary>
    /// 未解锁遮罩区域。
    /// </summary>
    private GameObject m_lockMask;

    /// <summary>
    /// 未解锁状态下额外显示的锁定框，对应 prefab 中的 LockFrame。
    /// </summary>
    private GameObject m_lockFrame;

    /// <summary>
    /// 未解锁条件文本。
    /// </summary>
    private TMP_Text m_unlockText;

    /// <summary>
    /// Item 根按钮，未解锁时也保持可点击，方便后续弹出解锁确认。
    /// </summary>
    private Button m_button;

    /// <summary>
    /// 奖励显示节点，最多展示三个。
    /// </summary>
    private Transform[] m_rewardNodes;

    /// <summary>
    /// 点击回调交给 MPPetsView 统一处理选中或解锁提示。
    /// </summary>
    private Action<MPPetConfig> m_onClick;

    /// <summary>
    /// 当前复用格子绑定的宠物配置。
    /// </summary>
    private MPPetConfig m_config;

    /// <summary>
    /// 当前宠物运行时数据。
    /// </summary>
    private MPPetRuntimeData m_runtimeData;

    /// <summary>
    /// Item 内部倒计时刷新计时器。
    /// </summary>
    private float m_timerRefreshElapsed;

    /// <summary>
    /// 兼容 prefab 上可能配置的初始化入口。
    /// </summary>
    public void Initialization()
    {
        Initialize(null);
    }

    /// <summary>
    /// 缓存 Item 内部节点，并注册点击事件。
    /// </summary>
    public void Initialize(Action<MPPetConfig> onClick)
    {
        m_onClick = onClick;

        m_petIcon = FindComponent<Image>("PetIcon");
        m_info = FindGameObject("Info");
        m_level = FindComponent<TMP_Text>("Info/LevelText");
        m_timer = FindComponent<TMP_Text>("Info/TimerText");
        m_progressFill = FindComponent<Image>("Info/ProgressBg/ProgressFill");
        m_selected = FindGameObject("Selected", "Info/Selected");
        m_lockMask = FindGameObject("LockMask");
        m_lockFrame = FindGameObject("LockFrame");
        m_unlockText = FindComponent<TMP_Text>("LockMask/UnlockText");
        m_button = GetComponent<Button>();

        if (m_button != null)
        {
            m_button.onClick.RemoveListener(OnClick);
            m_button.onClick.AddListener(OnClick);
        }

        m_rewardNodes = new Transform[MAX_REWARD_COUNT];
        for (int i = 0; i < MAX_REWARD_COUNT; i++)
        {
            m_rewardNodes[i] = FindTransform($"Info/Awards/Award{i + 1}", $"Awards/Award{i + 1}");
        }
    }

    /// <summary>
    /// 刷新 Item 展示内容。LoopGridView 复用格子时会重复调用。
    /// </summary>
    public void Refresh(MPPetConfig config, MPPetRuntimeData runtimeData, bool selected)
    {
        m_config = config;
        m_runtimeData = runtimeData;
        m_timerRefreshElapsed = 0f;

        bool unlocked = runtimeData != null && runtimeData.unlocked;

        SetPetIcon(config);
        SetActive(m_info, unlocked);
        SetActive(m_lockMask, !unlocked);
        SetActive(m_lockFrame, !unlocked);
        SetActive(m_selected, unlocked && selected);

        if (m_button != null)
        {
            // 未解锁状态也允许点击，后续在 View 层接入解锁确认弹窗。
            m_button.interactable = true;
        }

        if (!unlocked)
        {
            if (m_unlockText != null)
            {
                m_unlockText.text = config != null ? config.UnlockText : string.Empty;
            }
            return;
        }

        if (m_level != null)
        {
            m_level.text = $"Lv.{runtimeData.level}";
        }

        RefreshRewards(config);
        RefreshTimer(config);
    }

    private void Update()
    {
        if (m_config == null || m_runtimeData == null || !m_runtimeData.unlocked)
            return;

        m_timerRefreshElapsed += Time.deltaTime;
        if (m_timerRefreshElapsed < TIMER_REFRESH_INTERVAL)
            return;

        m_timerRefreshElapsed = 0f;
        RefreshTimer(m_config);
    }

    /// <summary>
    /// 刷新单个 Item 的奖励倒计时和进度。
    /// </summary>
    public void RefreshTimer(MPPetConfig config)
    {
        if (config == null)
            return;

        int remainingSeconds = MPUser.instance.GetPetRewardRemainingSeconds(config);
        float progress = MPUser.instance.GetPetRewardProgress(config);

        if (m_timer != null)
        {
            m_timer.text = remainingSeconds <= 0 ? "Ready" : FormatTime(remainingSeconds);
        }

        SetProgress(m_progressFill, progress);
    }

    /// <summary>
    /// 将秒数格式化为 00:00:00。
    /// </summary>
    public static string FormatTime(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    /// <summary>
    /// 根据奖励数量显示对应的奖励节点。
    /// </summary>
    private void RefreshRewards(MPPetConfig config)
    {
        int rewardCount = config == null ? 0 : Mathf.Min(config.Rewards.Count, MAX_REWARD_COUNT);
        for (int i = 0; i < MAX_REWARD_COUNT; i++)
        {
            Transform rewardNode = m_rewardNodes[i];
            if (rewardNode == null)
                continue;

            bool active = i < rewardCount;
            rewardNode.gameObject.SetActive(active);
            if (!active)
                continue;

            SetReward(rewardNode, config.Rewards[i]);
        }
    }

    /// <summary>
    /// 刷新单个奖励节点的图标和数量。
    /// </summary>
    private void SetReward(Transform rewardNode, MPPetRewardConfig reward)
    {
        if (reward == null)
            return;

        Transform icon = rewardNode.Find("Icon");
        if (icon != null)
        {
            Image image = icon.GetComponent<Image>();
            TMP_Text tmpText = icon.GetComponent<TMP_Text>();
            Text text = icon.GetComponent<Text>();

            Sprite sprite = LoadSprite(reward.Icon);
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
            }

            // 占位图或文本节点存在时，提供一个简短类型名兜底，方便美术资源未接入时调试。
            string shortName = GetRewardShortName(reward.Type);
            string fallbackText = reward.Count > 1 ? $"{shortName} {reward.Count}" : shortName;
            if (tmpText != null)
            {
                tmpText.text = fallbackText;
            }
            if (text != null)
            {
                text.text = fallbackText;
            }
        }

        Transform count = rewardNode.Find("Count");
        if (count != null)
        {
            SetText(count, reward.Count.ToString());
        }
    }

    /// <summary>
    /// 设置宠物图标。资源不存在时保留 prefab 原有占位图。
    /// </summary>
    private void SetPetIcon(MPPetConfig config)
    {
        if (m_petIcon == null || config == null)
            return;

        Sprite sprite = LoadSprite(config.Icon);
        if (sprite != null)
        {
            m_petIcon.sprite = sprite;
            m_petIcon.SetNativeSize();
        }
    }

    private void OnClick()
    {
        if (m_config == null)
            return;

        m_onClick?.Invoke(m_config);

        // 按钮点击音效
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    private T FindComponent<T>(params string[] paths) where T : Component
    {
        Transform target = FindTransform(paths);
        return target == null ? null : target.GetComponent<T>();
    }

    private GameObject FindGameObject(params string[] paths)
    {
        Transform target = FindTransform(paths);
        return target == null ? null : target.gameObject;
    }

    private Transform FindTransform(params string[] paths)
    {
        if (paths == null)
            return null;

        for (int i = 0; i < paths.Length; i++)
        {
            if (string.IsNullOrEmpty(paths[i]))
                continue;

            Transform target = transform.Find(paths[i]);
            if (target != null)
            {
                return target;
            }
        }

        return null;
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void SetText(Transform target, string value)
    {
        TMP_Text tmpText = target.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = value;
            return;
        }

        Text text = target.GetComponent<Text>();
        if (text != null)
        {
            text.text = value;
        }
    }

    private void SetProgress(Image image, float progress)
    {
        if (image == null)
            return;

        progress = Mathf.Clamp01(progress);
        // 进度条使用 Image.fillAmount，不修改 localScale，避免影响 prefab 原始布局。
        image.fillAmount = progress;
    }

    /// <summary>
    /// 通过项目资源加载封装加载图片，失败时返回 null。
    /// </summary>
    private Sprite LoadSprite(string location)
    {
        if (string.IsNullOrEmpty(location))
            return null;

        try
        {
            return MPLoad.Load<Sprite>(location);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 美术资源未接入时使用的奖励类型简写。
    /// </summary>
    private string GetRewardShortName(string rewardType)
    {
        if (string.IsNullOrEmpty(rewardType))
            return string.Empty;

        switch (rewardType.ToLowerInvariant())
        {
            case "coin":
                return "C";
            case "diamond":
            case "diamonds":
            case "gem":
            case "gems":
                return "D";
            case "light":
            case "hint":
            case "hint_prop":
                return "L";
            case "paw":
            case "love":
            case "life":
            case "love_recover":
            case "life_recover":
                return "P";
            case "leaf":
            case "food":
                return "Leaf";
            case "toy":
                return "Toy";
            default:
                return rewardType;
        }

    }
}
