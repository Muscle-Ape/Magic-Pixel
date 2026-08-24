using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 将 UGUI RawImage/透明 Image 上的屏幕输入转换为 0-1 视口坐标。
/// 单指由 WorldController 在长按移动、持续选择旋转与镜头环绕之间路由；双指处理平移和缩放，
/// 一旦进入双指手势，必须全部抬起后才恢复单指操作，避免误选零件。
/// </summary>
public sealed class MPThreeDViewportInput : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler,
    IPointerClickHandler,
    IInitializePotentialDragHandler,
    IScrollHandler
{
    private const float LONG_PRESS_DURATION = 0.4f;
    private const float DEFAULT_LONG_PRESS_MOVE_THRESHOLD = 18f;
    private const float DOUBLE_TAP_MAX_DURATION = 0.35f;
    private const int INVALID_POINTER_ID = int.MinValue;
    private const string EMPTY_TAP_TARGET_ID = "\u0000EMPTY";

    private readonly Dictionary<int, Vector2> m_pointerPositions =
        new Dictionary<int, Vector2>();

    private MPThreeDWorldController m_worldController;
    private RectTransform m_viewportRect;
    private Coroutine m_longPressCoroutine;
    private int m_longPressPointerId = INVALID_POINTER_ID;
    private Vector2 m_longPressStartScreenPosition;
    private Vector2 m_longPressStartViewportPosition;
    private bool m_suppressSinglePointer;
    private bool m_singlePointerRouted;
    private bool m_holdCompleted;
    private bool m_persistentRotationDragActivated;
    private Vector2 m_singlePressScreenPosition;
    private bool m_currentTapEligible;
    private int m_releasedPointerId = INVALID_POINTER_ID;
    private Vector2 m_releasedTapScreenPosition;
    private bool m_releasedTapEligible;
    private string m_firstTapTargetId;
    private Vector2 m_firstTapScreenPosition;
    private float m_firstTapTime = float.MinValue;
    private int m_tapCount;
    private int m_navigationPointerA = INVALID_POINTER_ID;
    private int m_navigationPointerB = INVALID_POINTER_ID;
    private bool m_middlePanActive;
    private int m_middlePointerId = INVALID_POINTER_ID;
    private Vector2 m_middleLastViewportPosition;

    public void Initialize(
        MPThreeDWorldController worldController,
        RectTransform viewportRect)
    {
        StopLongPressTimer();
        m_worldController = worldController;
        m_viewportRect = viewportRect != null
            ? viewportRect
            : transform as RectTransform;
        m_pointerPositions.Clear();
        m_suppressSinglePointer = false;
        ResetNavigationPointers();
        ResetMiddlePan();
        ResetTapSequence();
        ResetReleasedTap();
        ResetSingleGestureFlags();
    }

    public void Shutdown()
    {
        CancelCurrentInteraction();
        m_pointerPositions.Clear();
        m_suppressSinglePointer = false;
        ResetNavigationPointers();
        ResetMiddlePan();
        ResetTapSequence();
        ResetReleasedTap();
        m_worldController = null;
        m_viewportRect = null;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData != null)
        {
            eventData.useDragThreshold = false;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanProcess(eventData) ||
            !TryGetViewportPosition(eventData, out Vector2 viewportPosition))
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Middle)
        {
            BeginMiddlePan(eventData.pointerId, viewportPosition);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left ||
            m_middlePanActive)
        {
            return;
        }

        m_pointerPositions[eventData.pointerId] = viewportPosition;
        if (m_pointerPositions.Count == 1 && !m_suppressSinglePointer)
        {
            ResetSingleGestureFlags();
            ResetNavigationPointers();
            m_singlePressScreenPosition = eventData.position;
            m_currentTapEligible = !m_worldController.PreviewActive;
            ResetReleasedTap();
            if (m_worldController.PreviewActive ||
                m_worldController.PersistentSelectionActive)
            {
                if (m_worldController.PreviewActive)
                {
                    InvalidateCurrentTap();
                }

                m_singlePointerRouted =
                    m_worldController.HandlePointerDown(viewportPosition);
            }
            else
            {
                StartLongPress(
                    eventData.pointerId,
                    eventData.position,
                    viewportPosition);
            }

            return;
        }

        if (m_pointerPositions.Count >= 2 && !m_suppressSinglePointer)
        {
            // 双指导航不是“松开零件”，因此只能中断，不能触发落地提交。
            StopLongPressTimer();
            InvalidateCurrentTap();
            ResetTapSequence();
            ResetReleasedTap();
            if (m_worldController.PreviewActive && m_holdCompleted)
            {
                m_worldController.CancelPreview();
            }
            else if (m_singlePointerRouted)
            {
                m_worldController.HandlePointerCancel();
            }

            ResetSingleGestureFlags();
            m_suppressSinglePointer = true;
            CaptureNavigationPointers();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!CanProcess(eventData))
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Middle)
        {
            if (!m_middlePanActive ||
                eventData.pointerId != m_middlePointerId ||
                !TryGetViewportPosition(eventData, out Vector2 middleCurrent))
            {
                return;
            }

            Vector2 panDelta = middleCurrent - m_middleLastViewportPosition;
            m_middleLastViewportPosition = middleCurrent;
            m_worldController.PanViewport(panDelta);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left ||
            !m_pointerPositions.TryGetValue(eventData.pointerId, out Vector2 previous) ||
            !TryGetViewportPosition(eventData, out Vector2 current))
        {
            return;
        }

        if (m_pointerPositions.Count >= 2)
        {
            if (!IsNavigationPointer(eventData.pointerId))
            {
                m_pointerPositions[eventData.pointerId] = current;
                return;
            }

            Vector2 oldCenter = GetNavigationCenter();
            float oldDistance = GetNavigationDistance();
            m_pointerPositions[eventData.pointerId] = current;
            Vector2 newCenter = GetNavigationCenter();
            float newDistance = GetNavigationDistance();
            Vector2 panDelta = newCenter - oldCenter;
            float pinchDelta = newDistance - oldDistance;
            if (panDelta.sqrMagnitude > 0.00000001f)
            {
                m_worldController.PanViewport(panDelta);
            }

            if (Mathf.Abs(pinchDelta) > 0.0001f)
            {
                m_worldController.Zoom(pinchDelta);
            }

            return;
        }

        m_pointerPositions[eventData.pointerId] = current;
        if (m_suppressSinglePointer)
        {
            return;
        }

        bool movedBeyondTapThreshold =
            Vector2.Distance(eventData.position, m_singlePressScreenPosition) >
            GetTapMoveThreshold();
        if (movedBeyondTapThreshold)
        {
            InvalidateCurrentTap();
            ResetTapSequence();
        }
        if (m_worldController.PersistentSelectionActive &&
            m_singlePointerRouted &&
            !m_persistentRotationDragActivated)
        {
            if (!movedBeyondTapThreshold)
            {
                // 持续选择的轻微手抖仍属于点击；超过点击阈值后才正式开始旋转。
                return;
            }

            // 一旦旋转已经开始，拖回起点也必须持续转发，确保角度可准确复原。
            m_persistentRotationDragActivated = true;
        }

        if (m_longPressCoroutine != null &&
            eventData.pointerId == m_longPressPointerId)
        {
            float movement = Vector2.Distance(
                eventData.position,
                m_longPressStartScreenPosition);
            if (movement <= GetLongPressMoveThreshold())
            {
                return;
            }

            Vector2 orbitDelta = current - m_longPressStartViewportPosition;
            StopLongPressTimer();
            m_holdCompleted = false;
            m_singlePointerRouted = true;
            m_worldController.HandlePointerDrag(current, orbitDelta);
            return;
        }

        if (m_singlePointerRouted)
        {
            m_worldController.HandlePointerDrag(current, current - previous);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null || m_worldController == null)
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Middle)
        {
            if (m_middlePanActive && eventData.pointerId == m_middlePointerId)
            {
                ResetMiddlePan();
            }

            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left ||
            !m_pointerPositions.ContainsKey(eventData.pointerId))
        {
            return;
        }

        bool wasSinglePointer = m_pointerPositions.Count == 1;
        Vector2 viewportPosition = m_pointerPositions.TryGetValue(
            eventData.pointerId,
            out Vector2 storedPosition)
            ? storedPosition
            : Vector2.zero;
        if (TryGetViewportPosition(eventData, out Vector2 releasePosition))
        {
            viewportPosition = releasePosition;
        }

        bool shouldCompleteInteraction =
            wasSinglePointer &&
            !m_suppressSinglePointer &&
            m_singlePointerRouted;
        bool releaseMovedBeyondTapThreshold =
            Vector2.Distance(eventData.position, m_singlePressScreenPosition) >
            GetTapMoveThreshold();
        if (releaseMovedBeyondTapThreshold)
        {
            InvalidateCurrentTap();
            ResetTapSequence();
        }

        bool releasedTapEligible =
            wasSinglePointer &&
            !m_suppressSinglePointer &&
            m_currentTapEligible &&
            !m_holdCompleted &&
            !releaseMovedBeyondTapThreshold;

        StopLongPressTimer();
        m_pointerPositions.Remove(eventData.pointerId);
        if (m_suppressSinglePointer &&
            m_pointerPositions.Count >= 2 &&
            IsNavigationPointer(eventData.pointerId))
        {
            // 三指场景中若原导航指针抬起，用剩余两指继续导航。
            CaptureNavigationPointers();
        }

        if (shouldCompleteInteraction)
        {
            if (m_worldController.PersistentSelectionActive &&
                (m_persistentRotationDragActivated || releaseMovedBeyondTapThreshold))
            {
                // TouchPhase.Ended 可能带来最后一次位移，先将最终位置送入累计旋转。
                m_worldController.HandlePointerDrag(viewportPosition, Vector2.zero);
            }

            m_worldController.HandlePointerUp(viewportPosition);
        }

        m_releasedPointerId = eventData.pointerId;
        m_releasedTapScreenPosition = eventData.position;
        m_releasedTapEligible = releasedTapEligible;

        if (m_pointerPositions.Count == 0)
        {
            m_suppressSinglePointer = false;
            ResetNavigationPointers();
            ResetSingleGestureFlags();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanProcess(eventData) ||
            eventData.button != PointerEventData.InputButton.Left ||
            !m_releasedTapEligible ||
            eventData.pointerId != m_releasedPointerId)
        {
            return;
        }

        Vector2 tapScreenPosition = m_releasedTapScreenPosition;
        ResetReleasedTap();
        if (!TryGetViewportPosition(eventData, out Vector2 viewportPosition))
        {
            ResetTapSequence();
            return;
        }

        string tapTargetId;
        if (m_worldController.TryPickEditablePart(
                viewportPosition,
                out string partId))
        {
            tapTargetId = partId;
        }
        else if ((m_worldController.PersistentSelectionActive ||
                  m_worldController.AttachmentSourceActive) &&
                 m_worldController.IsViewportEmpty(viewportPosition))
        {
            // 地面和背景才属于空白；正式零件不会误触取消。
            tapTargetId = EMPTY_TAP_TARGET_ID;
        }
        else
        {
            ResetTapSequence();
            return;
        }

        float now = Time.unscaledTime;
        bool continuesSequence =
            string.Equals(m_firstTapTargetId, tapTargetId) &&
            now - m_firstTapTime <= DOUBLE_TAP_MAX_DURATION &&
            Vector2.Distance(m_firstTapScreenPosition, tapScreenPosition) <=
            GetDoubleTapMoveThreshold();
        if (!continuesSequence)
        {
            m_firstTapTargetId = tapTargetId;
            m_firstTapScreenPosition = tapScreenPosition;
            m_firstTapTime = now;
            m_tapCount = 1;
            return;
        }

        m_tapCount++;
        m_firstTapTime = now;
        if (m_tapCount == 2)
        {
            if (tapTargetId == EMPTY_TAP_TARGET_ID)
            {
                ResetTapSequence();
                m_worldController.ClearInteractionSelection();
            }
            else
            {
                // 保留序列，第三击会把这次普通双击升级为吸附选择。
                m_worldController.SelectPersistentPart(tapTargetId);
            }

            return;
        }

        if (m_tapCount >= 3)
        {
            ResetTapSequence();
            if (tapTargetId != EMPTY_TAP_TARGET_ID)
            {
                m_worldController.HandlePartTripleTap(tapTargetId);
            }
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!CanProcess(eventData) ||
            m_pointerPositions.Count > 0 ||
            m_middlePanActive)
        {
            return;
        }

        m_worldController.Zoom(eventData.scrollDelta.y * 0.08f);
    }

    private void OnDisable()
    {
        CancelCurrentInteraction();
        m_pointerPositions.Clear();
        m_suppressSinglePointer = false;
        ResetNavigationPointers();
        ResetMiddlePan();
        ResetTapSequence();
        ResetReleasedTap();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            CancelCurrentInteraction();
            m_pointerPositions.Clear();
            m_suppressSinglePointer = false;
            ResetNavigationPointers();
            ResetMiddlePan();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            CancelCurrentInteraction();
            m_pointerPositions.Clear();
            m_suppressSinglePointer = false;
            ResetNavigationPointers();
            ResetMiddlePan();
        }
    }

    private void StartLongPress(
        int pointerId,
        Vector2 screenPosition,
        Vector2 viewportPosition)
    {
        StopLongPressTimer();
        m_longPressPointerId = pointerId;
        m_longPressStartScreenPosition = screenPosition;
        m_longPressStartViewportPosition = viewportPosition;
        m_longPressCoroutine = StartCoroutine(WaitForLongPress());
    }

    private IEnumerator WaitForLongPress()
    {
        yield return new WaitForSecondsRealtime(LONG_PRESS_DURATION);

        int pointerId = m_longPressPointerId;
        m_longPressCoroutine = null;
        m_longPressPointerId = INVALID_POINTER_ID;
        if (!isActiveAndEnabled ||
            m_worldController == null ||
            m_suppressSinglePointer ||
            m_pointerPositions.Count != 1 ||
            !m_pointerPositions.ContainsKey(pointerId))
        {
            yield break;
        }

        m_holdCompleted = true;
        InvalidateCurrentTap();
        ResetTapSequence();
        if (m_worldController.PreviewActive)
        {
            yield break;
        }

        m_singlePointerRouted =
            m_worldController.HandlePointerDown(m_longPressStartViewportPosition);
        if (!m_singlePointerRouted)
        {
            // 空白处长按不应锁死本次手势；之后继续拖动时改为环绕镜头。
            m_singlePointerRouted = true;
        }
    }

    private void CancelCurrentInteraction()
    {
        StopLongPressTimer();
        if (m_worldController != null)
        {
            if (m_worldController.PreviewActive && m_holdCompleted)
            {
                m_worldController.CancelPreview();
            }
            else if (m_singlePointerRouted)
            {
                m_worldController.HandlePointerCancel();
            }
        }

        ResetTapSequence();
        ResetReleasedTap();
        ResetSingleGestureFlags();
    }

    private void StopLongPressTimer()
    {
        if (m_longPressCoroutine != null)
        {
            StopCoroutine(m_longPressCoroutine);
            m_longPressCoroutine = null;
        }

        m_longPressPointerId = INVALID_POINTER_ID;
    }

    private void ResetSingleGestureFlags()
    {
        StopLongPressTimer();
        m_singlePointerRouted = false;
        m_holdCompleted = false;
        m_persistentRotationDragActivated = false;
        m_currentTapEligible = false;
    }

    private static float GetLongPressMoveThreshold()
    {
        if (Screen.dpi <= 0f)
        {
            return DEFAULT_LONG_PRESS_MOVE_THRESHOLD;
        }

        return Mathf.Clamp(Screen.dpi * 0.08f, 12f, 32f);
    }

    private static float GetTapMoveThreshold()
    {
        return GetLongPressMoveThreshold();
    }

    private static float GetDoubleTapMoveThreshold()
    {
        return GetLongPressMoveThreshold() * 1.5f;
    }

    private void InvalidateCurrentTap()
    {
        m_currentTapEligible = false;
        m_releasedTapEligible = false;
    }

    private void ResetTapSequence()
    {
        m_firstTapTargetId = null;
        m_firstTapScreenPosition = Vector2.zero;
        m_firstTapTime = float.MinValue;
        m_tapCount = 0;
    }

    private void ResetReleasedTap()
    {
        m_releasedPointerId = INVALID_POINTER_ID;
        m_releasedTapScreenPosition = Vector2.zero;
        m_releasedTapEligible = false;
    }

    private bool CanProcess(PointerEventData eventData)
    {
        return eventData != null &&
               m_worldController != null &&
               m_viewportRect != null &&
               isActiveAndEnabled;
    }

    private bool TryGetViewportPosition(
        PointerEventData eventData,
        out Vector2 viewportPosition)
    {
        viewportPosition = Vector2.zero;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_viewportRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = m_viewportRect.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return false;
        }

        viewportPosition = new Vector2(
            Mathf.Clamp01((localPoint.x - rect.xMin) / rect.width),
            Mathf.Clamp01((localPoint.y - rect.yMin) / rect.height));
        return true;
    }

    private void BeginMiddlePan(int pointerId, Vector2 viewportPosition)
    {
        if (m_middlePanActive || m_pointerPositions.Count >= 2)
        {
            return;
        }

        StopLongPressTimer();
        InvalidateCurrentTap();
        ResetTapSequence();
        ResetReleasedTap();
        if (m_singlePointerRouted)
        {
            m_worldController.HandlePointerCancel();
        }

        ResetSingleGestureFlags();
        if (m_pointerPositions.Count > 0)
        {
            m_suppressSinglePointer = true;
        }

        m_middlePanActive = true;
        m_middlePointerId = pointerId;
        m_middleLastViewportPosition = viewportPosition;
    }

    private void ResetMiddlePan()
    {
        m_middlePanActive = false;
        m_middlePointerId = INVALID_POINTER_ID;
        m_middleLastViewportPosition = Vector2.zero;
    }

    private void CaptureNavigationPointers()
    {
        ResetNavigationPointers();
        foreach (int pointerId in m_pointerPositions.Keys)
        {
            if (m_navigationPointerA == INVALID_POINTER_ID)
            {
                m_navigationPointerA = pointerId;
            }
            else
            {
                m_navigationPointerB = pointerId;
                break;
            }
        }
    }

    private void ResetNavigationPointers()
    {
        m_navigationPointerA = INVALID_POINTER_ID;
        m_navigationPointerB = INVALID_POINTER_ID;
    }

    private bool IsNavigationPointer(int pointerId)
    {
        return pointerId == m_navigationPointerA ||
               pointerId == m_navigationPointerB;
    }

    private Vector2 GetNavigationCenter()
    {
        if (!TryGetNavigationPair(out Vector2 first, out Vector2 second))
        {
            return Vector2.zero;
        }

        return (first + second) * 0.5f;
    }

    private float GetNavigationDistance()
    {
        return TryGetNavigationPair(out Vector2 first, out Vector2 second)
            ? Vector2.Distance(first, second)
            : 0f;
    }

    private bool TryGetNavigationPair(
        out Vector2 first,
        out Vector2 second)
    {
        first = Vector2.zero;
        second = Vector2.zero;
        return m_pointerPositions.TryGetValue(m_navigationPointerA, out first) &&
               m_pointerPositions.TryGetValue(m_navigationPointerB, out second);
    }
}
