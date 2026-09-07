using DG.Tweening;
using HQ.UIManager;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

/// <summary>普通与大图游戏页共用的开场动画和行列完成波浪。</summary>
public abstract partial class MPGameViewBase
{
    private const float GAME_ENTER_TOTAL_DURATION = 1f;
    private const float GAME_ENTER_ITEM_DURATION = 0.3f;
    private const float GAME_ENTER_LINE_FADE_DURATION = 0.15f;
    private const float GAME_ENTER_CONTENT_END_TIME =
        GAME_ENTER_TOTAL_DURATION - GAME_ENTER_LINE_FADE_DURATION;
    private const float GAME_ENTER_WAVE_SPAN =
        GAME_ENTER_CONTENT_END_TIME - GAME_ENTER_ITEM_DURATION;

    private const float LINE_ANIMATION_STEP_DELAY = 0.045f;
    private const float LINE_ANIMATION_SCALE_DURATION = 0.15f;
    private const float LINE_ANIMATION_HOLD_DURATION = 0.05f;
    private const float LINE_ANIMATION_FADE_DURATION = 0.1f;
    private const float LINE_ANIMATION_TARGET_SCALE = 1.1f;

    // UIManager 会从具体页面类型反射字段，基类节点引用必须可被派生类继承后才能自动绑定。
    [TransformPath("View/Content/Animation")]
    protected RectTransform m_lineCompleteAnimationRoot;

    [TransformPath("View/Content/Animation/Item")]
    protected RectTransform m_lineCompleteAnimationTemplate;

    private Sequence m_gameEnterSequence;
    private CanvasGroup m_gameEnterLineCanvasGroup;
    private bool m_gameEnterAnimationPrepared;

    private ObjectPool<RectTransform> m_lineCompleteAnimationPool;
    private readonly List<RectTransform> m_activeLineCompleteAnimationItems =
        new List<RectTransform>();
    private readonly Dictionary<RectTransform, Sequence> m_lineCompleteAnimationSequences =
        new Dictionary<RectTransform, Sequence>();
    private Color m_lineCompleteAnimationColor = Color.white;

    /// <summary>创建页面时只设置初始状态，避免动画在过渡页遮挡期间提前播放。</summary>
    private void PrepareGameEnterAnimation()
    {
        StopGameEnterAnimation();
        if (m_blockGrid == null || m_blockGrid.transform.childCount == 0)
            return;

        SetChildrenScale(m_numberVertical, Vector3.zero);
        SetChildrenScale(m_numberHorizontal, Vector3.zero);
        SetChildrenScale(m_blockGrid.transform as RectTransform, Vector3.zero);

        m_gameEnterLineCanvasGroup = m_lineNode == null
            ? null
            : m_lineNode.GetComponent<CanvasGroup>();
        if (m_gameEnterLineCanvasGroup != null)
        {
            m_lineNode.gameObject.SetActive(true);
            m_gameEnterLineCanvasGroup.DOKill();
            m_gameEnterLineCanvasGroup.alpha = 0f;
        }

        m_gameEnterAnimationPrepared = true;
    }

    /// <summary>
    /// 由过渡页面退场完成回调触发。数字栏与网格按方向波浪展开，最后淡入分隔线，
    /// 整段动画固定为一秒。
    /// </summary>
    public void PlayEnterAnimationAfterTransition()
    {
        if (!m_gameEnterAnimationPrepared || m_gameEnterSequence != null)
            return;

        if (this == null || IsDestoried || !isActiveAndEnabled)
        {
            StopGameEnterAnimation();
            return;
        }

        m_gameEnterAnimationPrepared = false;
        Sequence sequence = DOTween.Sequence();
        sequence.AppendInterval(GAME_ENTER_TOTAL_DURATION);

        InsertChildrenScaleAnimations(sequence, m_numberVertical);
        InsertChildrenScaleAnimations(sequence, m_numberHorizontal);
        InsertGridScaleAnimations(sequence);

        if (m_gameEnterLineCanvasGroup != null)
        {
            sequence.Insert(
                GAME_ENTER_CONTENT_END_TIME,
                m_gameEnterLineCanvasGroup
                    .DOFade(1f, GAME_ENTER_LINE_FADE_DURATION)
                    .SetEase(Ease.Linear));
        }

        sequence.SetUpdate(true);
        sequence.SetLink(gameObject);
        m_gameEnterSequence = sequence;
        sequence.OnComplete(() =>
        {
            if (m_gameEnterSequence == sequence)
                m_gameEnterSequence = null;
        });
    }

