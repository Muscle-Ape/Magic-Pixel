using DG.Tweening;
using HQ.UIManager;
using System;
using UnityEngine;

/// <summary>
/// 通用弹窗缩放动画，挂载到弹窗根节点后会在打开和关闭时自动播放动画。
/// </summary>
[DisallowMultipleComponent]
public class MPPopScaleAnimation : MonoBehaviour
{
    /// <summary>
    /// 弹窗打开动画时长。
    /// </summary>
    private float m_openDuration = 0.25f;

    /// <summary>
    /// 弹窗关闭动画时长。
    /// </summary>
    private float m_closeDuration = 0.18f;

    /// <summary>
    /// 弹窗打开缩放缓动类型。
    /// </summary>
    private Ease m_openEase = Ease.OutBack;

    /// <summary>
    /// 弹窗关闭缩放缓动类型。
    /// </summary>
    private Ease m_closeEase = Ease.Linear;

    /// <summary>
    /// 缩放动画使用的窗口节点。
    /// </summary>
    private Transform m_window;

    /// <summary>
    /// 遮罩淡入淡出使用的透明度组件。
    /// </summary>
    private CanvasGroup m_maskCanvasGroup;

    /// <summary>
    /// 窗口节点原始缩放值。
    /// </summary>
    private Vector3 m_windowOriginalScale = Vector3.one;

    /// <summary>
    /// 遮罩最终透明度。
    /// </summary>
    private float m_maskOriginalAlpha = 1f;

    /// <summary>
    /// 当前正在播放的弹窗动画序列。
    /// </summary>
    private Sequence m_sequence;

    /// <summary>
    /// 是否已经初始化过节点引用。
    /// </summary>
    private bool m_initialized;

    /// <summary>
    /// 是否正在播放关闭动画，避免重复触发关闭。
    /// </summary>
    private bool m_isClosing;

    private void Awake()
    {
        InitAnimationNodes();
    }

    private void OnEnable()
    {
        PlayOpenAnimation();
    }

    private void OnDestroy()
    {
        m_sequence?.Kill();
    }

    /// <summary>
    /// 动态调用的关闭方法，会先播放关闭动画，再关闭当前弹窗。
    /// </summary>
    public void CloseWindow()
    {
        Close(null);
    }

    /// <summary>
    /// 关闭当前弹窗，动画结束后执行额外回调。
    /// </summary>
    /// <param name="onClosed">弹窗关闭后的回调。</param>
    public void Close(Action onClosed)
    {
        if (m_isClosing)
        {
            return;
        }

        m_isClosing = true;
        InitAnimationNodes();
        KillCurrentAnimation();

        bool hasTween = false;
        m_sequence = DOTween.Sequence().SetLink(gameObject);

        if (m_maskCanvasGroup != null)
        {
            hasTween = true;
            m_sequence.Join(m_maskCanvasGroup.DOFade(0f, m_closeDuration).SetEase(Ease.Linear));
        }

        if (m_window != null)
        {
            hasTween = true;
            m_sequence.Join(m_window.DOScale(Vector3.zero, m_closeDuration).SetEase(m_closeEase));
        }

        if (!hasTween)
        {
            CloseImmediately(onClosed);
            return;
        }

        m_sequence.OnComplete(() => CloseImmediately(onClosed));
    }

    /// <summary>
    /// 初始化弹窗动画需要的节点和初始状态。
    /// </summary>
    private void InitAnimationNodes()
    {
        if (m_initialized)
        {
            return;
        }

        m_initialized = true;
        Transform window = transform.Find("View/Window");
        if (window != null)
        {
            m_window = window;
            m_windowOriginalScale = window.localScale.sqrMagnitude <= 0.0001f ? Vector3.one : window.localScale;
        }

        Transform mask = transform.Find("Mask");

        if (mask != null)
        {
            m_maskCanvasGroup = mask.GetComponent<CanvasGroup>();
            if (m_maskCanvasGroup == null)
            {
                m_maskCanvasGroup = mask.gameObject.AddComponent<CanvasGroup>();
            }

            m_maskOriginalAlpha = Mathf.Approximately(m_maskCanvasGroup.alpha, 0f) ? 1f : m_maskCanvasGroup.alpha;
        }
    }

    /// <summary>
    /// 播放弹窗打开动画。
    /// </summary>
    private void PlayOpenAnimation()
    {
        if (m_isClosing)
        {
            return;
        }

        InitAnimationNodes();
        KillCurrentAnimation();

        bool hasTween = false;
        m_sequence = DOTween.Sequence().SetLink(gameObject);

        if (m_maskCanvasGroup != null)
        {
            hasTween = true;
            m_maskCanvasGroup.alpha = 0f;
            m_sequence.Join(m_maskCanvasGroup.DOFade(m_maskOriginalAlpha, m_openDuration).SetEase(Ease.Linear));
        }

        if (m_window != null)
        {
            hasTween = true;
            m_window.localScale = Vector3.zero;
            m_sequence.Join(m_window.DOScale(m_windowOriginalScale, m_openDuration).SetEase(m_openEase));
        }

        if (!hasTween)
        {
            m_sequence.Kill();
            m_sequence = null;
        }
    }

    /// <summary>
    /// 停止当前弹窗动画，避免多个 Tween 同时控制同一节点。
    /// </summary>
    private void KillCurrentAnimation()
    {
        m_sequence?.Kill();
        m_sequence = null;

        if (m_maskCanvasGroup != null)
        {
            m_maskCanvasGroup.DOKill();
        }

        if (m_window != null)
        {
            m_window.DOKill();
        }
    }

    /// <summary>
    /// 立即关闭当前弹窗窗口。
    /// </summary>
    /// <param name="onClosed">弹窗关闭后的回调。</param>
    private void CloseImmediately(Action onClosed)
    {
        AWindow window = GetComponent<AWindow>();
        if (window != null && !window.IsDestoried)
        {
            window.DestroyWindow();
        }
        else
        {
            Destroy(gameObject);
        }

        onClosed?.Invoke();
    }
}
