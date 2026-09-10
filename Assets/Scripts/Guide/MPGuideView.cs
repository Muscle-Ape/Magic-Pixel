using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>首次启动时显示的独立引导，不继承游戏页，不读写正式关卡进度。</summary>
[HQ.UIManager.Component("MPGuideView")]
public sealed partial class MPGuideView : AWindow
{
    private const string FINISHED_KEY = "key_nonogramTutorial_finished_v1";
    private const string DISMISSED_KEY = "key_guideDismissed_v1";
    private static MPGuideView s_current;
    private MPGuideLesson m_lesson;
    private bool m_preview, m_released, m_settling, m_hintDismissed, m_isReplay;
    private Sequence m_handTween;
    private Tween m_feedbackTween;
    private readonly List<Tween> m_settlementTweens = new List<Tween>();
    private Coroutine m_settlementRoutine;

    [TransformPath("View")] private CanvasGroup m_view;
    [TransformPath("View/Content/Grid")] private GridLayoutGroup m_grid;
    [TransformPath("View/Content/Vertical")] private RectTransform m_vertical;
    [TransformPath("View/Content/Horizontal")] private RectTransform m_horizontal;
    [TransformPath("View/Content/Input")] private RectTransform m_inputRect;
    [TransformPath("View/Content/CompletedFrame")] private Image m_completedFrame;
    [TransformPath("View/Content/Frame")] private Image m_contentFrame;
    [TransformPath("View/Content/OuterFrame")] private Image m_outerFrame;
    [TransformPath("Bg/Floor")] private Image m_floor;
    [TransformPath("View/Instruction")] private RectTransform m_instruction;
    [TransformPath("View/Instruction/Text")] private TMP_Text m_description;
    [TransformPath("View/Heading")] private TMP_Text m_heading;
    [TransformPath("View/Feedback")] private TMP_Text m_feedback;
    [TransformPath("View/Next")] private Button m_next;
    [TransformPath("View/Next/Text")] private TMP_Text m_nextText;
    [TransformPath("View/Skip")] private Button m_skip;
    [TransformPath("View/Btns")] private RectTransform m_btns;
    [TransformPath("View/Btns/ModeSwitch")] private Button m_switch;
    [TransformPath("View/Btns/ModeSwitch/Btn")] private RectTransform m_switchTab;
    [TransformPath("View/Btns/ModeSwitch/Btn/Fill")] private Image m_switchFill;
    [TransformPath("View/Btns/ModeSwitch/Btn/Blank")] private Image m_switchBlank;
    [TransformPath("View/Hand")] private RectTransform m_hand;
    [TransformPath("View/Hand")] private CanvasGroup m_handGroup;
    [TransformPath("View/Content/Highlights")] private RectTransform m_highlightRoot;

    private MPGuideInput m_input;
    private readonly MPGameBlock[] m_blocks = new MPGameBlock[25];
    private readonly MPGameNumberFrameBase[] m_rows = new MPGameNumberFrameBase[5];
    private readonly MPGameNumberFrameBase[] m_columns = new MPGameNumberFrameBase[5];
    private readonly Image[] m_highlights = new Image[8];

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static bool ShouldShowOnStartup()
    {
        try { return !ES3.Load<bool>(FINISHED_KEY, defaultValue: false) && !ES3.Load<bool>(DISMISSED_KEY, defaultValue: false); }
        catch (Exception ex)
        {
            Debug.LogWarning($"引导记录读取失败，将重新显示引导：{ex.Message}");
            return true;
        }
    }

    public static MPGuideView Show(bool isReplay = false)
    {
        if (s_current != null && !s_current.IsDestoried) return s_current;
        try
        {
            return UIManager.Inst.ShowWindow<MPGuideView>(new MPGuideViewUIMsgData { isReplay = isReplay });
        }
        catch
        {
            // 加载失败时清掉半初始化窗口，让启动页的重试真正重新加载资源。
            if (s_current != null && !s_current.IsDestoried) s_current.DestroyWindow();
            throw;
        }
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        s_current = this;
        m_isReplay = (uiMsg as MPGuideViewUIMsgData)?.isReplay ?? false;
        m_lesson = new MPGuideLesson();
        BuildBoard(name => MPLoad.Load<GameObject>(name, this), name => MPLoad.Load<Sprite>(name, this));
        m_next.onClick.AddListener(OnNext);
        m_skip.onClick.AddListener(Skip);
        m_switch.onClick.AddListener(OnSwitch);
        RefreshLesson();
    }

    private void OnNext()
    {
        if (!m_settling && m_lesson.Advance()) RefreshLesson();
    }

    private void OnSwitch()
    {
        if (m_settling || !m_lesson.SwitchMode()) return;
        StopHandHint();
        RefreshLesson();
    }

    private void OnCell(int index)
    {
        if (m_settling || m_lesson.IsMarked(index)) return;
        if (!m_lesson.TryMark(index))
        {
            if (m_lesson.FirstTarget() < 0) return;
            m_feedback.text = "Try a highlighted square. No lives are lost in this tutorial.";
            m_feedbackTween?.Kill();
            if (!m_preview)
                m_feedbackTween = DOVirtual.DelayedCall(2f, SetFeedback).SetUpdate(true).SetLink(gameObject);
            return;
        }
        // 每一步只演示到首次有效操作，不在每次落笔后重启动画。
        m_hintDismissed = true;
        StopHandHint();
        ShowMark(index, !m_preview);
        RefreshBoard();
        if (m_lesson.FirstTarget() < 0)
        {
            m_input.CancelGesture();
            m_lesson.Advance();
            RefreshLesson();
        }
    }