    private static void InsertChildrenScaleAnimations(Sequence sequence, RectTransform container)
    {
        if (container == null)
            return;

        int count = container.childCount;
        for (int i = 0; i < count; i++)
        {
            sequence.Insert(
                GetGameEnterDelay(i, count),
                container.GetChild(i)
                    .DOScale(Vector3.one, GAME_ENTER_ITEM_DURATION)
                    .SetEase(Ease.OutBack));
        }
    }

    private void InsertGridScaleAnimations(Sequence sequence)
    {
        if (m_blockGrid == null)
            return;

        Transform grid = m_blockGrid.transform;
        int gridSize = Mathf.Max(1, VisibleGridSize);
        int diagonalCount = gridSize * 2 - 1;
        for (int diagonal = 0; diagonal < diagonalCount; diagonal++)
        {
            float delay = GetGameEnterDelay(diagonal, diagonalCount);
            for (int row = 0; row < gridSize; row++)
            {
                int column = diagonal - row;
                if (column < 0 || column >= gridSize)
                    continue;

                int index = row * gridSize + column;
                if (index < 0 || index >= grid.childCount)
                    continue;

                sequence.Insert(
                    delay,
                    grid.GetChild(index)
                        .DOScale(Vector3.one, GAME_ENTER_ITEM_DURATION)
                        .SetEase(Ease.OutBack));
            }
        }
    }

    private static float GetGameEnterDelay(int index, int count)
    {
        return count <= 1 ? 0f : GAME_ENTER_WAVE_SPAN * index / (count - 1f);
    }

    /// <summary>停止开场动画并恢复最终显示状态。</summary>
    protected void StopGameEnterAnimation()
    {
        m_gameEnterSequence?.Kill();
        m_gameEnterSequence = null;
        m_gameEnterAnimationPrepared = false;

        SetChildrenScale(m_numberVertical, Vector3.one);
        SetChildrenScale(m_numberHorizontal, Vector3.one);
        SetChildrenScale(m_blockGrid == null ? null : m_blockGrid.transform as RectTransform, Vector3.one);

        if (m_gameEnterLineCanvasGroup != null)
        {
            m_gameEnterLineCanvasGroup.DOKill();
            m_gameEnterLineCanvasGroup.alpha = 1f;
        }
    }

    private static void SetChildrenScale(RectTransform container, Vector3 scale)
    {
        if (container == null)
            return;

        for (int i = 0; i < container.childCount; i++)
            container.GetChild(i).localScale = scale;
    }

