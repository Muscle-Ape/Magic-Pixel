using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MPButton : Button
{
    [Header("Scale Animation")]
    [SerializeField] private bool m_enableScaleAnimation = true;
    [SerializeField] private float m_pressedScale = 0.92f;
    [SerializeField] private float m_pressDuration = 0.08f;
    [SerializeField] private float m_releaseDuration = 0.1f;
    [SerializeField] private Ease m_pressEase = Ease.OutQuad;
    [SerializeField] private Ease m_releaseEase = Ease.OutBack;

    [Header("Click Limit")]
    [SerializeField] private float m_clickInterval = 0.5f;
    [SerializeField] private bool m_useUnscaledTime = true;

    private Vector3 m_normalScale;
    private Tween m_scaleTween;
    private float m_lastClickTime = -999f;
    private Coroutine m_submitCoroutine;

    protected override void Awake()
    {
        base.Awake();
        m_normalScale = transform.localScale;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        m_normalScale = transform.localScale;
    }

    protected override void OnDisable()
    {
        KillScaleTween();

        if (m_submitCoroutine != null)
        {
            StopCoroutine(m_submitCoroutine);
            m_submitCoroutine = null;
        }

        transform.localScale = m_normalScale;
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        KillScaleTween();
        base.OnDestroy();
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);

        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        PlayScaleTween(m_normalScale * m_pressedScale, m_pressDuration, m_pressEase);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        PlayScaleTween(m_normalScale, m_releaseDuration, m_releaseEase);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        PlayScaleTween(m_normalScale, m_releaseDuration, m_releaseEase);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        TryClick();
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (!TryClick())
        {
            return;
        }

        DoStateTransition(SelectionState.Pressed, false);

        if (m_submitCoroutine != null)
        {
            StopCoroutine(m_submitCoroutine);
        }

        m_submitCoroutine = StartCoroutine(FinishSubmit());
    }

    private bool TryClick()
    {
        if (!IsActive() || !IsInteractable() || !CanClick())
        {
            return false;
        }

        // 按钮点击音效
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
        MPVibrationManager.Instance.PlaySelection();

        m_lastClickTime = CurrentTime;
        onClick.Invoke();
        return true;
    }

    private bool CanClick()
    {
        return m_clickInterval <= 0f || CurrentTime - m_lastClickTime >= m_clickInterval;
    }

    private void PlayScaleTween(Vector3 targetScale, float duration, Ease ease)
    {
        if (!m_enableScaleAnimation || !IsActive() || !IsInteractable())
        {
            return;
        }

        KillScaleTween();

        if (duration <= 0f)
        {
            transform.localScale = targetScale;
            return;
        }

        m_scaleTween = transform.DOScale(targetScale, duration)
            .SetEase(ease)
            .SetUpdate(m_useUnscaledTime);
    }

    private void KillScaleTween()
    {
        if (m_scaleTween == null)
        {
            return;
        }

        m_scaleTween.Kill();
        m_scaleTween = null;
    }

    private IEnumerator FinishSubmit()
    {
        float fadeTime = colors.fadeDuration;
        float elapsedTime = 0f;

        while (elapsedTime < fadeTime)
        {
            elapsedTime += m_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        DoStateTransition(currentSelectionState, false);
        m_submitCoroutine = null;
    }

    private float CurrentTime
    {
        get { return m_useUnscaledTime ? Time.unscaledTime : Time.time; }
    }
}