    private void RefreshLesson()
    {
        m_feedbackTween?.Kill();
        m_feedbackTween = null;
        m_hintDismissed = false;
        m_description.text = m_lesson.Description;
        m_heading.text = m_lesson.Current == MPGuideLesson.Stage.Welcome ? "How to Play" : $"How to Play   {(int)m_lesson.Current}/10";
        bool next = m_lesson.Current == MPGuideLesson.Stage.Welcome || m_lesson.Current == MPGuideLesson.Stage.Columns;
        m_next.gameObject.SetActive(next);
        m_nextText.text = m_lesson.Current == MPGuideLesson.Stage.Welcome ? "Let's Play" : "Got It";
        m_switch.interactable = m_lesson.CanSwitch;
        m_switchTab.anchoredPosition = new Vector2(m_lesson.FillMode ? 78f : -78f, 0);
        m_switchFill.gameObject.SetActive(m_lesson.FillMode);
        m_switchBlank.gameObject.SetActive(!m_lesson.FillMode);
        RefreshBoard();
        // 输入区域和游戏页相同，始终为 800×800；尚未介绍的行由步骤校验拦截。
        m_input.Initialize(5, OnCell);
        SetFeedback();
        StartHandHint();
        if (m_lesson.Current == MPGuideLesson.Stage.Completed && !m_preview && !m_settling)
            m_settlementRoutine = StartCoroutine(PlayCompletedAnimation());
    }

    private void SetFeedback()
    {
        if (m_released) return;
        m_feedback.text = m_lesson.CanSwitch ? "Tap the mode switch" :
            m_lesson.FirstTarget() >= 0 ? "Follow the highlighted squares" : "Learn one step at a time";
    }

    private void RefreshBoard()
    {
        for (int i = 0; i < m_blocks.Length; i++)
        {
            m_blocks[i].GetComponent<CanvasGroup>().alpha = i / 5 < m_lesson.VisibleRows ? 1f : 0f;
            if (m_lesson.IsMarked(i) && !m_blocks[i].completed) ShowMark(i, false);
            m_blocks[i].SetBlankHit(false);
        }
        for (int i = 0; i < 5; i++)
        {
            m_rows[i].GetComponent<CanvasGroup>().alpha = i >= m_lesson.VisibleRows ? 0f : m_lesson.LineFilled(false, i) ? 0.5f : 1f;
            m_columns[i].GetComponent<CanvasGroup>().alpha = !m_lesson.ShowColumns ? 0f : m_lesson.LineFilled(true, i) ? 0.5f : 1f;
        }
        Canvas.ForceUpdateCanvases();
        RefreshHighlights();
    }

    private void ShowMark(int index, bool animate)
    {
        if (MPGuideLesson.IsFill(index)) m_blocks[index].Fill(animate);
        else m_blocks[index].Blank(animate);
        m_blocks[index].Disable();
    }

    private void Skip()
    {
        if (m_preview || m_settling) return;
        m_settling = true;
        StopHandHint();
        MPTransitionView.Play(() =>
        {
            if (this == null || IsDestoried) return;
            if (!m_isReplay)
            {
                if (UIManager.Inst.ShowWindow<MPHomeView>() == null) { m_settling = false; return; }
                SaveFlag(DISMISSED_KEY);
            }
            DestroyWindow();
        });
    }

    private static void SaveFlag(string key)
    {
        try { ES3.Save(key, true); }
        catch (Exception ex) { Debug.LogWarning($"引导记录保存失败：{ex.Message}"); }
    }

    public override void OnFocus(bool focus)
    {
        if (m_lesson == null || m_hand == null) return;
        if (focus && !m_settling) StartHandHint();
        else { StopHandHint(); m_input?.CancelGesture(); }
    }

    private void OnDisable() { StopHandHint(); m_feedbackTween?.Kill(); m_input?.CancelGesture(); }

    public override void OnRelease()
    {
        if (m_released) return;
        m_released = true;
        StopHandHint();
        m_feedbackTween?.Kill();
        if (m_settlementRoutine != null) StopCoroutine(m_settlementRoutine);
        foreach (Tween tween in m_settlementTweens) tween?.Kill();
        m_settlementTweens.Clear();
        m_input?.Initialize(5, null);
        m_next?.onClick.RemoveListener(OnNext);
        m_switch?.onClick.RemoveListener(OnSwitch);
        m_skip?.onClick.RemoveListener(Skip);
        foreach (var frame in m_rows) if (frame != null) frame.GetComponent<CanvasGroup>().DOKill();
        foreach (var frame in m_columns) if (frame != null) frame.GetComponent<CanvasGroup>().DOKill();
        if (s_current == this) s_current = null;
        MPLoad.ReleaseAll(this);
    }

    private void OnDestroy() { OnRelease(); }

#if UNITY_EDITOR
    public MPGuideLesson.Stage PreviewStage => m_lesson.Current;
    public bool PreviewHintVisible => m_hand.gameObject.activeSelf;
    public int PreviewHighlightCount => Array.FindAll(m_highlights, image => image != null && image.gameObject.activeSelf).Length;
    public void BuildEditorPreview(Func<string, GameObject> prefab, Func<string, Sprite> sprite, int stage)
    {
        m_preview = true;
        UIManagerUtils.InitComponent(this);
        m_lesson = new MPGuideLesson();
        while ((int)m_lesson.Current < stage)
        {
            if (m_lesson.CanSwitch) m_lesson.SwitchMode();
            else
            {
                int index;
                while ((index = m_lesson.FirstTarget()) >= 0) m_lesson.TryMark(index);
                if (!m_lesson.Advance()) break;
            }
        }
        BuildBoard(prefab, sprite);
        RefreshLesson();
        if (stage == 11) ShowCompletedPreview();
    }
#endif
}

public sealed class MPGuideViewUIMsgData : UIMsgData
{
    public bool isReplay;
}