    /// <summary>使用当前模式的填充图片初始化行列动画对象池。</summary>
    protected void InitializeLineCompleteAnimationPool(Sprite fillSprite)
    {
        ReleaseLineCompleteAnimationPool();
        if (m_lineCompleteAnimationRoot == null || m_lineCompleteAnimationTemplate == null)
        {
            Debug.LogWarning($"[{GetType().Name}] 缺少 Content/Animation 或 Item，已跳过行列完成动画。");
            return;
        }

        Image templateImage = m_lineCompleteAnimationTemplate.GetComponent<Image>();
        if (templateImage == null)
        {
            Debug.LogWarning($"[{GetType().Name}] Content/Animation/Item 缺少 Image，已跳过行列完成动画。");
            return;
        }

        if (fillSprite != null)
            templateImage.sprite = fillSprite;
        templateImage.raycastTarget = false;
        m_lineCompleteAnimationColor = templateImage.color;
        m_lineCompleteAnimationTemplate.gameObject.SetActive(false);

        int prewarmCount = Mathf.Max(1, VisibleGridSize * 2 - 1);
        m_lineCompleteAnimationPool = new ObjectPool<RectTransform>(
            CreateLineCompleteAnimationItem,
            GetLineCompleteAnimationItem,
            ReleaseLineCompleteAnimationItem,
            DestroyLineCompleteAnimationItem,
            collectionCheck: false,
            defaultCapacity: prewarmCount,
            maxSize: prewarmCount * 3);

        List<RectTransform> prewarmItems = new List<RectTransform>(prewarmCount);
        for (int i = 0; i < prewarmCount; i++)
            prewarmItems.Add(m_lineCompleteAnimationPool.Get());
        for (int i = prewarmItems.Count - 1; i >= 0; i--)
            m_lineCompleteAnimationPool.Release(prewarmItems[i]);
    }

    /// <summary>以最后操作格为起点，沿新完成的可视行列同时向两侧扩散。</summary>
    protected void PlayLineCompleteAnimation(
        RectTransform originBlock,
        int originRow,
        int originColumn,
        bool completedColumn,
        bool completedRow)
    {
        if (originBlock == null || m_lineCompleteAnimationPool == null ||
            !isActiveAndEnabled || m_hasCompleted || m_isRestoringProgress ||
            (!completedColumn && !completedRow))
            return;

        PlayLineCompleteAnimationItem(originBlock, 0f);
        int gridSize = VisibleGridSize;
        for (int distance = 1; distance < gridSize; distance++)
        {
            float delay = distance * LINE_ANIMATION_STEP_DELAY;
            if (completedRow)
            {
                PlayLineCompleteAnimationItemAt(originRow, originColumn - distance, delay);
                PlayLineCompleteAnimationItemAt(originRow, originColumn + distance, delay);
            }

            if (completedColumn)
            {
                PlayLineCompleteAnimationItemAt(originRow - distance, originColumn, delay);
                PlayLineCompleteAnimationItemAt(originRow + distance, originColumn, delay);
            }
        }
    }

    private void PlayLineCompleteAnimationItemAt(int row, int column, float delay)
    {
        int gridSize = VisibleGridSize;
        if (m_blockGrid == null || row < 0 || row >= gridSize || column < 0 || column >= gridSize)
            return;

        int index = row * gridSize + column;
        Transform grid = m_blockGrid.transform;
        if (index < 0 || index >= grid.childCount || !(grid.GetChild(index) is RectTransform block))
            return;

        PlayLineCompleteAnimationItem(block, delay);
    }

    private void PlayLineCompleteAnimationItem(RectTransform block, float delay)
    {
        RectTransform item = m_lineCompleteAnimationPool.Get();
        Image image = item.GetComponent<Image>();
        if (image == null)
        {
            RecycleLineCompleteAnimationItem(item);
            return;
        }

        AlignLineCompleteAnimationItem(item, block);
        item.SetAsLastSibling();

        Sequence sequence = DOTween.Sequence();
        m_lineCompleteAnimationSequences[item] = sequence;
        if (delay > 0f)
            sequence.AppendInterval(delay);
        sequence.Append(item
            .DOScale(Vector3.one * LINE_ANIMATION_TARGET_SCALE, LINE_ANIMATION_SCALE_DURATION)
            .SetEase(Ease.OutSine));
        sequence.AppendInterval(LINE_ANIMATION_HOLD_DURATION);
        sequence.Append(image
            .DOFade(0f, LINE_ANIMATION_FADE_DURATION)
            .SetEase(Ease.OutQuad));
        sequence.SetUpdate(true);
        sequence.SetLink(gameObject);
        sequence.OnComplete(() => CompleteLineCompleteAnimationItem(item, sequence));
    }

