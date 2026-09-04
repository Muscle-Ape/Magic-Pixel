using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 使用预制体 View/Grid 下的方块随机错峰缩放，在完全遮住页面后执行切页。
/// </summary>
[Component("MPTransitionView")]
public class MPTransitionView : AWindow
{
    private const float OPEN_SCALE_DURATION = 0.28f;
    private const float CLOSE_SCALE_DURATION = 0.24f;

    // 第一块到最后一块开始动画的时间差，不随方块数量增加而延长过渡。
    private const float OPEN_STAGGER_DURATION = 0.4f;
    private const float CLOSE_STAGGER_DURATION = 0.36f;
    private const float DEFAULT_STAY_DURATION = 0.45f;

    [TransformPath("View/Grid")]
    private RectTransform m_grid;

    private readonly List<RectTransform> m_items = new List<RectTransform>();
    private readonly List<RectTransform> m_playOrder = new List<RectTransform>();
    private Sequence m_stageSequence;
    private Tween m_delayTween;
    private Action m_transitionAction;
    private Action m_completedAction;
    private float m_stayDuration = DEFAULT_STAY_DURATION;
    private bool m_autoClose = true;
    private bool m_closeRequested;
    private TransitionStage m_stage;

    private enum TransitionStage
    {
        Idle,
        Opening,
        Switching,
        Covered,
        Closing,
        Finished,
    }

    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>
    /// 全部方块放大后执行 transitionAction，全部缩小并销毁页面后执行 completedAction。
    /// autoClose 为 false 时，完全遮住页面后等待外部调用 PlayClose。
    /// </summary>
    public static MPTransitionView Play(Action transitionAction, Action completedAction = null,
        float stayDuration = DEFAULT_STAY_DURATION, bool autoClose = true)
    {
        MPTransitionViewUIMsgData data = new MPTransitionViewUIMsgData()
        {
            transitionAction = transitionAction,
            completedAction = completedAction,
            stayDuration = stayDuration,
            autoClose = autoClose,
        };

        return UIManager.Inst.ShowWindow<MPTransitionView>(data, true, UILayer.Top);
    }

    public static void OpenWindow<T>(UIMsgData uiMsgData, AWindow sourceWindow = null,
        UILayer targetLayer = UILayer.Bottom) where T : AWindow
    {
        Play(() =>
        {
            UIManager.Inst.ShowWindow<T>(uiMsgData, true, targetLayer);
            if (sourceWindow != null && !sourceWindow.IsDestoried)
            {
                sourceWindow.LostFocus(false);
            }
        });
    }

    public override void OnCreate()
    {
        CacheItems();
        SetItemsScale(Vector3.zero);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        if (IsDestoried || m_stage == TransitionStage.Finished)
            return;

        KillAllTweens();
        MPTransitionViewUIMsgData data = uiMsg as MPTransitionViewUIMsgData;
        m_transitionAction = data?.transitionAction;
        m_completedAction = data?.completedAction;
        m_stayDuration = data == null ? DEFAULT_STAY_DURATION : Mathf.Max(0f, data.stayDuration);
        m_autoClose = data == null || data.autoClose;
        m_closeRequested = false;
        m_stage = TransitionStage.Opening;

        CacheItems();
        SetItemsScale(Vector3.zero);
        FitGridToScreen();
        if (m_items.Count == 0)
        {
            Debug.LogWarning("[MPTransitionView] View/Grid 下没有可用的方块，将跳过方块动画。");
        }

        PlayScaleAnimation(true, OnOpenAnimationCompleted);
    }

    /// <summary>
    /// 入场期间收到关闭请求时，仍等待所有方块放大、完成切页后再退场。
    /// </summary>
    public void PlayClose(Action onClosed = null)
    {
        if (this == null || IsDestoried || m_stage == TransitionStage.Finished)
            return;

        m_completedAction += onClosed;
        m_closeRequested = true;
        if (m_stage == TransitionStage.Covered)
        {
            PlayCloseAnimation();
        }
    }

    private void CacheItems()
    {
        m_items.Clear();
        if (m_grid == null)
            return;

        for (int i = 0; i < m_grid.childCount; i++)
        {
            RectTransform item = m_grid.GetChild(i) as RectTransform;
            if (item != null && item.gameObject.activeSelf)
            {
                m_items.Add(item);
            }
        }
    }

