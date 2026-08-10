using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主页关卡地图的云朵视差控制器。
/// 云朵和影子节点全部预先搭建在 Prefab 中，脚本只负责位置与漂浮动画。
/// </summary>
[DisallowMultipleComponent]
public class MPHomeParallaxController : MonoBehaviour
{
    private const int FAR_CLOUD_COUNT = 2;
    private const float FAR_PARALLAX = 0.08f;
    private const float NEAR_PARALLAX = 0.18f;
    private const float CLOUD_WRAP_PADDING = 40f;

    private sealed class CloudItem
    {
        public RectTransform cloud;
        public RectTransform shadow;
        public float baseX;
        public float baseY;
        public float parallax;
    }

    [Header("Prefab 节点")]
    [SerializeField]
    private RectTransform m_cloudRoot;

    [SerializeField]
    private RectTransform m_shadowRoot;

    [SerializeField]
    private RectTransform[] m_clouds;

    [SerializeField]
    private RectTransform[] m_shadows;

    [Header("影子")]
    [SerializeField]
    private float m_shadowVerticalOffset = -170f;

    private readonly List<CloudItem> m_cloudItems = new List<CloudItem>();
    private readonly List<Tween> m_cloudTweens = new List<Tween>();

    private ScrollRect m_scrollRect;
    private float m_initialContentY;
    private bool m_initialized;

    public void Initialize(ScrollRect scrollRect)
    {
        if (scrollRect == null || scrollRect.content == null)
            return;

        if (!ValidatePrefabReferences())
            return;

        UnsubscribeScroll();
        KillCloudTweens();

        m_scrollRect = scrollRect;
        m_initialContentY = m_scrollRect.content.anchoredPosition.y;
        CacheCloudItems();

        m_initialized = true;
        SubscribeScroll();
        ResetHorizontalPositions();
        RefreshCloudPositions();
        StartCloudTweens();
    }

    public void Shutdown()
    {
        m_initialized = false;
        UnsubscribeScroll();
        KillCloudTweens();
        m_scrollRect = null;
    }

    private bool ValidatePrefabReferences()
    {
        if (m_cloudRoot == null || m_shadowRoot == null)
        {
            Debug.LogError("MPHomeParallaxController 缺少 CloudRoot 或 CloudShadowRoot 引用", this);
            return false;
        }

        if (m_clouds == null || m_shadows == null || m_clouds.Length == 0
            || m_clouds.Length != m_shadows.Length)
        {
            Debug.LogError("MPHomeParallaxController 的云朵与影子数量不一致", this);
            return false;
        }

        return true;
    }

    private void CacheCloudItems()
    {
        m_cloudItems.Clear();
        for (int i = 0; i < m_clouds.Length; i++)
        {
            RectTransform cloud = m_clouds[i];
            RectTransform shadow = m_shadows[i];
            if (cloud == null || shadow == null)
                continue;

            bool isFarCloud = i < FAR_CLOUD_COUNT;
            m_cloudItems.Add(new CloudItem
            {
                cloud = cloud,
                shadow = shadow,
                baseX = cloud.anchoredPosition.x,
                baseY = cloud.anchoredPosition.y,
                parallax = isFarCloud ? FAR_PARALLAX : NEAR_PARALLAX,
            });
        }
    }

    private void SubscribeScroll()
    {
        if (m_scrollRect == null)
            return;

        m_scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        m_scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
    }

    private void UnsubscribeScroll()
    {
        if (m_scrollRect != null)
        {
            m_scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        }
    }

    private void OnScrollValueChanged(Vector2 _)
    {
        RefreshCloudPositions();
    }

    private void RefreshCloudPositions()
    {
        if (m_scrollRect == null || m_scrollRect.content == null)
            return;

        float contentDeltaY = m_scrollRect.content.anchoredPosition.y - m_initialContentY;
        for (int i = 0; i < m_cloudItems.Count; i++)
        {
            CloudItem item = m_cloudItems[i];
            Vector2 cloudPosition = item.cloud.anchoredPosition;
            cloudPosition.y = WrapCloudVerticalPosition(
                item,
                item.baseY + contentDeltaY * item.parallax);
            item.cloud.anchoredPosition = cloudPosition;

            RefreshShadowVerticalPosition(item);
        }
    }

    /// <summary>
    /// 影子位于 Bg/Floor 的 Mask 子节点下，通过坐标转换跟随对应云朵。
    /// Floor 的图片透明区域会自动裁掉影子。
    /// </summary>
    private void RefreshShadowVerticalPosition(CloudItem item)
    {
        Vector3 shadowLocalPosition = m_shadowRoot.InverseTransformPoint(item.cloud.position);
        Vector2 shadowPosition = item.shadow.anchoredPosition;
        shadowPosition.y = shadowLocalPosition.y + m_shadowVerticalOffset;
        item.shadow.anchoredPosition = shadowPosition;
    }

    private void ResetHorizontalPositions()
    {
        for (int i = 0; i < m_cloudItems.Count; i++)
        {
            CloudItem item = m_cloudItems[i];
            Vector2 cloudPosition = item.cloud.anchoredPosition;
            cloudPosition.x = item.baseX;
            item.cloud.anchoredPosition = cloudPosition;

            Vector3 shadowLocalPosition = m_shadowRoot.InverseTransformPoint(item.cloud.position);
            Vector2 shadowPosition = item.shadow.anchoredPosition;
            shadowPosition.x = shadowLocalPosition.x;
            item.shadow.anchoredPosition = shadowPosition;
        }
    }

    private void StartCloudTweens()
    {
        KillCloudTweens();
        ResetHorizontalPositions();

        for (int i = 0; i < m_cloudItems.Count; i++)
        {
            CloudItem item = m_cloudItems[i];
            float distance = 30f + i % 3 * 18f;
            float direction = i % 2 == 0 ? 1f : -1f;
            float duration = 7.5f + i * 1.35f;

            Tween cloudTween = item.cloud
                .DOAnchorPosX(item.cloud.anchoredPosition.x + distance * direction, duration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(gameObject);
            Tween shadowTween = item.shadow
                .DOAnchorPosX(item.shadow.anchoredPosition.x + distance * direction, duration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(gameObject);

            m_cloudTweens.Add(cloudTween);
            m_cloudTweens.Add(shadowTween);
        }
    }

    private void KillCloudTweens()
    {
        for (int i = 0; i < m_cloudTweens.Count; i++)
        {
            Tween tween = m_cloudTweens[i];
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }

        m_cloudTweens.Clear();
    }

    /// <summary>
    /// 云朵完全离开视口后才循环到另一侧，并在视口外预留少量距离。
    /// 这样云朵会从边缘逐渐滑入，不会直接出现在顶部可见区域。
    /// </summary>
    private float WrapCloudVerticalPosition(CloudItem item, float value)
    {
        Rect rootRect = m_cloudRoot.rect;
        float cloudHalfHeight = item.cloud.rect.height
            * Mathf.Abs(item.cloud.localScale.y)
            * 0.5f;
        float minY = rootRect.yMin - cloudHalfHeight - CLOUD_WRAP_PADDING;
        float maxY = rootRect.yMax + cloudHalfHeight + CLOUD_WRAP_PADDING;
        float cycleHeight = maxY - minY;
        if (cycleHeight <= 0f)
            return value;

        return Mathf.Repeat(value - minY, cycleHeight) + minY;
    }

    private void OnEnable()
    {
        if (!m_initialized)
            return;

        SubscribeScroll();
        RefreshCloudPositions();
        StartCloudTweens();
    }

    private void OnDisable()
    {
        UnsubscribeScroll();
        KillCloudTweens();
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