    private void AlignLineCompleteAnimationItem(RectTransform item, RectTransform block)
    {
        Vector3 center = m_lineCompleteAnimationRoot.InverseTransformPoint(
            block.TransformPoint(block.rect.center));
        Vector3 width = m_lineCompleteAnimationRoot.InverseTransformVector(
            block.TransformVector(Vector3.right * block.rect.width));
        Vector3 height = m_lineCompleteAnimationRoot.InverseTransformVector(
            block.TransformVector(Vector3.up * block.rect.height));

        item.anchorMin = item.anchorMax = item.pivot = new Vector2(0.5f, 0.5f);
        item.sizeDelta = new Vector2(width.magnitude, height.magnitude);
        item.localRotation = Quaternion.Inverse(m_lineCompleteAnimationRoot.rotation) * block.rotation;
        item.localPosition = new Vector3(center.x, center.y, 0f);
    }

    private RectTransform CreateLineCompleteAnimationItem()
    {
        RectTransform item = Instantiate(m_lineCompleteAnimationTemplate, m_lineCompleteAnimationRoot);
        item.name = "Item_Pooled";
        item.gameObject.SetActive(false);
        return item;
    }

    private void GetLineCompleteAnimationItem(RectTransform item)
    {
        if (item == null)
            return;

        item.DOKill();
        item.localScale = Vector3.zero;
        Image image = item.GetComponent<Image>();
        if (image != null)
        {
            image.DOKill();
            image.color = m_lineCompleteAnimationColor;
        }

        item.gameObject.SetActive(true);
        m_activeLineCompleteAnimationItems.Add(item);
    }

    private void ReleaseLineCompleteAnimationItem(RectTransform item)
    {
        if (item == null)
            return;

        item.DOKill();
        item.GetComponent<Image>()?.DOKill();
        item.localScale = Vector3.zero;
        item.gameObject.SetActive(false);
        m_activeLineCompleteAnimationItems.Remove(item);
    }

    private static void DestroyLineCompleteAnimationItem(RectTransform item)
    {
        if (item != null)
            Destroy(item.gameObject);
    }

    private void CompleteLineCompleteAnimationItem(RectTransform item, Sequence sequence)
    {
        if (item == null || m_lineCompleteAnimationPool == null ||
            !m_lineCompleteAnimationSequences.TryGetValue(item, out Sequence current) ||
            current != sequence)
            return;

        m_lineCompleteAnimationSequences.Remove(item);
        m_lineCompleteAnimationPool.Release(item);
    }

    private void RecycleLineCompleteAnimationItem(RectTransform item)
    {
        if (item == null)
            return;

        if (m_lineCompleteAnimationSequences.TryGetValue(item, out Sequence sequence))
        {
            m_lineCompleteAnimationSequences.Remove(item);
            sequence?.Kill();
        }

        if (m_lineCompleteAnimationPool != null && m_activeLineCompleteAnimationItems.Contains(item))
            m_lineCompleteAnimationPool.Release(item);
    }

    /// <summary>终止当前仍在播放的行列波浪。</summary>
    protected void StopLineCompleteAnimations()
    {
        for (int i = m_activeLineCompleteAnimationItems.Count - 1; i >= 0; i--)
            RecycleLineCompleteAnimationItem(m_activeLineCompleteAnimationItems[i]);
    }

    /// <summary>关闭页面或重新初始化时清理对象池。</summary>
    private void ReleaseLineCompleteAnimationPool()
    {
        StopLineCompleteAnimations();
        m_lineCompleteAnimationSequences.Clear();
        m_activeLineCompleteAnimationItems.Clear();
        m_lineCompleteAnimationPool?.Clear();
        m_lineCompleteAnimationPool = null;

        if (m_lineCompleteAnimationTemplate != null)
            m_lineCompleteAnimationTemplate.gameObject.SetActive(false);
    }

    protected virtual void OnDisable()
    {
        StopGameEnterAnimation();
        StopLineCompleteAnimations();
    }
}