    private void FitGridToScreen()
    {
        if (m_grid == null || m_items.Count == 0)
            return;

        GridLayoutGroup layout = m_grid.GetComponent<GridLayoutGroup>();
        if (layout == null || !layout.enabled)
            return;

        m_grid.ForceUpdateRectTransforms();
        int columns = Mathf.Clamp(layout.constraintCount, 1, m_items.Count);
        // 仅用完整行计算覆盖面积，末尾不足一行的方块排在屏幕下方，避免留下缺口。
        int rows = Mathf.Max(1, m_items.Count / columns);
        float cellSize = Mathf.Ceil(Mathf.Max(m_grid.rect.width / columns, m_grid.rect.height / rows));
        if (cellSize <= 0f)
            return;

        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.padding = new RectOffset();
        layout.spacing = Vector2.zero;
        layout.cellSize = new Vector2(cellSize, cellSize);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_grid);
    }

    private void ShufflePlayOrder()
    {
        m_playOrder.Clear();
        for (int i = 0; i < m_items.Count; i++)
        {
            if (m_items[i] != null)
            {
                m_playOrder.Add(m_items[i]);
            }
        }

        // 洗牌只改变播放次序，不修改 Grid 中的节点顺序或布局位置。
        for (int i = m_playOrder.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            RectTransform item = m_playOrder[i];
            m_playOrder[i] = m_playOrder[randomIndex];
            m_playOrder[randomIndex] = item;
        }
    }

    private void PlayScaleAnimation(bool opening, Action onCompleted)
    {
        ShufflePlayOrder();
        if (m_playOrder.Count == 0)
        {
            onCompleted();
            return;
        }

        float duration = opening ? OPEN_SCALE_DURATION : CLOSE_SCALE_DURATION;
        float staggerDuration = opening ? OPEN_STAGGER_DURATION : CLOSE_STAGGER_DURATION;
        float interval = m_playOrder.Count > 1 ? staggerDuration / (m_playOrder.Count - 1) : 0f;
        Vector3 targetScale = opening ? Vector3.one : Vector3.zero;

        // 过渡不受游戏暂停影响；子 Tween 由 Sequence 统一管理和释放。
        m_stageSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject).SetEase(Ease.Linear);
        for (int i = 0; i < m_playOrder.Count; i++)
        {
            m_stageSequence.Insert(i * interval,
                m_playOrder[i].DOScale(targetScale, duration).SetEase(opening ? Ease.OutQuad : Ease.InQuad));
        }

        m_stageSequence.OnComplete(() =>
        {
            m_stageSequence = null;
            onCompleted();
        });
    }

    private void OnOpenAnimationCompleted()
    {
        if (this == null || IsDestoried || m_stage != TransitionStage.Opening)
            return;

        SetItemsScale(Vector3.one);
        m_stage = TransitionStage.Switching;
        Action transitionAction = m_transitionAction;
        m_transitionAction = null;
        try
        {
            transitionAction?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[MPTransitionView] 页面切换失败：{e}");
        }

        // 切页回调中的 PlayClose 只记录请求，等所有窗口变更完成后再准备退场。
        if (this == null || IsDestoried || m_stage != TransitionStage.Switching)
            return;

        m_stage = TransitionStage.Covered;
        PrepareTargetWindowForReveal();

        // 目标页的聚焦回调也可能销毁过渡页，或主动开始退场。
        if (this == null || IsDestoried || m_stage != TransitionStage.Covered)
            return;

        if (m_closeRequested || (m_autoClose && m_stayDuration <= 0f))
        {
            PlayCloseAnimation();
        }
        else if (m_autoClose)
        {
            m_delayTween = DOVirtual.DelayedCall(m_stayDuration, () =>
            {
                m_delayTween = null;
                PlayCloseAnimation();
            }).SetUpdate(true).SetLink(gameObject);
        }
    }

    private void PlayCloseAnimation()
    {
        if (this == null || IsDestoried || m_stage != TransitionStage.Covered)
            return;

        // 手动退场时目标页也可能已发生变化，缩小方块前再检查一次。
        PrepareTargetWindowForReveal();
        if (this == null || IsDestoried || m_stage != TransitionStage.Covered)
            return;

        m_stage = TransitionStage.Closing;
        KillAllTweens();
        PlayScaleAnimation(false, FinishTransition);
    }

    private void PrepareTargetWindowForReveal()
    {
        List<AWindow> history = UIManager.Inst.HistoryList;
        int transitionIndex = history.IndexOf(this);
        if (transitionIndex <= 0 || transitionIndex != history.Count - 1)
            return;

        AWindow targetWindow = history[transitionIndex - 1];
        if (targetWindow == null || targetWindow.IsDestoried || targetWindow is MPTransitionView)
            return;

        // 返回旧页面时没有 ShowWindow 帮它入栈、聚焦，需要提前恢复。
        // 将逻辑顺序调整为「过渡页、目标页」，与打开新页面后的顺序一致。
        // 只移动自身的历史位置，不改 Transform 层级：过渡页仍在 Top 层遮挡和拦截点击。
        history.RemoveAt(transitionIndex);
        history.Insert(transitionIndex - 1, this);
        LostFocus(true);
        if (!targetWindow.IsFocus)
        {
            try
            {
                targetWindow.GetFocus();
            }
            catch (Exception e)
            {
                // 聚焦刷新异常时仍需完成退场，不能留下阻挡操作的过渡页。
                Debug.LogError($"[MPTransitionView] 目标页面恢复失败：{e}");
            }
        }
        // 退场后移除的已不是栈顶窗口，UIManager 不会再次触发目标页的 OnFocus。
    }

    private void FinishTransition()
    {
        if (this == null || IsDestoried || m_stage != TransitionStage.Closing)
            return;

        SetItemsScale(Vector3.zero);
        m_stage = TransitionStage.Finished;
        Action completedAction = m_completedAction;
        m_completedAction = null;
        DestroyWindow();
        completedAction?.Invoke();
    }

    private void SetItemsScale(Vector3 scale)
    {
        for (int i = 0; i < m_items.Count; i++)
        {
            if (m_items[i] != null)
            {
                m_items[i].localScale = scale;
            }
        }
    }

    private void KillAllTweens()
    {
        m_stageSequence?.Kill();
        m_stageSequence = null;
        m_delayTween?.Kill();
        m_delayTween = null;

        for (int i = 0; i < m_items.Count; i++)
        {
            if (m_items[i] != null)
            {
                m_items[i].DOKill();
            }
        }
    }

    public override void OnRelease()
    {
        m_stage = TransitionStage.Finished;
        KillAllTweens();
        m_transitionAction = null;
        m_completedAction = null;
        m_items.Clear();
        m_playOrder.Clear();
    }

    private void OnDestroy()
    {
        OnRelease();
    }
}

/// <summary>
/// 过渡页打开时传入的数据，保留原有切页调用方式。
/// </summary>
public class MPTransitionViewUIMsgData : UIMsgData
{
    public Action transitionAction;
    public Action completedAction;
    public float stayDuration = 0.45f;
    public bool autoClose = true;
}
