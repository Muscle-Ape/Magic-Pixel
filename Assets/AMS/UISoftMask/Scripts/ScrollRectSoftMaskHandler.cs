using UnityEngine;
using UnityEngine.UI;

namespace AMS.UI.SoftMask
{
    [ExecuteAlways,
     AddComponentMenu("UI/AMS/Scroll Rect Soft Mask Handler"),
     RequireComponent(typeof(ScrollRect))]
    public class ScrollRectSoftMaskHandler : MonoBehaviour
    {
        [SerializeField,
         Tooltip("Viewport margin threshold (in pixels) for visibility check.\n" +
                 "Positive = earlier / outside viewport;\n" +
                 "Negative = later / inside viewport.")]
        private float m_MarginThreshold = 0;

        private ScrollRect m_ScrollRect;
        private UISoftMask[] m_SoftMasks = { };

        private readonly Vector3[] m_ViewportCorners = new Vector3[4];
        private readonly Vector3[] m_ChildCorners = new Vector3[4];

        private Vector3 m_ViewportMin;
        private Vector3 m_ViewportMax;

        private RectTransform m_ViewportRect;
        private Transform m_ContentRect;

        private void OnEnable()
        {
            if (!m_ScrollRect)
                m_ScrollRect = GetComponent<ScrollRect>();

            if (!m_ViewportRect && m_ScrollRect.viewport is { } viewport)
                m_ViewportRect = viewport;

            if (!m_ContentRect && m_ScrollRect.content is { } content)
                m_ContentRect = content;

            GetSoftMaskChildren();

            Canvas.willRenderCanvases += CheckMasksInsideViewport;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= CheckMasksInsideViewport;

            for (var i = 0; i < m_SoftMasks.Length; i++)
            {
                if (m_SoftMasks[i] is not { } mask)
                    continue;

                mask.SetMaskToActive(true);
            }
        }

        private void OnTransformChildrenChanged()
        {
            GetSoftMaskChildren();
        }

        private void GetSoftMaskChildren()
        {
            if (!m_ViewportRect)
                return;

            m_SoftMasks = m_ViewportRect.GetComponentsInChildren<UISoftMask>(true);
        }

        private void CheckViewportData()
        {
            if (!m_ViewportRect && m_ScrollRect.viewport is { } viewport)
                m_ViewportRect = viewport;

            if (!m_ViewportRect)
                return;

            m_ViewportRect.GetWorldCorners(m_ViewportCorners);

            m_ViewportMin = m_ViewportCorners[0]; // Bottom-left
            m_ViewportMax = m_ViewportCorners[2]; // Top-right
        }

        private void CheckMasksInsideViewport()
        {
            CheckViewportData();

            for (var i = 0; i < m_SoftMasks.Length; i++)
            {
                if (m_SoftMasks[i] is not { } mask)
                    continue;

                mask.SetMaskToActive(IsRectInsideViewport(mask.rectTransform));
            }
        }

        private bool IsRectInsideViewport(RectTransform child)
        {
            if (!child)
                return false;

            child.GetWorldCorners(m_ChildCorners);

            var threshold = new Vector3(m_MarginThreshold, m_MarginThreshold, 0f);
            Vector2 viewMin = m_ViewportMin - threshold;
            Vector2 viewMax = m_ViewportMax + threshold;

            Vector2 childMin = m_ChildCorners[0];
            Vector2 childMax = m_ChildCorners[2];

            return childMax.x >= viewMin.x && childMin.x <= viewMax.x &&
                   childMax.y >= viewMin.y && childMin.y <= viewMax.y;
        }

        private void OnDrawGizmosSelected()
        {
            if (!m_ViewportRect || !enabled)
                return;

            m_ViewportRect.GetWorldCorners(m_ViewportCorners);

            var min = m_ViewportCorners[0];
            var max = m_ViewportCorners[2];

            var threshold = new Vector3(m_MarginThreshold, m_MarginThreshold, 0);
            var center = (min + max) * 0.5f;
            var size = (max - min) + threshold * 2f;
            size.z = 0f;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, size);
        }
    }
}