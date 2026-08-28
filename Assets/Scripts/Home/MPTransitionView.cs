using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 页面过渡动画界面，用于在两个页面切换时播放云片、图标和装饰物的入场与退场动画。
/// </summary>
[Component("MPTransitionView")]
public class MPTransitionView : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>
    /// 四周云片滑入和滑出的动画时长。
    /// </summary>
    private const float CLOUD_DURATION = 0.45f;

    /// <summary>
    /// 中间图标缩放和淡入的动画时长。
    /// </summary>
    private const float ICON_DURATION = 0.42f;

    /// <summary>
    /// 小方块、叶子和星光从中心展开到目标位置的动画时长。
    /// </summary>
    private const float ITEM_SPREAD_DURATION = 0.55f;

    /// <summary>
    /// 装饰物逐个展开时的间隔。
    /// </summary>
    private const float ITEM_STAGGER_INTERVAL = 0.025f;

    /// <summary>
    /// 图标和装饰物收回到中心的动画时长。
    /// </summary>
    private const float CLOSE_DURATION = 0.36f;

    /// <summary>
    /// 云片移出屏幕时额外增加的偏移，避免边缘露出。
    /// </summary>
    private const float CLOUD_OUT_PADDING = 40f;

    /// <summary>
    /// 过渡页默认停留时间，停留结束后会自动播放退场动画。
    /// </summary>
    private const float DEFAULT_STAY_DURATION = 0.45f;

    /// <summary>
    /// 左上云片节点，Prefab 中节点名为 LetfUp。
    /// </summary>
    [TransformPath("View/Cloud/LetfUp")]
    private RectTransform m_leftUpCloud;

    /// <summary>
    /// 左侧云片节点。
    /// </summary>
    [TransformPath("View/Cloud/Left")]
    private RectTransform m_leftCloud;

    /// <summary>
    /// 左下云片节点。
    /// </summary>
    [TransformPath("View/Cloud/LeftDown")]
    private RectTransform m_leftDownCloud;

    /// <summary>
    /// 右上云片节点。
    /// </summary>
    [TransformPath("View/Cloud/RightUp")]
    private RectTransform m_rightUpCloud;

    /// <summary>
    /// 右侧云片节点。
    /// </summary>
    [TransformPath("View/Cloud/Right")]
    private RectTransform m_rightCloud;

    /// <summary>
    /// 右下云片节点。
    /// </summary>
    [TransformPath("View/Cloud/RightDown")]
    private RectTransform m_rightDownCloud;

    /// <summary>
    /// 中间图标节点，只做缩放和透明度变化。
    /// </summary>
    [TransformPath("View/Icon")]
    private RectTransform m_icon;

    /// <summary>
    /// 小方块装饰物父节点。
    /// </summary>
    [TransformPath("View/Items/Block")]
    private RectTransform m_blockRoot;

    /// <summary>
    /// 叶子装饰物父节点。
    /// </summary>
    [TransformPath("View/Items/Leaf")]
    private RectTransform m_leafRoot;

    /// <summary>
    /// 星光装饰物父节点。
    /// </summary>
    [TransformPath("View/Items/Star")]
    private RectTransform m_starRoot;

    /// <summary>
    /// 所有云片的原始位置和隐藏位置缓存。
    /// </summary>
    private readonly List<CloudState> m_clouds = new List<CloudState>();

    /// <summary>
    /// 所有装饰物的原始位置、缩放、旋转和透明度缓存。
    /// </summary>
    private readonly List<ItemState> m_items = new List<ItemState>();

    /// <summary>
    /// 装饰物展开后的循环浮动、旋转和闪烁 Tween。
    /// </summary>
    private readonly List<Tween> m_idleTweens = new List<Tween>();

    /// <summary>
    /// 中间图标下所有 Graphic 的初始透明度。
    /// </summary>
    private readonly List<float> m_iconOriginalAlphas = new List<float>();

    /// <summary>
    /// 中间图标下参与透明度动画的 Graphic 组件。
    /// </summary>
    private Graphic[] m_iconGraphics;

    /// <summary>
    /// 中间图标在 Prefab 中配置的原始锚点坐标。
    /// </summary>
    private Vector2 m_iconOriginalPosition;

    /// <summary>
    /// 中间图标在 Prefab 中配置的原始缩放。
    /// </summary>
    private Vector3 m_iconOriginalScale;

    /// <summary>
    /// 当前阶段动画序列，入场和退场都会复用。
    /// </summary>
    private Sequence m_stageSequence;

    /// <summary>
    /// 入场完成后等待自动关闭的延迟 Tween。
    /// </summary>
    private Tween m_delayTween;

    /// <summary>
    /// 过渡页完全覆盖后执行的页面切换回调。
    /// </summary>
    private Action m_transitionAction;

    /// <summary>
    /// 过渡页退场结束并销毁后的回调。
    /// </summary>
    private Action m_completedAction;

    /// <summary>
    /// 入场完成后停留多久再自动退场。
    /// </summary>
    private float m_stayDuration = DEFAULT_STAY_DURATION;

    /// <summary>
    /// 是否在入场完成并停留一段时间后自动关闭。
    /// </summary>
    private bool m_autoClose = true;

    /// <summary>
    /// 是否已经缓存过 Prefab 中配置好的原始状态。
    /// </summary>
    private bool m_hasCachedOriginalState;

    /// <summary>
    /// 页面切换回调是否已经执行过，避免重复切页。
    /// </summary>
    private bool m_hasInvokedTransitionAction;

    /// <summary>
    /// 是否正在播放退场动画，避免多次触发关闭。
    /// </summary>
    private bool m_isClosing;

    /// <summary>
    /// 播放一次通用过渡动画，入场完成后执行页面切换逻辑，退场结束后执行完成回调。
    /// </summary>
    /// <param name="transitionAction">过渡页完全覆盖后执行的切页逻辑。</param>
    /// <param name="completedAction">过渡页退场销毁后的完成回调。</param>
    /// <param name="stayDuration">入场完成后停留多久再自动退场。</param>
    /// <param name="autoClose">是否自动退场关闭。</param>
    /// <returns>创建出来的过渡页实例。</returns>
    public static MPTransitionView Play(Action transitionAction, Action completedAction = null, float stayDuration = DEFAULT_STAY_DURATION, bool autoClose = true)
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

    /// <summary>
    /// 通过过渡动画打开指定页面，常用于从关卡列表或结算页进入游戏页。
    /// </summary>
    /// <param name="uiMsgData">目标页面需要的数据。</param>
    /// <param name="sourceWindow">触发切页的原页面，目标页打开后会将它隐藏。</param>
    /// <param name="targetLayer">目标页面所在的 UI 层级。</param>
    /// <typeparam name="T">需要打开的目标页面类型。</typeparam>
    public static void OpenWindow<T>(UIMsgData uiMsgData, AWindow sourceWindow = null, UILayer targetLayer = UILayer.Bottom) where T : AWindow
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

    /// <summary>
    /// 读取过渡页参数并开始播放入场动画。
    /// </summary>
    /// <param name="uiMsg">过渡页打开时传入的数据。</param>
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPTransitionViewUIMsgData data = uiMsg as MPTransitionViewUIMsgData;
        if (data != null)
        {
            m_transitionAction = data.transitionAction;
            m_completedAction = data.completedAction;
            m_stayDuration = Mathf.Max(0f, data.stayDuration);
            m_autoClose = data.autoClose;
        }
        else
        {
            m_stayDuration = DEFAULT_STAY_DURATION;
            m_autoClose = true;
        }

        CacheOriginalState();
        PlayOpenAnimation();
    }

    /// <summary>
    /// 外部手动触发关闭过渡页，适用于需要自己控制退场时机的场景。
    /// </summary>
    /// <param name="onClosed">过渡页关闭完成后的额外回调。</param>
    public void PlayClose(Action onClosed = null)
    {
        if (onClosed != null)
        {
            m_completedAction += onClosed;
        }

        PlayCloseAnimation();
    }

    /// <summary>
    /// 页面销毁前清理所有 Tween 和回调引用。
    /// </summary>
    public override void OnRelease()
    {
        KillAllTweens();
        m_transitionAction = null;
        m_completedAction = null;
    }

    /// <summary>
    /// 防止非 UIManager 路径销毁时 Tween 残留。
    /// </summary>
    private void OnDestroy()
    {
        KillAllTweens();
    }

    /// <summary>
    /// 缓存 Prefab 中配置好的最终状态，后续动画都以这些位置和缩放作为目标。
    /// </summary>
    private void CacheOriginalState()
    {
        if (m_hasCachedOriginalState)
            return;

        m_hasCachedOriginalState = true;

        AddCloud(m_leftUpCloud, new Vector2(-1f, 1f));
        AddCloud(m_leftCloud, new Vector2(-1f, 0f));
        AddCloud(m_leftDownCloud, new Vector2(-1f, -1f));
        AddCloud(m_rightUpCloud, new Vector2(1f, 1f));
        AddCloud(m_rightCloud, new Vector2(1f, 0f));
        AddCloud(m_rightDownCloud, new Vector2(1f, -1f));

        if (m_icon != null)
        {
            m_iconOriginalPosition = m_icon.anchoredPosition;
            m_iconOriginalScale = m_icon.localScale;
            m_iconGraphics = m_icon.GetComponentsInChildren<Graphic>(true);
            CacheGraphicAlphas(m_iconGraphics, m_iconOriginalAlphas);
        }

        CollectItems(m_blockRoot);
        CollectItems(m_leafRoot);
        CollectItems(m_starRoot);
    }

    /// <summary>
    /// 添加一个云片动画对象，并根据方向计算它的屏幕外隐藏位置。
    /// </summary>
    /// <param name="cloud">云片节点。</param>
    /// <param name="outDirection">云片退场方向。</param>
    private void AddCloud(RectTransform cloud, Vector2 outDirection)
    {
        if (cloud == null)
            return;

        Vector2 size = cloud.sizeDelta;
        Vector2 offset = new Vector2(
            Mathf.Approximately(outDirection.x, 0f) ? 0f : outDirection.x * (Mathf.Abs(size.x) + CLOUD_OUT_PADDING),
            Mathf.Approximately(outDirection.y, 0f) ? 0f : outDirection.y * (Mathf.Abs(size.y) + CLOUD_OUT_PADDING));

        m_clouds.Add(new CloudState()
        {
            rectTransform = cloud,
            originalPosition = cloud.anchoredPosition,
            hiddenPosition = cloud.anchoredPosition + offset,
        });
    }

    /// <summary>
    /// 收集指定父节点下的装饰物，并缓存每个装饰物的原始状态。
    /// </summary>
    /// <param name="root">装饰物父节点。</param>
    private void CollectItems(RectTransform root)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform item = root.GetChild(i) as RectTransform;
            if (item == null)
                continue;

            ItemState itemState = new ItemState()
            {
                rectTransform = item,
                originalPosition = item.anchoredPosition,
                originalScale = item.localScale,
                originalEulerAngles = item.localEulerAngles,
                graphics = item.GetComponentsInChildren<Graphic>(true),
            };

            CacheGraphicAlphas(itemState.graphics, itemState.originalAlphas);
            m_items.Add(itemState);
        }
    }

    /// <summary>
    /// 缓存一组 Graphic 的原始透明度，方便动画结束后按原比例恢复。
    /// </summary>
    /// <param name="graphics">需要缓存透明度的 Graphic 组件。</param>
    /// <param name="alphas">透明度缓存列表。</param>
    private void CacheGraphicAlphas(Graphic[] graphics, List<float> alphas)
    {
        alphas.Clear();
        if (graphics == null)
            return;

        for (int i = 0; i < graphics.Length; i++)
        {
            alphas.Add(graphics[i] == null ? 1f : graphics[i].color.a);
        }
    }

    /// <summary>
    /// 播放过渡页入场动画。
    /// </summary>
    private void PlayOpenAnimation()
    {
        KillAllTweens();
        m_hasInvokedTransitionAction = false;
        m_isClosing = false;

        PrepareOpenState();

        m_stageSequence = CreateOpenSequence();
        m_stageSequence.OnComplete(OnOpenAnimationCompleted);
    }

    /// <summary>
    /// 将所有节点设置到入场动画的起始状态。
    /// </summary>
    private void PrepareOpenState()
    {
        for (int i = 0; i < m_clouds.Count; i++)
        {
            if (m_clouds[i].rectTransform != null)
            {
                m_clouds[i].rectTransform.anchoredPosition = m_clouds[i].hiddenPosition;
            }
        }

        if (m_icon != null)
        {
            m_icon.anchoredPosition = m_iconOriginalPosition;
            m_icon.localScale = Vector3.zero;
            SetGraphicAlpha(m_iconGraphics, m_iconOriginalAlphas, 0f);
        }

        for (int i = 0; i < m_items.Count; i++)
        {
            ItemState item = m_items[i];
            if (item.rectTransform == null)
                continue;

            item.rectTransform.anchoredPosition = Vector2.zero;
            item.rectTransform.localScale = Vector3.zero;
            item.rectTransform.localEulerAngles = item.originalEulerAngles;
            SetGraphicAlpha(item.graphics, item.originalAlphas, 0f);
        }
    }

    /// <summary>
    /// 创建入场动画序列：云片滑入、图标显示、装饰物从中心展开。
    /// </summary>
    /// <returns>入场动画序列。</returns>
    private Sequence CreateOpenSequence()
    {
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);

        for (int i = 0; i < m_clouds.Count; i++)
        {
            RectTransform cloud = m_clouds[i].rectTransform;
            if (cloud == null)
                continue;

            cloud.DOKill();
            sequence.Join(cloud.DOAnchorPos(m_clouds[i].originalPosition, CLOUD_DURATION).SetEase(Ease.OutSine));
        }

        if (m_icon != null)
        {
            m_icon.DOKill();
            sequence.Join(m_icon.DOScale(m_iconOriginalScale, ICON_DURATION).SetEase(Ease.OutBack));
            InsertGraphicFade(sequence, 0f, m_iconGraphics, m_iconOriginalAlphas, 1f, ICON_DURATION);
        }

        for (int i = 0; i < m_items.Count; i++)
        {
            ItemState item = m_items[i];
            if (item.rectTransform == null)
                continue;

            float delay = 0.08f + i * ITEM_STAGGER_INTERVAL;
            item.rectTransform.DOKill();
            sequence.Insert(delay, item.rectTransform.DOAnchorPos(item.originalPosition, ITEM_SPREAD_DURATION).SetEase(Ease.OutBack));
            sequence.Insert(delay, item.rectTransform.DOScale(item.originalScale, ITEM_SPREAD_DURATION).SetEase(Ease.OutBack));
            InsertGraphicFade(sequence, delay, item.graphics, item.originalAlphas, 1f, ITEM_SPREAD_DURATION);
        }

        return sequence;
    }

    /// <summary>
    /// 入场动画完成后开始装饰物循环动画，并触发真正的页面切换回调。
    /// </summary>
    private void OnOpenAnimationCompleted()
    {
        if (IsDestoried || m_isClosing)
            return;

        StartIdleAnimation();
        InvokeTransitionAction();

        if (!m_autoClose || IsDestoried)
            return;

        m_delayTween?.Kill();
        m_delayTween = DOVirtual.DelayedCall(m_stayDuration, PlayCloseAnimation).SetLink(gameObject);
    }

    /// <summary>
    /// 执行页面切换回调，确保回调只会执行一次。
    /// </summary>
    private void InvokeTransitionAction()
    {
        if (m_hasInvokedTransitionAction)
            return;

        m_hasInvokedTransitionAction = true;

        try
        {
            m_transitionAction?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"MPTransitionView transition action error: {e}");
        }
    }

    /// <summary>
    /// 开始装饰物的轻微浮动、旋转和闪烁循环动画。
    /// </summary>
    private void StartIdleAnimation()
    {
        KillIdleTweens();

        for (int i = 0; i < m_items.Count; i++)
        {
            ItemState item = m_items[i];
            if (item.rectTransform == null)
                continue;

            float seed = i + 1f;
            Vector2 floatPosition = item.originalPosition + new Vector2(
                Mathf.Sin(seed * 1.7f) * 10f,
                Mathf.Cos(seed * 1.3f) * 12f);
            float floatDuration = 1.15f + (i % 4) * 0.12f;
            float rotateAngle = ((i % 2 == 0) ? 1f : -1f) * (5f + (i % 3) * 2f);
            float blinkAlpha = i % 3 == 0 ? 0.45f : 0.65f;

            item.rectTransform.DOKill();
            Tween moveTween = item.rectTransform
                .DOAnchorPos(floatPosition, floatDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
            Tween rotateTween = item.rectTransform
                .DOLocalRotate(item.originalEulerAngles + new Vector3(0f, 0f, rotateAngle), floatDuration + 0.2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
            Tween fadeTween = CreateGraphicsFadeLoop(item.graphics, item.originalAlphas, blinkAlpha, 0.72f + (i % 3) * 0.1f);

            m_idleTweens.Add(moveTween);
            m_idleTweens.Add(rotateTween);
            if (fadeTween != null)
            {
                m_idleTweens.Add(fadeTween);
            }
        }
    }

    /// <summary>
    /// 播放退场动画，所有装饰物和图标收回到中心，云片向外移出。
    /// </summary>
    private void PlayCloseAnimation()
    {
        if (m_isClosing || IsDestoried)
            return;

        m_isClosing = true;
        m_delayTween?.Kill();
        m_delayTween = null;

        m_stageSequence?.Kill();
        m_stageSequence = null;

        KillIdleTweens();
        KillElementTweens();

        m_stageSequence = CreateCloseSequence();
        m_stageSequence.OnComplete(FinishTransition);
    }

    /// <summary>
    /// 创建退场动画序列。
    /// </summary>
    /// <returns>退场动画序列。</returns>
    private Sequence CreateCloseSequence()
    {
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);

        for (int i = 0; i < m_items.Count; i++)
        {
            ItemState item = m_items[i];
            if (item.rectTransform == null)
                continue;

            item.rectTransform.DOKill();
            sequence.Join(item.rectTransform.DOAnchorPos(Vector2.zero, CLOSE_DURATION).SetEase(Ease.InBack));
            sequence.Join(item.rectTransform.DOScale(Vector3.zero, CLOSE_DURATION).SetEase(Ease.InBack));
            InsertGraphicFade(sequence, 0f, item.graphics, item.originalAlphas, 0f, CLOSE_DURATION);
        }

        if (m_icon != null)
        {
            m_icon.DOKill();
            sequence.Join(m_icon.DOScale(Vector3.zero, CLOSE_DURATION).SetEase(Ease.InBack));
            InsertGraphicFade(sequence, 0f, m_iconGraphics, m_iconOriginalAlphas, 0f, CLOSE_DURATION);
        }

        for (int i = 0; i < m_clouds.Count; i++)
        {
            RectTransform cloud = m_clouds[i].rectTransform;
            if (cloud == null)
                continue;

            cloud.DOKill();
            sequence.Join(cloud.DOAnchorPos(m_clouds[i].hiddenPosition, CLOUD_DURATION).SetEase(Ease.InSine));
        }

        return sequence;
    }

    /// <summary>
    /// 退场动画结束后销毁过渡页，并执行完成回调。
    /// </summary>
    private void FinishTransition()
    {
        Action completedAction = m_completedAction;
        DestroyWindow();
        completedAction?.Invoke();
    }

    /// <summary>
    /// 在指定动画序列时间点插入一组 Graphic 的透明度动画。
    /// </summary>
    /// <param name="sequence">目标动画序列。</param>
    /// <param name="atPosition">插入到序列中的时间点。</param>
    /// <param name="graphics">参与透明度变化的 Graphic。</param>
    /// <param name="originalAlphas">Graphic 原始透明度。</param>
    /// <param name="alphaRate">目标透明度倍率。</param>
    /// <param name="duration">动画时长。</param>
    private void InsertGraphicFade(Sequence sequence, float atPosition, Graphic[] graphics, List<float> originalAlphas, float alphaRate, float duration)
    {
        if (sequence == null || graphics == null)
            return;

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            float targetAlpha = GetOriginalAlpha(originalAlphas, i) * alphaRate;
            graphic.DOKill();
            sequence.Insert(atPosition, graphic.DOFade(targetAlpha, duration).SetEase(Ease.Linear));
        }
    }

    /// <summary>
    /// 创建一组 Graphic 的循环闪烁动画。
    /// </summary>
    /// <param name="graphics">参与闪烁的 Graphic。</param>
    /// <param name="originalAlphas">Graphic 原始透明度。</param>
    /// <param name="alphaRate">闪烁时的最低透明度倍率。</param>
    /// <param name="duration">单次闪烁动画时长。</param>
    /// <returns>循环闪烁 Tween。</returns>
    private Tween CreateGraphicsFadeLoop(Graphic[] graphics, List<float> originalAlphas, float alphaRate, float duration)
    {
        if (graphics == null || graphics.Length == 0)
            return null;

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        bool hasTween = false;

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            hasTween = true;
            graphic.DOKill();
            sequence.Join(graphic.DOFade(GetOriginalAlpha(originalAlphas, i) * alphaRate, duration).SetEase(Ease.InOutSine));
        }

        if (!hasTween)
        {
            sequence.Kill();
            return null;
        }

        return sequence.SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>
    /// 立即设置一组 Graphic 的透明度。
    /// </summary>
    /// <param name="graphics">目标 Graphic。</param>
    /// <param name="originalAlphas">Graphic 原始透明度。</param>
    /// <param name="alphaRate">目标透明度倍率。</param>
    private void SetGraphicAlpha(Graphic[] graphics, List<float> originalAlphas, float alphaRate)
    {
        if (graphics == null)
            return;

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            Color color = graphic.color;
            color.a = GetOriginalAlpha(originalAlphas, i) * alphaRate;
            graphic.color = color;
        }
    }

    /// <summary>
    /// 获取指定 Graphic 的原始透明度。
    /// </summary>
    /// <param name="originalAlphas">透明度缓存列表。</param>
    /// <param name="index">Graphic 下标。</param>
    /// <returns>原始透明度。</returns>
    private float GetOriginalAlpha(List<float> originalAlphas, int index)
    {
        if (originalAlphas == null || index < 0 || index >= originalAlphas.Count)
            return 1f;

        return originalAlphas[index];
    }

    /// <summary>
    /// 清理当前界面上所有正在播放或等待播放的 Tween。
    /// </summary>
    private void KillAllTweens()
    {
        m_stageSequence?.Kill();
        m_stageSequence = null;

        m_delayTween?.Kill();
        m_delayTween = null;

        KillIdleTweens();
        KillElementTweens();
    }

    /// <summary>
    /// 清理装饰物展开后的循环 Tween。
    /// </summary>
    private void KillIdleTweens()
    {
        for (int i = 0; i < m_idleTweens.Count; i++)
        {
            m_idleTweens[i]?.Kill();
        }

        m_idleTweens.Clear();
    }

    /// <summary>
    /// 清理所有节点和 Graphic 上可能残留的 Tween。
    /// </summary>
    private void KillElementTweens()
    {
        for (int i = 0; i < m_clouds.Count; i++)
        {
            if (m_clouds[i].rectTransform != null)
            {
                m_clouds[i].rectTransform.DOKill();
            }
        }

        if (m_icon != null)
        {
            m_icon.DOKill();
        }

        KillGraphicsTweens(m_iconGraphics);

        for (int i = 0; i < m_items.Count; i++)
        {
            if (m_items[i].rectTransform != null)
            {
                m_items[i].rectTransform.DOKill();
            }

            KillGraphicsTweens(m_items[i].graphics);
        }
    }

    /// <summary>
    /// 清理一组 Graphic 上的透明度 Tween。
    /// </summary>
    /// <param name="graphics">需要清理 Tween 的 Graphic。</param>
    private void KillGraphicsTweens(Graphic[] graphics)
    {
        if (graphics == null)
            return;

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].DOKill();
            }
        }
    }

    /// <summary>
    /// 云片动画状态缓存。
    /// </summary>
    private class CloudState
    {
        /// <summary>
        /// 云片的 RectTransform。
        /// </summary>
        public RectTransform rectTransform;

        /// <summary>
        /// 云片在 Prefab 中配置的目标位置。
        /// </summary>
        public Vector2 originalPosition;

        /// <summary>
        /// 云片滑出屏幕后的隐藏位置。
        /// </summary>
        public Vector2 hiddenPosition;
    }

    /// <summary>
    /// 装饰物动画状态缓存。
    /// </summary>
    private class ItemState
    {
        /// <summary>
        /// 装饰物的 RectTransform。
        /// </summary>
        public RectTransform rectTransform;

        /// <summary>
        /// 装饰物在 Prefab 中配置的目标位置。
        /// </summary>
        public Vector2 originalPosition;

        /// <summary>
        /// 装饰物在 Prefab 中配置的原始缩放。
        /// </summary>
        public Vector3 originalScale;

        /// <summary>
        /// 装饰物在 Prefab 中配置的原始旋转。
        /// </summary>
        public Vector3 originalEulerAngles;

        /// <summary>
        /// 装饰物下所有需要参与透明度动画的 Graphic。
        /// </summary>
        public Graphic[] graphics;

        /// <summary>
        /// 装饰物下所有 Graphic 的原始透明度。
        /// </summary>
        public readonly List<float> originalAlphas = new List<float>();
    }
}

/// <summary>
/// 过渡页打开时传入的数据。
/// </summary>
public class MPTransitionViewUIMsgData : UIMsgData
{
    /// <summary>
    /// 过渡页入场完成后执行的页面切换回调。
    /// </summary>
    public Action transitionAction;

    /// <summary>
    /// 过渡页退场结束并销毁后的回调。
    /// </summary>
    public Action completedAction;

    /// <summary>
    /// 入场完成后停留多久再自动退场。
    /// </summary>
    public float stayDuration = 0.45f;

    /// <summary>
    /// 是否在停留结束后自动播放退场并关闭。
    /// </summary>
    public bool autoClose = true;
}
