using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主页宠物选择 Item。只负责锁定、选中和图标状态，不包含旧版养成及奖励逻辑。
/// </summary>
public class MPPetItem : MonoBehaviour
{
    private const float SELECTED_OFFSET_Y = 20f;
    private const float SELECT_ANIMATION_DURATION = 0.22f;
    private static readonly Color LOCKED_ICON_COLOR = new Color(0.42f, 0.42f, 0.42f, 1f);

    private Image m_icon;
    private RectTransform m_lock;
    private RectTransform m_shadow;
    private RectTransform m_iconRect;
    private RectTransform m_frame;
    private Vector2 m_frameBasePosition;
    private Vector2 m_iconBasePosition;
    private Tween m_frameTween;
    private Button m_button;
    private Action<MPPetConfig> m_onClick;
    private MPPetConfig m_config;
    private bool m_hasState;
    private bool m_selected;

    public MPPetConfig Config => m_config;
    public RectTransform RectTransform => transform as RectTransform;

    public void Initialization()
    {
        Initialize(null);
    }

    public void Initialize(Action<MPPetConfig> onClick)
    {
        m_onClick = onClick;
        m_lock = transform.Find("Lock") as RectTransform;
        m_shadow = transform.Find("Shadow") as RectTransform;
        m_iconRect = transform.Find("Icon") as RectTransform;
        m_frame = transform.Find("Shadow/Frame") as RectTransform;
        m_icon = m_iconRect == null ? null : m_iconRect.GetComponent<Image>();
        m_button = GetComponent<Button>();

        if (m_button != null)
        {
            m_button.onClick.RemoveListener(OnClick);
            m_button.onClick.AddListener(OnClick);
            m_button.interactable = true;
        }

        if (m_frame != null)
            m_frameBasePosition = m_frame.anchoredPosition;
        if (m_iconRect != null)
            m_iconBasePosition = m_iconRect.anchoredPosition;
    }

    public void Refresh(MPPetConfig config, bool unlocked, bool selected)
    {
        bool sameConfig = m_config != null && config != null && m_config.ID == config.ID;
        m_config = config;

        SetIcon(config);
        SetActive(m_lock, !unlocked);
        SetActive(m_shadow, unlocked);

        bool targetSelected = unlocked && selected;
        if (m_icon != null)
            m_icon.color = unlocked ? Color.white : LOCKED_ICON_COLOR;

        bool animated = m_hasState && sameConfig && m_selected != targetSelected;
        SetFrameSelectedState(targetSelected, animated);

        m_selected = targetSelected;
        m_hasState = true;
    }

    private void SetIcon(MPPetConfig config)
    {
        if (m_icon == null || config == null || string.IsNullOrWhiteSpace(config.Icon))
            return;

        MPLoad.ReleaseAll(this);
        try
        {
            Sprite sprite = MPLoad.Load<Sprite>(config.Icon, this);
            if (sprite != null)
            {
                m_icon.sprite = sprite;
                m_icon.preserveAspect = true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"宠物图标加载失败：{config.Icon}，{exception.Message}");
        }
    }

    /// <summary>
    /// Shadow 是固定背景，选中切换同步移动 Frame 和 Icon。
    /// 取消选择时保留 Frame 到下落动画结束，避免直接隐藏看不到回落过程。
    /// </summary>
    private void SetFrameSelectedState(bool selected, bool animated)
    {
        if (m_frame == null)
            return;

        KillFrameTween();
        Vector2 offset = Vector2.up * (selected ? SELECTED_OFFSET_Y : 0f);
        Vector2 frameTargetPosition = m_frameBasePosition + offset;
        Vector2 iconTargetPosition = m_iconBasePosition + offset;
        if (!animated)
        {
            m_frame.anchoredPosition = frameTargetPosition;
            if (m_iconRect != null)
                m_iconRect.anchoredPosition = iconTargetPosition;
            m_frame.localScale = Vector3.one;
            SetActive(m_frame, selected);
            return;
        }

        SetActive(m_frame, true);
        if (selected)
            m_frame.localScale = Vector3.one * 0.82f;

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject)
            .Join(m_frame.DOAnchorPos(frameTargetPosition, SELECT_ANIMATION_DURATION)
                .SetEase(Ease.OutCubic));
        if (m_iconRect != null)
        {
            sequence.Join(m_iconRect.DOAnchorPos(iconTargetPosition, SELECT_ANIMATION_DURATION)
                .SetEase(Ease.OutCubic));
        }
        if (selected)
        {
            sequence.Join(m_frame.DOScale(1f, SELECT_ANIMATION_DURATION)
                .SetEase(Ease.OutBack));
        }

        m_frameTween = sequence;
        sequence.OnComplete(() =>
        {
            if (m_frameTween != sequence)
                return;

            m_frameTween = null;
            if (!selected)
                SetActive(m_frame, false);
        });
        sequence.OnKill(() =>
        {
            if (m_frameTween == sequence)
                m_frameTween = null;
        });
    }

    private void KillFrameTween()
    {
        Tween previousTween = m_frameTween;
        m_frameTween = null;
        if (previousTween != null && previousTween.IsActive())
            previousTween.Kill();

        if (m_frame != null)
            m_frame.DOKill();
        if (m_iconRect != null)
            m_iconRect.DOKill();
    }

    private static void SetActive(Component target, bool active)
    {
        if (target != null && target.gameObject.activeSelf != active)
            target.gameObject.SetActive(active);
    }

    private void OnClick()
    {
        if (m_config == null)
            return;

        m_onClick?.Invoke(m_config);
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    private void OnDestroy()
    {
        if (m_button != null)
            m_button.onClick.RemoveListener(OnClick);

        KillFrameTween();

        MPLoad.ReleaseAll(this);
    }
}
