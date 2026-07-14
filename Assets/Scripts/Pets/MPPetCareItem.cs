using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MPPetCareItem : MonoBehaviour
{
    private Image m_icon;
    private TMP_Text m_nameText;
    private TMP_Text m_restoreText;
    private TMP_Text m_countText;
    private GameObject m_info;
    private GameObject m_progressBg;
    private GameObject m_awards;
    private GameObject m_selected;
    private GameObject m_selectFrame;
    private GameObject m_useButtonRoot;
    private Button m_useButton;
    private TMP_Text m_useButtonText;
    private GameObject m_lockMask;
    private TMP_Text m_unlockText;
    private Button m_button;
    private Action<MPPetCareItemConfig> m_onClick;
    private Action<MPPetCareItemConfig> m_onUseClick;
    private MPPetCareItemConfig m_config;

    /// <summary>
    /// 兼容 prefab 上可能配置的初始化入口。
    /// </summary>
    public void Initialization()
    {
        Initialize(null, null);
    }

    public void Initialize(Action<MPPetCareItemConfig> onClick, Action<MPPetCareItemConfig> onUseClick)
    {
        m_onClick = onClick;
        m_onUseClick = onUseClick;

        m_icon = FindComponent<Image>("PetIcon");
        m_info = FindGameObject("Info");
        m_nameText = FindComponent<TMP_Text>("Info/LevelText");
        m_restoreText = FindComponent<TMP_Text>("Info/TimerText");
        m_countText = FindComponent<TMP_Text>("Info/CountText");
        m_progressBg = FindGameObject("Info/ProgressBg");
        m_awards = FindGameObject("Info/Awards");
        m_selected = FindGameObject("Info/Selected");
        m_selectFrame = FindGameObject("Info/SelectFrame");
        m_useButtonRoot = FindGameObject("Info/UseButton");
        m_lockMask = FindGameObject("LockMask");
        m_unlockText = FindComponent<TMP_Text>("LockMask/UnlockText");
        m_button = GetComponent<Button>();

        EnsureSelectFrame();
        EnsureCountText();
        EnsureUseButton();

        if (m_button != null)
        {
            m_button.onClick.RemoveListener(OnClick);
            m_button.onClick.AddListener(OnClick);
        }

        if (m_useButton != null)
        {
            m_useButton.onClick.RemoveListener(OnUseClick);
            m_useButton.onClick.AddListener(OnUseClick);
        }

        // 食物和玩具不展示宠物奖励列表和倒计时进度，复用卡片尺寸但隐藏无关节点。
        SetActive(m_progressBg, false);
        SetActive(m_awards, false);
        SetActive(m_selected, false);
        SetActive(m_selectFrame, false);
        SetActive(m_useButtonRoot, false);
    }

    public void Refresh(MPPetCareItemConfig config, MPPetCareRuntimeData runtimeData, bool selected, bool canUse)
    {
        m_config = config;

        bool unlocked = runtimeData != null && runtimeData.unlocked;
        SetIcon(config);
        SetActive(m_info, unlocked);
        SetActive(m_lockMask, !unlocked);
        SetActive(m_progressBg, false);
        SetActive(m_awards, false);
        SetActive(m_selected, false);
        SetActive(m_selectFrame, unlocked && selected);
        SetActive(m_useButtonRoot, unlocked && selected);

        if (m_button != null)
        {
            // 未解锁道具也允许点击，后续可在 View 层接入解锁弹窗。
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

        int count = runtimeData == null ? 0 : Mathf.Max(0, runtimeData.count);

        if (m_nameText != null)
        {
            m_nameText.text = config != null ? config.Name : string.Empty;
        }

        if (m_restoreText != null)
        {
            m_restoreText.text = config != null ? config.RestoreText : string.Empty;
        }

        if (m_countText != null)
        {
            m_countText.text = $"x{count}";
        }

        if (m_useButton != null)
        {
            // 数量不足或目标状态已满时禁用使用按钮，避免玩家误消耗。
            m_useButton.interactable = canUse;
        }

        if (m_useButtonText != null)
        {
            m_useButtonText.text = "Use";
        }
    }

    private void EnsureSelectFrame()
    {
        if (m_selectFrame != null || m_info == null)
            return;

        GameObject frame = new GameObject("SelectFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
        frame.layer = gameObject.layer;
        frame.transform.SetParent(m_info.transform, false);

        RectTransform rectTransform = frame.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;

        Image image = frame.GetComponent<Image>();
        image.color = new Color(0.55f, 0.9f, 0.25f, 0.08f);
        image.raycastTarget = false;

        Outline outline = frame.GetComponent<Outline>();
        outline.effectColor = new Color(0.42f, 0.78f, 0.16f, 1f);
        outline.effectDistance = new Vector2(4f, -4f);
        outline.useGraphicAlpha = false;

        m_selectFrame = frame;
    }

    private void EnsureCountText()
    {
        if (m_countText != null || m_info == null)
            return;

        GameObject textRoot = new GameObject("CountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textRoot.layer = gameObject.layer;
        textRoot.transform.SetParent(m_info.transform, false);

        RectTransform rectTransform = textRoot.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        rectTransform.anchoredPosition = new Vector2(-14f, -14f);
        rectTransform.sizeDelta = new Vector2(82f, 34f);

        m_countText = textRoot.GetComponent<TextMeshProUGUI>();
        if (m_nameText != null)
        {
            m_countText.font = m_nameText.font;
            m_countText.fontSharedMaterial = m_nameText.fontSharedMaterial;
        }
        m_countText.fontSize = 28;
        m_countText.fontStyle = FontStyles.Bold;
        m_countText.color = new Color(0.12f, 0.07f, 0.04f, 1f);
        m_countText.alignment = TextAlignmentOptions.Right;
        m_countText.raycastTarget = false;
        m_countText.text = "x0";
    }

    private void EnsureUseButton()
    {
        if (m_useButtonRoot == null && m_info != null)
        {
            GameObject buttonRoot = new GameObject("UseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonRoot.layer = gameObject.layer;
            buttonRoot.transform.SetParent(m_info.transform, false);

            RectTransform rectTransform = buttonRoot.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, -138f);
            rectTransform.sizeDelta = new Vector2(140f, 42f);

            Image image = buttonRoot.GetComponent<Image>();
            image.color = new Color(0.52f, 0.82f, 0.22f, 1f);
            image.raycastTarget = true;

            m_useButtonRoot = buttonRoot;
        }

        if (m_useButtonRoot == null)
            return;

        m_useButton = m_useButtonRoot.GetComponent<Button>();
        if (m_useButton == null)
        {
            m_useButton = m_useButtonRoot.AddComponent<Button>();
        }

        m_useButtonText = m_useButtonRoot.GetComponentInChildren<TMP_Text>(true);
        if (m_useButtonText == null)
        {
            GameObject textRoot = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textRoot.layer = gameObject.layer;
            textRoot.transform.SetParent(m_useButtonRoot.transform, false);

            RectTransform textRect = textRoot.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;

            m_useButtonText = textRoot.GetComponent<TextMeshProUGUI>();
            if (m_nameText != null)
            {
                m_useButtonText.font = m_nameText.font;
                m_useButtonText.fontSharedMaterial = m_nameText.fontSharedMaterial;
            }
            m_useButtonText.fontSize = 28;
            m_useButtonText.fontStyle = FontStyles.Bold;
            m_useButtonText.color = new Color(0.12f, 0.07f, 0.04f, 1f);
            m_useButtonText.alignment = TextAlignmentOptions.Center;
            m_useButtonText.raycastTarget = false;
            m_useButtonText.text = "Use";
        }
    }

    private void SetIcon(MPPetCareItemConfig config)
    {
        if (m_icon == null || config == null || string.IsNullOrEmpty(config.Icon))
            return;

        try
        {
            Sprite sprite = MPLoad.Load<Sprite>(config.Icon);
            if (sprite != null)
            {
                m_icon.sprite = sprite;
            }
        }
        catch (Exception)
        {
        }
    }

    private void OnClick()
    {
        if (m_config == null)
            return;

        m_onClick?.Invoke(m_config);
    }

    private void OnUseClick()
    {
        if (m_config == null)
            return;

        m_onUseClick?.Invoke(m_config);
    }

    private T FindComponent<T>(string path) where T : Component
    {
        Transform target = transform.Find(path);
        return target == null ? null : target.GetComponent<T>();
    }

    private GameObject FindGameObject(string path)
    {
        Transform target = transform.Find(path);
        return target == null ? null : target.gameObject;
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
