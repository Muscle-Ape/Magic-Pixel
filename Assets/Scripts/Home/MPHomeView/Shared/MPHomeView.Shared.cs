using DG.Tweening;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class MPHomeView
{
    private const int LARGER_TAB_INDEX = 0;
    private const int HOME_TAB_INDEX = 1;
    private const int CUSTOM_TAB_INDEX = 2;
    private const float TAB_SELECTED_Y = 66f;
    private const float TAB_NORMAL_Y = 14f;
    private const float SELECT_Y = 67f;
    private const float SWITCH_DURATION = 0.32f;

    [TransformPath("View")]
    private RectTransform m_viewRect;

    [TransformPath("View/Center")]
    private RectTransform m_center;

    [TransformPath("View/Head/SettingBtn")]
    private Button m_settingBtn;

    [TransformPath("View/Head/Coin/Count")]
    private TMP_Text m_coinText;

    [TransformPath("View/Head/Diamond/Count")]
    private TMP_Text m_diamondText;

    [TransformPath("View/Down/Tab/LargeImage")]
    private Button m_largerTabBtn;

    [TransformPath("View/Down/Tab/LargeImage/Text")]
    private TMP_Text m_largerTabText;

    [TransformPath("View/Down/Tab/Home")]
    private Button m_homeTabBtn;

    [TransformPath("View/Down/Tab/Home/Text")]
    private TMP_Text m_homeTabText;

    [TransformPath("View/Down/Tab/Custom")]
    private Button m_customTabBtn;

    [TransformPath("View/Down/Tab/Custom/Text")]
    private TMP_Text m_customTabText;

    [TransformPath("View/Down/Select")]
    private RectTransform m_select;

    private TabData[] m_tabs;
    private Sequence m_switchSequence;
    private int m_selectedTabIndex = HOME_TAB_INDEX;
    private float m_pageWidth;
    private bool m_initialized;
    private bool m_isApplyingLayout;

    private sealed class TabData
    {
        public Button Button;
        public RectTransform Item;
        public RectTransform MovingNode;
        public TMP_Text Label;
    }

    private void InitializeTabs()
    {
        m_tabs = new[]
        {
            CreateTabData(m_largerTabBtn, m_largerTabText),
            CreateTabData(m_homeTabBtn, m_homeTabText),
            CreateTabData(m_customTabBtn, m_customTabText),
        };
    }

    /// <summary>
    /// 当前预制体由 Tab 自身上下移动；如果后续在 Tab 下增加 Node，会自动只移动 Node。
    /// </summary>
    private static TabData CreateTabData(Button button, TMP_Text label)
    {
        RectTransform item = button.transform as RectTransform;
        RectTransform movingNode = button.transform.Find("Node") as RectTransform;
        return new TabData
        {
            Button = button,
            Item = item,
            MovingNode = movingNode != null ? movingNode : item,
            Label = label,
        };
    }

    private void RegisterListeners()
    {
        UnregisterListeners();
        m_settingBtn.onClick.AddListener(OnSettingClick);
        m_largerTabBtn.onClick.AddListener(OnLargerTabClick);
        m_homeTabBtn.onClick.AddListener(OnHomeTabClick);
        m_customTabBtn.onClick.AddListener(OnCustomTabClick);
        RegisterHomeListeners();
    }

    private void UnregisterListeners()
    {
        if (m_settingBtn != null)
            m_settingBtn.onClick.RemoveListener(OnSettingClick);
        if (m_largerTabBtn != null)
            m_largerTabBtn.onClick.RemoveListener(OnLargerTabClick);
        if (m_homeTabBtn != null)
            m_homeTabBtn.onClick.RemoveListener(OnHomeTabClick);
        if (m_customTabBtn != null)
            m_customTabBtn.onClick.RemoveListener(OnCustomTabClick);
        UnregisterHomeListeners();
    }

    /// <summary>
    /// 使用实际 View 尺寸横向排列三个整屏页面。
    /// </summary>
    private void RefreshResponsiveLayout()
    {
        if (m_viewRect == null || m_center == null)
            return;

        Canvas.ForceUpdateCanvases();
        Vector2 pageSize = m_viewRect.rect.size;
        if (pageSize.x <= 0f || pageSize.y <= 0f)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null
                ? Mathf.Max(0.0001f, canvas.scaleFactor)
                : 1f;
            pageSize = new Vector2(
                Screen.width / scaleFactor,
                Screen.height / scaleFactor);
        }

        m_isApplyingLayout = true;
        m_pageWidth = pageSize.x;
        m_center.anchorMin = new Vector2(0.5f, 0.5f);
        m_center.anchorMax = new Vector2(0.5f, 0.5f);
        m_center.pivot = new Vector2(0.5f, 0.5f);
        m_center.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, pageSize.x * 3f);
        m_center.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, pageSize.y);

        ConfigurePage(m_largerPage, pageSize, LARGER_TAB_INDEX);
        ConfigurePage(m_homePage, pageSize, HOME_TAB_INDEX);
        ConfigurePage(m_customPage, pageSize, CUSTOM_TAB_INDEX);
        m_isApplyingLayout = false;
    }

    private static void ConfigurePage(RectTransform page, Vector2 pageSize, int pageIndex)
    {
        if (page == null)
            return;

        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.zero;
        page.pivot = new Vector2(0.5f, 0.5f);
        page.sizeDelta = pageSize;
        page.anchoredPosition = new Vector2(
            (pageIndex + 0.5f) * pageSize.x,
            pageSize.y * 0.5f);
    }

    private void SwitchTab(int tabIndex)
    {
        if (tabIndex < LARGER_TAB_INDEX || tabIndex > CUSTOM_TAB_INDEX)
            return;
        if (m_selectedTabIndex == tabIndex)
            return;

        HidePetTip(false);
        if (tabIndex != CUSTOM_TAB_INDEX)
            CloseCustomPalette(false);
        m_selectedTabIndex = tabIndex;
        ApplyTabState(true);
    }

    private void ApplyTabState(bool animated)
    {
        KillSwitchSequence();
        if (!animated)
        {
            ApplyPagePosition(false);
            for (int i = 0; i < m_tabs.Length; i++)
            {
                bool selected = i == m_selectedTabIndex;
                Vector2 nodePosition = m_tabs[i].MovingNode.anchoredPosition;
                nodePosition.y = selected ? TAB_SELECTED_Y : TAB_NORMAL_Y;
                m_tabs[i].MovingNode.anchoredPosition = nodePosition;
                m_tabs[i].Label.alpha = selected ? 1f : 0f;
            }

            m_select.anchoredPosition = new Vector2(
                m_tabs[m_selectedTabIndex].Item.anchoredPosition.x,
                SELECT_Y);
            return;
        }

        m_switchSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);
        m_switchSequence.Join(m_center.DOAnchorPosX(
            GetCenterTargetX(),
            SWITCH_DURATION).SetEase(Ease.OutCubic));

        for (int i = 0; i < m_tabs.Length; i++)
        {
            bool selected = i == m_selectedTabIndex;
            m_switchSequence.Join(m_tabs[i].MovingNode.DOAnchorPosY(
                selected ? TAB_SELECTED_Y : TAB_NORMAL_Y,
                SWITCH_DURATION).SetEase(Ease.OutCubic));
            m_switchSequence.Join(m_tabs[i].Label.DOFade(
                selected ? 1f : 0f,
                SWITCH_DURATION * 0.75f));
        }

        m_switchSequence.Join(m_select.DOAnchorPos(
            new Vector2(m_tabs[m_selectedTabIndex].Item.anchoredPosition.x, SELECT_Y),
            SWITCH_DURATION).SetEase(Ease.OutCubic));
        m_switchSequence.OnKill(() => m_switchSequence = null);
    }

    private void ApplyPagePosition(bool animated)
    {
        float targetX = GetCenterTargetX();
        if (animated)
        {
            m_center.DOAnchorPosX(targetX, SWITCH_DURATION)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
            return;
        }

        Vector2 position = m_center.anchoredPosition;
        position.x = targetX;
        m_center.anchoredPosition = position;
    }

    private float GetCenterTargetX()
    {
        return (HOME_TAB_INDEX - m_selectedTabIndex) * m_pageWidth;
    }

    private void RefreshCurrency()
    {
        if (m_coinText != null)
            m_coinText.text = MPUser.instance.GetCoins().ToString();
        if (m_diamondText != null)
            m_diamondText.text = MPUser.instance.GetDiamond().ToString();
    }

    private void KillSwitchSequence()
    {
        if (m_switchSequence != null && m_switchSequence.IsActive())
            m_switchSequence.Kill();
        m_switchSequence = null;
    }

    private void OnLargerTabClick()
    {
        SwitchTab(LARGER_TAB_INDEX);
    }

    private void OnHomeTabClick()
    {
        SwitchTab(HOME_TAB_INDEX);
    }

    private void OnCustomTabClick()
    {
        SwitchTab(CUSTOM_TAB_INDEX);
    }

    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }
}
