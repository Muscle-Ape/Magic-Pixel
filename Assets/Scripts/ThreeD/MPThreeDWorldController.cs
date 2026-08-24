using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 3D 拼装模式的独立运行时世界。
/// 负责相机、运行时基础几何、放置事务、吸附、校验、历史和 DTO 重建。
/// 正式零件自身不运行 Update，所有操作均由此控制器集中处理。
/// </summary>
public sealed class MPThreeDWorldController : MonoBehaviour
{
    private const string ROOT_INSTANCE_ID = "root";
    private const float BUILD_HALF_SIZE = 6f;
    private const float BUILD_MAX_HEIGHT = 8f;
    private const float SNAP_DISTANCE = 0.68f;
    private const float SOCKET_CONNECTION_TOLERANCE = 0.025f;
    private const float PENETRATION_TOLERANCE = 0.005f;
    private const float INITIAL_HEIGHT_STEP = 1f;
    private const float HEIGHT_INTEGER_EPSILON = 0.0001f;
    private const float LINE_SNAP_SKIN = 0.003f;
    private const float CONTACT_QUERY_TOLERANCE = 0.0001f;
    private const float CONTACT_PROBE_DISTANCE = 0.006f;
    private const float MAX_CONTACT_SCAN_STEP = 0.05f;
    private const int MAX_CONTACT_SCAN_STEPS = 1024;
    private const int CONTACT_BINARY_SEARCH_ITERATIONS = 16;
    private const float CAMERA_MIN_DISTANCE = 4f;
    private const float CAMERA_MAX_DISTANCE = 22f;
    private const float CAMERA_MIN_PITCH = 2f;
    private const float CAMERA_MAX_PITCH = 88f;
    private const float PART_DRAG_VIEWPORT_THRESHOLD = 0.005f;
    private const float PART_ROTATION_VIEWPORT_THRESHOLD = 0.006f;
    private const float PART_ROTATION_SENSITIVITY = 180f;
    private const float ROTATION_SNAP_ANGLE = 10f;
    private const float ROTATION_SNAP_DRAG_THRESHOLD_PIXELS = 56f;
    private const float ROTATION_SNAP_GRID_EPSILON = 0.0001f;
    private const int MAX_LOADED_NON_ROOT_PARTS = 100;

    private enum MoveAxisPlane
    {
        XY,
        XZ,
        YZ
    }

    private sealed class PartPoseSnapshot
    {
        public MPThreeDPart Part;
        public Vector3 Position;
        public Quaternion Rotation;
        public string ConnectionId;
    }

    // 当前项目只有一个 Main 场景且不修改全局 Layer 配置。
    // 将运行时世界放到主场景相机远裁剪面之外，可避免两套相机互相看见内容。
    private static readonly Vector3 s_worldOffset = new Vector3(1000f, 1000f, 1000f);

    private static readonly Vector3[] s_partPrincipalAxes =
    {
        Vector3.right,
        Vector3.up,
        Vector3.forward
    };

    private readonly List<MPThreeDPart> m_parts = new List<MPThreeDPart>();
    private readonly HashSet<string> m_loadIds = new HashSet<string>();
    private readonly List<PartPoseSnapshot> m_interactionStartPoses =
        new List<PartPoseSnapshot>();
    private readonly HashSet<string> m_interactionPartIds =
        new HashSet<string>();

    private GameObject m_worldRoot;
    private Transform m_partsRoot;
    private Transform m_previewRoot;
    private GameObject m_gridObject;
    private Camera m_camera;
    private Light m_light;
    private Mesh m_gridMesh;
    private Material m_placedMaterial;
    private Material m_previewMaterial;
    private Material m_selectionOutlineMaterial;
    private Material m_groundMaterial;
    private Material m_gridMaterial;
    private Mesh m_cubeSelectionOutlineMesh;
    private Mesh m_cylinderSelectionOutlineMesh;
    private Mesh m_sphereSelectionOutlineMesh;
    private Mesh m_cylinderCollisionMesh;
    private Mesh m_sphereCollisionMesh;
    private MPThreeDPart m_previewPart;
    private MPThreeDPart m_editingOriginal;
    private MPThreeDPart m_persistentSelectedPart;
    private MPThreeDPart m_movingPart;
    private MPThreeDPart m_attachmentSourcePart;
    private MPThreeDPart m_highlightedConflict;
    private MPThreeDValidationResult m_validation;
    private MPThreeDCommandHistory m_history;
    private Bounds m_buildBounds;
    private Vector3 m_cameraTarget;
    private Vector3 m_previewDragOffset;
    private Vector3 m_moveDragOffset;
    private Vector3 m_moveStartPosition;
    private Plane m_moveDragPlane;
    private MoveAxisPlane m_moveAxisPlane;
    private Vector2 m_persistentRotationStartViewport;
    private Quaternion m_persistentRotationStartLocalRotation;
    private Vector3 m_verticalDragRotationLocalAxis;
    private Vector3 m_horizontalDragRotationLocalAxis;
    private float m_cameraYaw = 35f;
    private float m_cameraPitch = 30f;
    private float m_cameraDistance = 12f;
    private float m_previewPlaneHeight;
    private float m_pointerPartDragDistance;
    private string m_moveSnapTargetId;
    private string m_previewSnapTargetId;
    private bool m_pointerManipulatesPart;
    private bool m_pointerMovedPart;
    private bool m_pointerRotatesPersistentPart;
    private bool m_persistentRotationMoved;
    private bool m_persistentRotationUsesHorizontalDrag;
    private bool m_persistentRotationAxisLocked;
    private bool m_initialized;
    private bool m_isShuttingDown;
    private string m_title = "My 3D Build";

    public event Action<MPThreeDValidationResult> ValidationChanged;
    public event Action StateCommitted;
    public event Action<string> MessageChanged;

    public RenderTexture RenderTexture { get; private set; }
    public bool PreviewActive => m_previewPart != null;
    public bool PersistentSelectionActive => m_persistentSelectedPart != null;
    public bool AttachmentSourceActive => m_attachmentSourcePart != null;
    public bool OperationTargetActive => PreviewActive || PersistentSelectionActive;
    public bool CanEditPlacedPart =>
        m_persistentSelectedPart != null ||
        (m_previewPart != null && m_editingOriginal != null);
    public bool PointerGestureActive =>
        m_pointerManipulatesPart || m_pointerRotatesPersistentPart;
    public bool CanConfirm => PreviewActive && m_validation != null && m_validation.CanConfirm;
    public bool CanUndo => m_history != null && m_history.CanUndo;
    public bool CanRedo => m_history != null && m_history.CanRedo;
    public bool RequireConnection { get; private set; } = true;
    public bool GridVisible { get; private set; } = true;
    public bool TransformSnapEnabled { get; private set; }

    /// <summary>
    /// 创建独立世界和指定像素尺寸的 RenderTexture。
    /// 重复调用会先完整释放上一次运行时资源。
    /// </summary>
    public void Initialize(int textureWidth, int textureHeight)
    {
        if (m_initialized || m_worldRoot != null || RenderTexture != null)
        {
            Shutdown();
        }

        m_isShuttingDown = false;
        m_initialized = true;
        TransformSnapEnabled = true;
        float groundPlaneHeight = GetGroundPlaneHeight();
        m_buildBounds = new Bounds(
            new Vector3(
                0f,
                (groundPlaneHeight + BUILD_MAX_HEIGHT) * 0.5f,
                0f),
            new Vector3(
                BUILD_HALF_SIZE * 2f,
                BUILD_MAX_HEIGHT - groundPlaneHeight,
                BUILD_HALF_SIZE * 2f));

        CreateMaterials();
        CreateWorld(Mathf.Max(64, textureWidth), Mathf.Max(64, textureHeight));
        CreateDefaultRoot();
        m_history = new MPThreeDCommandHistory(50);
        m_history.Reset(CaptureState());
        SetIdleValidation("Long-press to move; double-tap a part to rotate");
        UpdateCameraTransform();
    }

    /// <summary>
    /// 使用 DTO 重建已提交的作品，并重置撤销历史。
    /// </summary>
    public void LoadState(MPThreeDAssemblySaveDto data)
    {
        if (!EnsureInitialized())
        {
            return;
        }

        ApplyState(data ?? MPThreeDAssemblySaveDto.CreateEmpty(), true);
        SetIdleValidation(
            $"Loaded: {Mathf.Max(0, m_parts.Count - 1)} parts - double-tap a part to rotate");
        StateCommitted?.Invoke();
    }

    /// <summary>
    /// 捕获正式结构。未确认的幽灵预览不会进入 DTO。
    /// </summary>
    public MPThreeDAssemblySaveDto CaptureState()
    {
        MPThreeDAssemblySaveDto data = new MPThreeDAssemblySaveDto
        {
            schemaVersion = MPThreeDAssemblySaveDto.CurrentSchemaVersion,
            title = string.IsNullOrEmpty(m_title) ? "My 3D Build" : m_title,
            requireConnection = RequireConnection,
            gridVisible = GridVisible,
            placedParts = new List<MPThreeDPlacedPartDto>(m_parts.Count)
        };

        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart part = m_parts[i];
            if (part != null && !part.IsPreview)
            {
                data.placedParts.Add(part.CaptureDto());
            }
        }

        return data;
    }

    public void BeginCreate(string partId)
    {
        if (!EnsureInitialized())
        {
            return;
        }

        if (PointerGestureActive)
        {
            PublishMessage("Finish the current gesture first");
            return;
        }

        if (!MPThreeDPartCatalog.TryGet(partId, out MPThreeDPartDefinition definition) ||
            definition.Id == MPThreeDPartCatalog.RootPartId)
        {
            PublishMessage("Invalid part");
            return;
        }

        ClearPersistentSelectionInternal(false);
        ClearAttachmentSourceInternal(false);
        CancelPreviewInternal(false);
        string instanceId = Guid.NewGuid().ToString("N");
        MPThreeDPart part = MPThreeDPart.Create(
            m_partsRoot,
            definition,
            instanceId,
            false,
            false,
            m_placedMaterial,
            m_previewMaterial,
            m_selectionOutlineMaterial,
            GetSelectionOutlineMesh(definition.Shape),
            GetCollisionMesh(definition.Shape));

        Quaternion rotation = Quaternion.Euler(definition.DefaultEuler);
        part.transform.localRotation = rotation;
        MPThreeDPart root = GetRootPart();
        float rootTop = root == null
            ? 0f
            : root.transform.localPosition.y + root.Definition.Size.y * 0.5f;
        float verticalExtent = GetShapeAabbExtents(
            definition.Shape,
            definition.Size,
            rotation).y;
        float planeHeight = Mathf.Ceil(
            Mathf.Max(verticalExtent, rootTop + verticalExtent) -
            HEIGHT_INTEGER_EPSILON);

        Vector3 preferredPosition = GetPointOnWorkPlane(
            new Vector2(0.5f, 0.5f),
            planeHeight);
        preferredPosition.y = planeHeight;
        part.SetLocalPose(preferredPosition, rotation);
        bool foundSafePosition = TryFindSafeInitialPosition(
            part,
            preferredPosition,
            out Vector3 initialPosition);
        part.SetLocalPose(initialPosition, rotation);
        bool connected = TryResolveConnection(part, null, out string targetId);
        part.SetConnection(connected ? targetId : null);
        m_parts.Add(part);
        CommitCurrentState(
            foundSafePosition
                ? $"Added {definition.DisplayName}"
                : $"Added {definition.DisplayName} with overlap warning",
            true,
            part);
        if (!foundSafePosition)
        {
            SetValidation(new MPThreeDValidationResult(
                MPThreeDPlacementState.Collision,
                false,
                false,
                $"No clear space for {definition.DisplayName}; placed with warning"),
                false);
        }
    }

    /// <summary>
    /// 仅做 3D 命中测试，不改变零件状态。输入层用它校验两次点击是否命中同一零件。
    /// </summary>
    public bool TryPickEditablePart(
        Vector2 viewportPosition,
        out string instanceId)
    {
        instanceId = null;
        if (!EnsureInitialized() || m_previewPart != null)
        {
            return false;
        }

        MPThreeDPart part = RaycastEditablePart(ClampViewport(viewportPosition));
        if (part == null)
        {
            return false;
        }

        instanceId = part.InstanceId;
        return true;
    }

    /// <summary>
    /// 判断点击位置是否没有命中任何正式零件。地面和背景视为空白，
    /// 根节点及带子零件的父节点虽然不可编辑，但不属于空白区域。
    /// </summary>
    public bool IsViewportEmpty(Vector2 viewportPosition)
    {
        if (!EnsureInitialized() || m_previewPart != null)
        {
            return false;
        }

        return RaycastPlacedPart(ClampViewport(viewportPosition)) == null;
    }

    /// <summary>
    /// 双击进入持续编辑。零件保持正式外观，所有修改在一次操作结束时提交，
    /// 选中描边会一直保留到取消、换选或结构被重建。
    /// </summary>
    public bool SelectPersistentPart(string instanceId)
    {
        if (!EnsureInitialized() || m_previewPart != null)
        {
            return false;
        }

        MPThreeDPart part = FindPart(instanceId);
        if (part == null || part.IsRoot)
        {
            PublishMessage("Select a placed part");
            return false;
        }

        if (m_persistentSelectedPart != part)
        {
            ClearPersistentSelectionInternal(false);
            m_persistentSelectedPart = part;
        }

        part.SetSelectionOutline(true);
        string warningSummary = GetPartWarningSummary(part);
        SetIdleValidation(
            string.IsNullOrEmpty(warningSummary)
                ? $"Selected {part.Definition.DisplayName} - drag to rotate; double-tap empty to exit"
                : $"Selected {part.Definition.DisplayName} - Warning: {warningSummary}");
        return true;
    }

    public void ClearPersistentSelection()
    {
        ClearPersistentSelectionInternal(true);
    }

    public void ClearInteractionSelection()
    {
        bool hadSelection =
            m_persistentSelectedPart != null ||
            m_attachmentSourcePart != null;
        ClearPersistentSelectionInternal(false);
        ClearAttachmentSourceInternal(false);
        if (hadSelection)
        {
            SetIdleValidation("Selection cleared - drag to orbit");
        }
    }

    /// <summary>
    /// 第一次三击把零件设为蓝色待吸附源；第二次三击目标零件后，
    /// 只移动源零件本身并保持朝向不变，逻辑子零件不会跟随。
    /// </summary>
    public bool HandlePartTripleTap(string instanceId)
    {
        if (!EnsureInitialized() || PointerGestureActive || m_previewPart != null)
        {
            return false;
        }

        MPThreeDPart tappedPart = FindPart(instanceId);
        if (tappedPart == null || tappedPart.IsRoot)
        {
            PublishMessage("Triple-tap a placed part");
            return false;
        }

        ClearPersistentSelectionInternal(false);
        if (m_attachmentSourcePart == null)
        {
            m_attachmentSourcePart = tappedPart;
            tappedPart.SetAttachmentOutline(true);
            SetIdleValidation(
                $"Attach source: {tappedPart.Definition.DisplayName} - triple-tap a target");
            return true;
        }

        if (m_attachmentSourcePart == tappedPart)
        {
            ClearAttachmentSourceInternal(false);
            SetIdleValidation("Attach selection cancelled");
            return true;
        }

        return TryAttachSourceToTarget(tappedPart);
    }

    /// <summary>
    /// viewportPosition 使用 RawImage 内的 0-1 归一化坐标。
    /// </summary>
    public bool HandlePointerDown(Vector2 viewportPosition)
    {
        if (!EnsureInitialized())
        {
            return false;
        }

        viewportPosition = ClampViewport(viewportPosition);
        m_pointerManipulatesPart = false;
        m_pointerRotatesPersistentPart = false;

        if (m_persistentSelectedPart != null && m_previewPart == null)
        {
            BeginPersistentRotation(viewportPosition);
            return true;
        }

        if (m_previewPart != null)
        {
            Vector3 planePoint = GetPointOnWorkPlane(viewportPosition, m_previewPlaneHeight);
            m_previewDragOffset = m_previewPart.transform.localPosition - planePoint;
            m_pointerManipulatesPart = true;
            m_pointerPartDragDistance = 0f;
            m_pointerMovedPart = false;
            return true;
        }

        MPThreeDPart selectedPart = RaycastEditablePart(viewportPosition);
        if (selectedPart == null)
        {
            return false;
        }

        return BeginPlacedMove(selectedPart, viewportPosition);
    }

    /// <summary>
    /// viewportPosition 和 viewportDelta 都是 RawImage 内的归一化坐标。
    /// </summary>
    public void HandlePointerDrag(Vector2 viewportPosition, Vector2 viewportDelta)
    {
        if (!EnsureInitialized())
        {
            return;
        }

        if (m_pointerRotatesPersistentPart && m_persistentSelectedPart != null)
        {
            UpdatePersistentRotation(ClampViewport(viewportPosition));
            return;
        }

        if (m_pointerManipulatesPart && m_previewPart != null)
        {
            m_pointerPartDragDistance += viewportDelta.magnitude;
            Vector3 previousPosition = m_previewPart.transform.localPosition;
            Vector3 rawPosition = GetPointOnWorkPlane(
                ClampViewport(viewportPosition),
                m_previewPlaneHeight) + m_previewDragOffset;
            rawPosition.y = m_previewPlaneHeight;
            if (TransformSnapEnabled)
            {
                rawPosition = MPThreeDMath.QuantizeXZ(
                    rawPosition,
                    m_previewPart.Definition.GridStep);
            }
            ApplyCandidatePose(rawPosition, m_previewPart.transform.localRotation);
            if (m_pointerPartDragDistance >= PART_DRAG_VIEWPORT_THRESHOLD ||
                (m_previewPart.transform.localPosition - previousPosition).sqrMagnitude > 0.000001f)
            {
                m_pointerMovedPart = true;
            }

            return;
        }

        if (m_pointerManipulatesPart && m_movingPart != null)
        {
            UpdatePlacedMove(ClampViewport(viewportPosition));
            return;
        }

        m_cameraYaw += viewportDelta.x * 220f;
        m_cameraPitch = Mathf.Clamp(
            m_cameraPitch - viewportDelta.y * 180f,
            CAMERA_MIN_PITCH,
            CAMERA_MAX_PITCH);
        UpdateCameraTransform();
    }

    public void HandlePointerUp(Vector2 viewportPosition)
    {
        if (m_pointerRotatesPersistentPart)
        {
            EndPersistentRotation(true);
            return;
        }

        if (m_pointerManipulatesPart && m_movingPart != null)
        {
            UpdatePlacedMove(ClampViewport(viewportPosition));
            EndPlacedMove(true);
            return;
        }

        if (m_pointerManipulatesPart && m_previewPart != null && m_pointerMovedPart)
        {
            Vector3 finalPosition = GetPointOnWorkPlane(
                ClampViewport(viewportPosition),
                m_previewPlaneHeight) + m_previewDragOffset;
            finalPosition.y = m_previewPlaneHeight;
            if (TransformSnapEnabled)
            {
                finalPosition = MPThreeDMath.QuantizeXZ(
                    finalPosition,
                    m_previewPart.Definition.GridStep);
            }
            ApplyCandidatePose(finalPosition, m_previewPart.transform.localRotation);
        }

        bool shouldCommit =
            m_pointerManipulatesPart &&
            m_previewPart != null &&
            (m_editingOriginal != null || m_pointerMovedPart);
        m_pointerManipulatesPart = false;
        m_pointerPartDragDistance = 0f;
        m_pointerMovedPart = false;
        m_previewDragOffset = Vector3.zero;
        if (shouldCommit)
        {
            CommitPreview(true);
        }
    }

    /// <summary>
    /// 双指切换、页面失焦或销毁只中断当前指针，不应被当作用户松手落地。
    /// </summary>
    public void HandlePointerCancel()
    {
        if (m_pointerRotatesPersistentPart)
        {
            EndPersistentRotation(false);
        }

        else if (m_pointerManipulatesPart && m_movingPart != null)
        {
            EndPlacedMove(false);
        }

        m_pointerManipulatesPart = false;
        m_pointerPartDragDistance = 0f;
        m_pointerMovedPart = false;
        m_previewDragOffset = Vector3.zero;
    }

    /// <summary>
    /// delta 为正时拉近，负时拉远。
    /// </summary>
    public void Zoom(float delta)
    {
        if (!EnsureInitialized())
        {
            return;
        }

        m_cameraDistance = Mathf.Clamp(
            m_cameraDistance - delta * 8f,
            CAMERA_MIN_DISTANCE,
            CAMERA_MAX_DISTANCE);
        UpdateCameraTransform();
    }

    /// <summary>
    /// 平移镜头观察中心。delta 是视口归一化位移，内容跟随手势移动。
    /// </summary>
    public void PanViewport(Vector2 delta)
    {
        if (!EnsureInitialized() || m_camera == null)
        {
            return;
        }

        float worldHeight =
            2f * m_cameraDistance *
            Mathf.Tan(m_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = RenderTexture != null && RenderTexture.height > 0
            ? (float)RenderTexture.width / RenderTexture.height
            : Mathf.Max(0.1f, m_camera.aspect);
        Vector3 cameraRight = m_camera.transform.localRotation * Vector3.right;
        Vector3 cameraUp = m_camera.transform.localRotation * Vector3.up;
        Vector3 movement =
            -cameraRight * (delta.x * worldHeight * aspect) -
            cameraUp * (delta.y * worldHeight);
        m_cameraTarget += movement;
        m_cameraTarget.x = Mathf.Clamp(
            m_cameraTarget.x,
            -BUILD_HALF_SIZE,
            BUILD_HALF_SIZE);
        m_cameraTarget.y = Mathf.Clamp(
            m_cameraTarget.y,
            0f,
            BUILD_MAX_HEIGHT);
        m_cameraTarget.z = Mathf.Clamp(
            m_cameraTarget.z,
            -BUILD_HALF_SIZE,
            BUILD_HALF_SIZE);
        UpdateCameraTransform();
    }

    public void AdjustPreviewHeight(int steps)
    {
        if (PointerGestureActive)
        {
            return;
        }

        MPThreeDPart operationPart = m_previewPart ?? m_persistentSelectedPart;
        if (operationPart == null || steps == 0)
        {
            return;
        }

        float height = MPThreeDMath.Quantize(
            operationPart.transform.localPosition.y +
            steps * operationPart.Definition.GridStep,
            operationPart.Definition.GridStep);
        Vector3 position = operationPart.transform.localPosition;
        position.y = height;
        if (m_previewPart != null)
        {
            m_previewPlaneHeight = height;
            ApplyCandidatePose(position, m_previewPart.transform.localRotation);
            return;
        }

        CaptureInteractionPart(operationPart);
        float heightDelta = height - operationPart.transform.localPosition.y;
        for (int i = 0; i < m_interactionStartPoses.Count; i++)
        {
            PartPoseSnapshot snapshot = m_interactionStartPoses[i];
            if (snapshot.Part != null)
            {
                snapshot.Part.SetLocalPose(
                    snapshot.Position + Vector3.up * heightDelta,
                    snapshot.Rotation);
            }
        }

        RefreshConnectionsAfterPartChanged(operationPart, true);
        ClearInteractionSnapshots();
        CommitCurrentState(
            $"Moved {operationPart.Definition.DisplayName}",
            true,
            operationPart);
    }

    public void ConfirmPreview()
    {
        if (PointerGestureActive)
        {
            return;
        }

        CommitPreview(false);
    }

    private void CommitPreview(bool allowInvalid)
    {
        if (m_previewPart == null)
        {
            PublishMessage("Nothing to confirm");
            return;
        }

        MPThreeDPart snappedTarget = FindPart(m_previewSnapTargetId);
        bool keepSnapTarget = snappedTarget != null &&
            !WouldCreateConnectionCycle(m_previewPart, snappedTarget.InstanceId) &&
            TryGetConnectionDistanceAtPose(
                m_previewPart,
                snappedTarget,
                m_previewPart.transform.localPosition,
                m_previewPart.transform.localRotation,
                out _);
        if (keepSnapTarget)
        {
            ValidatePreview(true, snappedTarget.InstanceId);
        }
        else
        {
            m_previewSnapTargetId = null;
            ValidatePreview();
        }
        if (!allowInvalid && !CanConfirm)
        {
            PublishMessage(m_validation?.Message ?? "Invalid placement");
            return;
        }

        string targetId = m_validation.ConnectedToInstanceId;
        string displayName = m_previewPart.Definition.DisplayName;
        MPThreeDPart placedStatusPart = m_editingOriginal;
        if (m_editingOriginal != null)
        {
            bool poseChanged =
                (m_editingOriginal.transform.localPosition -
                 m_previewPart.transform.localPosition).sqrMagnitude > 0.000001f ||
                Quaternion.Angle(
                    m_editingOriginal.transform.localRotation,
                    m_previewPart.transform.localRotation) > 0.01f ||
                !string.Equals(
                    m_editingOriginal.ConnectedToInstanceId,
                    targetId,
                    StringComparison.Ordinal);
            if (!poseChanged)
            {
                CancelPreviewInternal(false);
                SetIdleValidation($"Placed {displayName}");
                return;
            }

            m_editingOriginal.SetLocalPose(
                m_previewPart.transform.localPosition,
                m_previewPart.transform.localRotation);
            m_editingOriginal.SetConnection(targetId);
            m_editingOriginal.SetRuntimeVisible(true);
            DestroyRuntimeObject(m_previewPart.gameObject);
        }
        else
        {
            MPThreeDPart committedPart = m_previewPart;
            committedPart.transform.SetParent(m_partsRoot, false);
            committedPart.ConvertPreviewToPlaced(committedPart.InstanceId, targetId);
            m_parts.Add(committedPart);
            placedStatusPart = committedPart;
        }

        m_previewPart = null;
        m_editingOriginal = null;
        m_previewSnapTargetId = null;
        m_pointerManipulatesPart = false;
        m_pointerPartDragDistance = 0f;
        m_pointerMovedPart = false;
        ClearConflictHighlight();
        bool hasWarning = m_validation != null && !m_validation.CanConfirm;
        CommitCurrentState(
            hasWarning
                ? $"Placed {displayName} with warning"
                : $"Placed {displayName}",
            true,
            placedStatusPart);
    }

    public void CancelPreview()
    {
        if (m_previewPart != null)
        {
            CancelPreviewInternal(true);
            return;
        }

        ClearInteractionSelection();
    }

    public void DuplicateEditingPart()
    {
        if (PointerGestureActive)
        {
            return;
        }

        MPThreeDPart source = m_editingOriginal ?? m_persistentSelectedPart;
        MPThreeDPart poseSource = m_previewPart ?? m_persistentSelectedPart;
        if (source == null || poseSource == null)
        {
            PublishMessage("Select a placed part");
            return;
        }

        MPThreeDPartDefinition definition = poseSource.Definition;
        Vector3 preferredPosition = poseSource.transform.localPosition;
        Quaternion rotation = poseSource.transform.localRotation;
        if (m_previewPart != null)
        {
            CancelPreviewInternal(false);
        }
        else
        {
            ClearPersistentSelectionInternal(false);
        }

        ClearAttachmentSourceInternal(false);
        preferredPosition.x += definition.GridStep;
        preferredPosition.y = Mathf.Ceil(
            preferredPosition.y - HEIGHT_INTEGER_EPSILON);
        MPThreeDPart duplicate = MPThreeDPart.Create(
            m_partsRoot,
            definition,
            Guid.NewGuid().ToString("N"),
            false,
            false,
            m_placedMaterial,
            m_previewMaterial,
            m_selectionOutlineMaterial,
            GetSelectionOutlineMesh(definition.Shape),
            GetCollisionMesh(definition.Shape));
        duplicate.SetLocalPose(preferredPosition, rotation);
        bool foundSafePosition = TryFindSafeInitialPosition(
            duplicate,
            preferredPosition,
            out Vector3 safePosition);
        duplicate.SetLocalPose(safePosition, rotation);
        bool connected = TryResolveConnection(
            duplicate,
            null,
            out string targetId);
        duplicate.SetConnection(connected ? targetId : null);
        m_parts.Add(duplicate);
        CommitCurrentState(
            foundSafePosition
                ? $"Duplicated {definition.DisplayName}"
                : $"Duplicated {definition.DisplayName} with overlap warning",
            true,
            duplicate);
        if (!foundSafePosition)
        {
            SetValidation(new MPThreeDValidationResult(
                MPThreeDPlacementState.Collision,
                false,
                false,
                $"No clear space for {definition.DisplayName}; duplicated with warning"),
                false);
        }
    }

    public void DeleteEditingPart()
    {
        if (PointerGestureActive)
        {
            return;
        }

        MPThreeDPart target = m_editingOriginal ?? m_persistentSelectedPart;
        if (target == null)
        {
            PublishMessage("Select a placed part");
            return;
        }

        if (target.IsRoot)
        {
            PublishMessage("The base cannot be deleted");
            return;
        }

        string displayName = target.Definition.DisplayName;
        HashSet<string> deleteIds = new HashSet<string>
        {
            target.InstanceId
        };
        bool added;
        do
        {
            added = false;
            for (int i = 0; i < m_parts.Count; i++)
            {
                MPThreeDPart part = m_parts[i];
                if (part == null ||
                    deleteIds.Contains(part.InstanceId) ||
                    string.IsNullOrEmpty(part.ConnectedToInstanceId) ||
                    !deleteIds.Contains(part.ConnectedToInstanceId))
                {
                    continue;
                }

                deleteIds.Add(part.InstanceId);
                added = true;
            }
        }
        while (added);

        if (m_previewPart != null)
        {
            DestroyRuntimeObject(m_previewPart.gameObject);
            m_previewPart = null;
            m_editingOriginal = null;
        }

        ClearPersistentSelectionInternal(false);
        ClearAttachmentSourceInternal(false);
        for (int i = m_parts.Count - 1; i >= 0; i--)
        {
            MPThreeDPart part = m_parts[i];
            if (part == null || !deleteIds.Contains(part.InstanceId))
            {
                continue;
            }

            m_parts.RemoveAt(i);
            DestroyRuntimeObject(part.gameObject);
        }

        ClearConflictHighlight();
        CommitCurrentState($"Deleted {displayName}", true);
    }

    public void ClearAll()
    {
        if (!EnsureInitialized())
        {
            return;
        }

        if (PointerGestureActive)
        {
            return;
        }

        ClearPersistentSelectionInternal(false);
        ClearAttachmentSourceInternal(false);
        CancelPreviewInternal(false);
        bool removedAny = false;
        for (int i = m_parts.Count - 1; i >= 0; i--)
        {
            MPThreeDPart part = m_parts[i];
            if (part == null || part.IsRoot)
            {
                continue;
            }

            m_parts.RemoveAt(i);
            DestroyRuntimeObject(part.gameObject);
            removedAny = true;
        }

        if (!removedAny)
        {
            PublishMessage("Already empty");
            return;
        }

        CommitCurrentState("Cleared", true);
    }

    public void Undo()
    {
        if (PointerGestureActive)
        {
            return;
        }

        if (m_history == null || !m_history.TryUndo(out MPThreeDAssemblySaveDto state))
        {
            PublishMessage("Nothing to undo");
            return;
        }

        CancelPreviewInternal(false);
        ClearAttachmentSourceInternal(false);
        ApplyState(state, false);
        SetIdleValidation("Undo");
        StateCommitted?.Invoke();
    }

    public void Redo()
    {
        if (PointerGestureActive)
        {
            return;
        }

        if (m_history == null || !m_history.TryRedo(out MPThreeDAssemblySaveDto state))
        {
            PublishMessage("Nothing to redo");
            return;
        }

        CancelPreviewInternal(false);
        ClearAttachmentSourceInternal(false);
        ApplyState(state, false);
        SetIdleValidation("Redo");
        StateCommitted?.Invoke();
    }

    public void TogglePlacementMode()
    {
        if (!EnsureInitialized())
        {
            return;
        }

        if (PointerGestureActive)
        {
            return;
        }

        RequireConnection = !RequireConnection;
        if (m_previewPart != null)
        {
            ValidatePreview();
        }

        CommitCurrentState(
            RequireConnection ? "Snap on" : "Snap off",
            true);
    }

    public void ToggleGrid()
    {
        if (!EnsureInitialized())
        {
            return;
        }

        if (PointerGestureActive)
        {
            return;
        }

        GridVisible = !GridVisible;
        if (m_gridObject != null)
        {
            m_gridObject.SetActive(GridVisible);
        }

        CommitCurrentState(GridVisible ? "Grid on" : "Grid off", true);
    }

    /// <summary>
    /// 开启后，零件位移按 GridStep 跨档，持续选择旋转吸附到绝对 10 度网格；
    /// 开关属于本次编辑会话的操作偏好，不进入作品历史，也不会在手势中切换。
    /// </summary>
    public void ToggleTransformSnap()
    {
        if (!EnsureInitialized() || PointerGestureActive)
        {
            return;
        }

        TransformSnapEnabled = !TransformSnapEnabled;
        PublishMessage(
            TransformSnapEnabled
                ? "Transform step enabled"
                : "Transform step disabled");
    }

    public void CheckAssembly()
    {
        if (!EnsureInitialized())
        {
            return;
        }

        if (m_previewPart != null)
        {
            PublishMessage("Confirm or cancel preview");
            return;
        }

        ClearConflictHighlight();
        RefreshPlacedWarnings();
        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart part = m_parts[i];
            if (part == null)
            {
                continue;
            }

            if (!IsInsideBuildBounds(CalculatePartBounds(part)))
            {
                MPThreeDValidationResult result = new MPThreeDValidationResult(
                    MPThreeDPlacementState.OutOfBounds,
                    false,
                    false,
                    $"Check: {part.Definition.DisplayName} out of bounds");
                SetValidation(result, false);
                return;
            }

            if (!part.IsRoot && !IsConnectedToRoot(part))
            {
                MPThreeDValidationResult result = new MPThreeDValidationResult(
                    MPThreeDPlacementState.NoConnection,
                    false,
                    false,
                    $"Check: {part.Definition.DisplayName} disconnected");
                SetValidation(result, false);
                return;
            }
        }

        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart a = m_parts[i];
            if (a == null)
            {
                continue;
            }

            Bounds boundsA = CalculatePartBounds(a);
            for (int j = i + 1; j < m_parts.Count; j++)
            {
                MPThreeDPart b = m_parts[j];
                if (b == null || !boundsA.Intersects(CalculatePartBounds(b)))
                {
                    continue;
                }

                if (TryGetPenetration(a, b, out _, out float depth) &&
                    depth > PENETRATION_TOLERANCE)
                {
                    b.SetConflictHighlight(true);
                    m_highlightedConflict = b;
                    MPThreeDValidationResult result = new MPThreeDValidationResult(
                        MPThreeDPlacementState.Collision,
                        false,
                        false,
                        $"Check: {a.Definition.DisplayName} overlaps {b.Definition.DisplayName}",
                        null,
                        b);
                    SetValidation(result, false);
                    return;
                }
            }
        }

        MPThreeDValidationResult success = new MPThreeDValidationResult(
            MPThreeDPlacementState.FreeValid,
            false,
            false,
            $"Check passed: {Mathf.Max(0, m_parts.Count - 1)} parts");
        SetValidation(success, false);
    }

    /// <summary>
    /// 幂等释放运行时资源。可由 OnRelease 和 OnDestroy 重复调用。
    /// </summary>
    public void Shutdown()
    {
        if (m_isShuttingDown)
        {
            return;
        }

        if (m_pointerManipulatesPart && m_movingPart != null)
        {
            EndPlacedMove(false);
        }

        m_isShuttingDown = true;
        ClearPersistentSelectionInternal(false);
        ClearAttachmentSourceInternal(false);
        CancelPreviewInternal(false);
        ClearConflictHighlight();

        if (m_camera != null)
        {
            m_camera.targetTexture = null;
        }

        if (RenderTexture != null)
        {
            if (RenderTexture.IsCreated())
            {
                RenderTexture.Release();
            }

            DestroyRuntimeObject(RenderTexture);
            RenderTexture = null;
        }

        if (m_worldRoot != null)
        {
            m_worldRoot.SetActive(false);
            DestroyRuntimeObject(m_worldRoot);
        }

        DestroyRuntimeObject(m_gridMesh);
        DestroyRuntimeObject(m_cubeSelectionOutlineMesh);
        DestroyRuntimeObject(m_cylinderSelectionOutlineMesh);
        DestroyRuntimeObject(m_sphereSelectionOutlineMesh);
        DestroyRuntimeObject(m_cylinderCollisionMesh);
        DestroyRuntimeObject(m_sphereCollisionMesh);
        DestroyRuntimeObject(m_placedMaterial);
        DestroyRuntimeObject(m_previewMaterial);
        DestroyRuntimeObject(m_selectionOutlineMaterial);
        DestroyRuntimeObject(m_groundMaterial);
        DestroyRuntimeObject(m_gridMaterial);

        m_worldRoot = null;
        m_partsRoot = null;
        m_previewRoot = null;
        m_gridObject = null;
        m_camera = null;
        m_light = null;
        m_gridMesh = null;
        m_cubeSelectionOutlineMesh = null;
        m_cylinderSelectionOutlineMesh = null;
        m_sphereSelectionOutlineMesh = null;
        m_cylinderCollisionMesh = null;
        m_sphereCollisionMesh = null;
        m_placedMaterial = null;
        m_previewMaterial = null;
        m_selectionOutlineMaterial = null;
        m_groundMaterial = null;
        m_gridMaterial = null;
        m_previewPart = null;
        m_editingOriginal = null;
        m_previewSnapTargetId = null;
        m_persistentSelectedPart = null;
        m_movingPart = null;
        m_moveSnapTargetId = null;
        m_attachmentSourcePart = null;
        m_validation = null;
        m_history = null;
        m_parts.Clear();
        ClearInteractionSnapshots();
        TransformSnapEnabled = false;
        m_initialized = false;
        m_isShuttingDown = false;
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void CreateWorld(int textureWidth, int textureHeight)
    {
        m_worldRoot = new GameObject("[MPThreeDWorld]");
        m_worldRoot.transform.position = s_worldOffset;
        m_worldRoot.transform.rotation = Quaternion.identity;
        m_worldRoot.transform.localScale = Vector3.one;

        m_partsRoot = new GameObject("PlacedParts").transform;
        m_partsRoot.SetParent(m_worldRoot.transform, false);
        m_previewRoot = new GameObject("PreviewPart").transform;
        m_previewRoot.SetParent(m_worldRoot.transform, false);

        RenderTexture = new RenderTexture(
            textureWidth,
            textureHeight,
            16,
            RenderTextureFormat.ARGB32)
        {
            name = "MPThreeDViewportRT",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        RenderTexture.Create();

        GameObject cameraObject = new GameObject("BuildCamera");
        cameraObject.transform.SetParent(m_worldRoot.transform, false);
        m_camera = cameraObject.AddComponent<Camera>();
        m_camera.orthographic = false;
        m_camera.fieldOfView = 42f;
        m_camera.nearClipPlane = 0.05f;
        m_camera.farClipPlane = 100f;
        m_camera.clearFlags = CameraClearFlags.SolidColor;
        m_camera.backgroundColor = new Color(0.075f, 0.10f, 0.15f, 1f);
        m_camera.cullingMask = (1 << 0) | (1 << 2);
        m_camera.allowHDR = false;
        m_camera.allowMSAA = false;
        m_camera.targetTexture = RenderTexture;

        GameObject lightObject = new GameObject("BuildLight");
        lightObject.transform.SetParent(m_worldRoot.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(50f, -35f, 0f);
        m_light = lightObject.AddComponent<Light>();
        m_light.type = LightType.Point;
        m_light.color = new Color(1f, 0.96f, 0.90f);
        m_light.intensity = 1.35f;
        m_light.range = 30f;
        m_light.shadows = LightShadows.None;
        lightObject.transform.localPosition = new Vector3(4f, 8f, -4f);

        CreateGround();
        CreateGrid();
        m_cameraTarget = new Vector3(0f, 1.2f, 0f);
    }

    private void CreateMaterials()
    {
        Shader standardShader = Shader.Find("Standard");
        if (standardShader == null)
        {
            standardShader = Shader.Find("Unlit/Color");
        }

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null)
        {
            lineShader = standardShader;
        }

        m_placedMaterial = new Material(standardShader)
        {
            name = "MPThreeDPlacedMaterial",
            color = Color.white
        };

        m_previewMaterial = new Material(standardShader)
        {
            name = "MPThreeDPreviewMaterial",
            color = new Color(0.3f, 0.75f, 0.5f, 0.58f)
        };
        ConfigureTransparentMaterial(m_previewMaterial);

        m_selectionOutlineMaterial = new Material(lineShader)
        {
            name = "MPThreeDSelectionOutlineMaterial",
            color = new Color(1f, 0.78f, 0.12f, 1f),
            renderQueue = (int)RenderQueue.Transparent + 20
        };
        CreateSelectionOutlineMeshes();
        CreateCollisionMeshes();

        m_groundMaterial = new Material(standardShader)
        {
            name = "MPThreeDGroundMaterial",
            color = new Color(0.18f, 0.22f, 0.29f, 1f)
        };

        m_gridMaterial = new Material(lineShader)
        {
            name = "MPThreeDGridMaterial",
            color = new Color(0.45f, 0.74f, 0.92f, 0.34f)
        };
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null || !material.HasProperty("_Mode"))
        {
            return;
        }

        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void CreateSelectionOutlineMeshes()
    {
        m_cubeSelectionOutlineMesh = CreateCubeSelectionOutlineMesh();
        m_cylinderSelectionOutlineMesh = CreateCylinderSelectionOutlineMesh();
        m_sphereSelectionOutlineMesh = CreateSphereSelectionOutlineMesh();
    }

    private Mesh GetSelectionOutlineMesh(MPThreeDPartShape shape)
    {
        switch (shape)
        {
            case MPThreeDPartShape.Cylinder:
                return m_cylinderSelectionOutlineMesh;
            case MPThreeDPartShape.Sphere:
                return m_sphereSelectionOutlineMesh;
            default:
                return m_cubeSelectionOutlineMesh;
        }
    }

    private Mesh GetCollisionMesh(MPThreeDPartShape shape)
    {
        switch (shape)
        {
            case MPThreeDPartShape.Cylinder:
                return m_cylinderCollisionMesh;
            case MPThreeDPartShape.Sphere:
                return m_sphereCollisionMesh;
            default:
                return null;
        }
    }

    /// <summary>
    /// 曲面零件使用共享的低模凸网格作为碰撞代理。网格都低于 Unity
    /// convex MeshCollider 的 255 三角面限制，并只在模块初始化时创建一次。
    /// </summary>
    private void CreateCollisionMeshes()
    {
        m_cylinderCollisionMesh = CreateCylinderCollisionMesh();
        m_sphereCollisionMesh = CreateSphereCollisionMesh();
    }

    private static Mesh CreateCylinderCollisionMesh()
    {
        const int segments = 24;
        Vector3[] vertices = new Vector3[segments * 2 + 2];
        int bottomCenter = segments * 2;
        int topCenter = bottomCenter + 1;
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            float x = Mathf.Cos(angle) * 0.5f;
            float z = Mathf.Sin(angle) * 0.5f;
            vertices[i] = new Vector3(x, -1f, z);
            vertices[i + segments] = new Vector3(x, 1f, z);
        }

        vertices[bottomCenter] = new Vector3(0f, -1f, 0f);
        vertices[topCenter] = new Vector3(0f, 1f, 0f);
        int[] triangles = new int[segments * 12];
        int cursor = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[cursor++] = i;
            triangles[cursor++] = i + segments;
            triangles[cursor++] = next + segments;
            triangles[cursor++] = i;
            triangles[cursor++] = next + segments;
            triangles[cursor++] = next;

            triangles[cursor++] = bottomCenter;
            triangles[cursor++] = i;
            triangles[cursor++] = next;
            triangles[cursor++] = topCenter;
            triangles[cursor++] = next + segments;
            triangles[cursor++] = i + segments;
        }

        return CreateCollisionMesh(
            "MPThreeDCylinderCollision",
            vertices,
            triangles);
    }

    private static Mesh CreateSphereCollisionMesh()
    {
        // 频率 3 的 icosphere 共 180 个三角面，比经纬球在极区更均匀，
        // 同时稳定低于 convex MeshCollider 的 255 面限制。
        float goldenRatio = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] baseVertices =
        {
            new Vector3(-1f, goldenRatio, 0f),
            new Vector3(1f, goldenRatio, 0f),
            new Vector3(-1f, -goldenRatio, 0f),
            new Vector3(1f, -goldenRatio, 0f),
            new Vector3(0f, -1f, goldenRatio),
            new Vector3(0f, 1f, goldenRatio),
            new Vector3(0f, -1f, -goldenRatio),
            new Vector3(0f, 1f, -goldenRatio),
            new Vector3(goldenRatio, 0f, -1f),
            new Vector3(goldenRatio, 0f, 1f),
            new Vector3(-goldenRatio, 0f, -1f),
            new Vector3(-goldenRatio, 0f, 1f)
        };
        int[] baseTriangles =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        const int frequency = 3;
        List<Vector3> vertices = new List<Vector3>(200);
        List<int> triangles = new List<int>(180 * 3);
        int[,] faceIndices = new int[frequency + 1, frequency + 1];
        for (int face = 0; face < baseTriangles.Length; face += 3)
        {
            Vector3 a = baseVertices[baseTriangles[face]];
            Vector3 b = baseVertices[baseTriangles[face + 1]];
            Vector3 c = baseVertices[baseTriangles[face + 2]];
            for (int i = 0; i <= frequency; i++)
            {
                for (int j = 0; j <= frequency - i; j++)
                {
                    float weightA = (float)(frequency - i - j) / frequency;
                    float weightB = (float)i / frequency;
                    float weightC = (float)j / frequency;
                    faceIndices[i, j] = vertices.Count;
                    vertices.Add(
                        (a * weightA + b * weightB + c * weightC)
                        .normalized * 0.5f);
                }
            }

            for (int i = 0; i < frequency; i++)
            {
                for (int j = 0; j < frequency - i; j++)
                {
                    int first = faceIndices[i, j];
                    int second = faceIndices[i + 1, j];
                    int third = faceIndices[i, j + 1];
                    triangles.Add(first);
                    triangles.Add(second);
                    triangles.Add(third);
                    if (j >= frequency - i - 1)
                    {
                        continue;
                    }

                    int fourth = faceIndices[i + 1, j + 1];
                    triangles.Add(second);
                    triangles.Add(fourth);
                    triangles.Add(third);
                }
            }
        }

        return CreateCollisionMesh(
            "MPThreeDSphereCollision",
            vertices.ToArray(),
            triangles.ToArray());
    }

    private static Mesh CreateCollisionMesh(
        string meshName,
        Vector3[] vertices,
        int[] triangles)
    {
        Mesh mesh = new Mesh
        {
            name = meshName,
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateCubeSelectionOutlineMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f)
        };
        int[] indices =
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };
        return CreateLineMesh("MPThreeDCubeSelectionOutline", vertices, indices);
    }

    private static Mesh CreateCylinderSelectionOutlineMesh()
    {
        const int segments = 24;
        Vector3[] vertices = new Vector3[segments * 2];
        List<int> indices = new List<int>(segments * 4 + 8);
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            float x = Mathf.Cos(angle) * 0.5f;
            float z = Mathf.Sin(angle) * 0.5f;
            vertices[i] = new Vector3(x, -1f, z);
            vertices[i + segments] = new Vector3(x, 1f, z);
            int next = (i + 1) % segments;
            indices.Add(i);
            indices.Add(next);
            indices.Add(i + segments);
            indices.Add(next + segments);
        }

        for (int i = 0; i < segments; i += segments / 4)
        {
            indices.Add(i);
            indices.Add(i + segments);
        }

        return CreateLineMesh(
            "MPThreeDCylinderSelectionOutline",
            vertices,
            indices.ToArray());
    }

    private static Mesh CreateSphereSelectionOutlineMesh()
    {
        const int segments = 24;
        Vector3[] vertices = new Vector3[segments * 3];
        List<int> indices = new List<int>(segments * 6);
        for (int ring = 0; ring < 3; ring++)
        {
            int offset = ring * segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                float a = Mathf.Cos(angle) * 0.5f;
                float b = Mathf.Sin(angle) * 0.5f;
                vertices[offset + i] = ring == 0
                    ? new Vector3(a, b, 0f)
                    : ring == 1
                        ? new Vector3(a, 0f, b)
                        : new Vector3(0f, a, b);
                indices.Add(offset + i);
                indices.Add(offset + (i + 1) % segments);
            }
        }

        return CreateLineMesh(
            "MPThreeDSphereSelectionOutline",
            vertices,
            indices.ToArray());
    }

    private static Mesh CreateLineMesh(
        string meshName,
        Vector3[] vertices,
        int[] indices)
    {
        Mesh mesh = new Mesh
        {
            name = meshName,
            vertices = vertices
        };
        Color[] colors = new Color[vertices.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.white;
        }

        mesh.colors = colors;
        mesh.SetIndices(indices, MeshTopology.Lines, 0, false);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float GetGroundPlaneHeight()
    {
        return -MPThreeDPartCatalog.RootDefinition.Size.y * 0.5f;
    }

    private void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(m_worldRoot.transform, false);
        ground.transform.localPosition =
            new Vector3(0f, GetGroundPlaneHeight(), 0f);
        ground.transform.localScale = new Vector3(1.4f, 1f, 1.4f);

        // Plane primitive 依赖 MeshCollider；显式引用可避免 IL2CPP Engine Stripping。
        MeshCollider collider = ground.GetComponent<MeshCollider>();
        if (collider != null)
        {
            collider.enabled = false;
            DestroyRuntimeObject(collider);
        }

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = m_groundMaterial;
        }
    }

    private void CreateGrid()
    {
        m_gridObject = new GameObject("VirtualGrid");
        m_gridObject.transform.SetParent(m_worldRoot.transform, false);
        m_gridObject.transform.localPosition =
            new Vector3(0f, GetGroundPlaneHeight() + 0.006f, 0f);
        m_gridObject.layer = 2;

        MeshFilter filter = m_gridObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = m_gridObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = m_gridMaterial;

        const float step = 0.5f;
        int lineCountPerAxis = Mathf.RoundToInt(BUILD_HALF_SIZE * 2f / step) + 1;
        Vector3[] vertices = new Vector3[lineCountPerAxis * 4];
        int[] indices = new int[vertices.Length];
        int cursor = 0;
        for (int i = 0; i < lineCountPerAxis; i++)
        {
            float value = -BUILD_HALF_SIZE + i * step;
            vertices[cursor] = new Vector3(value, 0f, -BUILD_HALF_SIZE);
            indices[cursor] = cursor++;
            vertices[cursor] = new Vector3(value, 0f, BUILD_HALF_SIZE);
            indices[cursor] = cursor++;
            vertices[cursor] = new Vector3(-BUILD_HALF_SIZE, 0f, value);
            indices[cursor] = cursor++;
            vertices[cursor] = new Vector3(BUILD_HALF_SIZE, 0f, value);
            indices[cursor] = cursor++;
        }

        m_gridMesh = new Mesh
        {
            name = "MPThreeDGridMesh"
        };
        m_gridMesh.vertices = vertices;
        m_gridMesh.SetIndices(indices, MeshTopology.Lines, 0, false);
        m_gridMesh.RecalculateBounds();
        filter.sharedMesh = m_gridMesh;
        m_gridObject.SetActive(GridVisible);
    }

    private void CreateDefaultRoot()
    {
        MPThreeDPartDefinition definition = MPThreeDPartCatalog.RootDefinition;
        MPThreeDPart root = MPThreeDPart.Create(
            m_partsRoot,
            definition,
            ROOT_INSTANCE_ID,
            true,
            false,
            m_placedMaterial,
            m_previewMaterial,
            m_selectionOutlineMaterial,
            GetSelectionOutlineMesh(definition.Shape),
            GetCollisionMesh(definition.Shape));
        root.SetLocalPose(
            Vector3.zero,
            Quaternion.Euler(definition.DefaultEuler));
        root.SetConnection(null);
        m_parts.Add(root);
    }

    /// <summary>
    /// 从视口中心开始向外做方形环搜索。只在新增零件时执行，
    /// 不进入帧循环；找不到时由调用者保留中心位置并显示碰撞警告。
    /// </summary>
    private bool TryFindSafeInitialPosition(
        MPThreeDPart part,
        Vector3 preferredPosition,
        out Vector3 safePosition)
    {
        preferredPosition.y = Mathf.Ceil(
            preferredPosition.y - HEIGHT_INTEGER_EPSILON);
        safePosition = preferredPosition;
        if (part == null)
        {
            return false;
        }

        float step = Mathf.Max(0.05f, part.Definition.GridStep);
        int maxRing = Mathf.CeilToInt(BUILD_HALF_SIZE * 2f / step);
        float verticalExtent = GetShapeAabbExtents(
            part.Definition.Shape,
            part.Definition.Size,
            part.transform.localRotation).y;
        int maxHeightLevel = Mathf.Clamp(
            Mathf.FloorToInt(
                (m_buildBounds.max.y - preferredPosition.y - verticalExtent +
                 HEIGHT_INTEGER_EPSILON) /
                INITIAL_HEIGHT_STEP),
            0,
            8);
        for (int ring = 0; ring <= maxRing; ring++)
        {
            for (int x = -ring; x <= ring; x++)
            {
                for (int z = -ring; z <= ring; z++)
                {
                    if (ring > 0 && Mathf.Abs(x) != ring && Mathf.Abs(z) != ring)
                    {
                        continue;
                    }

                    for (int heightLevel = 0;
                         heightLevel <= maxHeightLevel;
                         heightLevel++)
                    {
                        Vector3 candidate = preferredPosition +
                            new Vector3(
                                x * step,
                                heightLevel * INITIAL_HEIGHT_STEP,
                                z * step);
                        part.SetLocalPose(
                            candidate,
                            part.transform.localRotation);
                        if (!IsInsideBuildBounds(CalculatePartBounds(part)) ||
                            HasCollision(part))
                        {
                            continue;
                        }

                        safePosition = candidate;
                        return true;
                    }
                }
            }
        }

        part.SetLocalPose(preferredPosition, part.transform.localRotation);
        return false;
    }

    private static Vector3 GetShapeAabbExtents(
        MPThreeDPartShape shape,
        Vector3 size,
        Quaternion rotation)
    {
        Vector3 right = rotation * Vector3.right;
        Vector3 up = rotation * Vector3.up;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 halfSize = size * 0.5f;
        switch (shape)
        {
            case MPThreeDPartShape.Sphere:
                return new Vector3(
                    Mathf.Sqrt(
                        right.x * right.x * halfSize.x * halfSize.x +
                        up.x * up.x * halfSize.y * halfSize.y +
                        forward.x * forward.x * halfSize.z * halfSize.z),
                    Mathf.Sqrt(
                        right.y * right.y * halfSize.x * halfSize.x +
                        up.y * up.y * halfSize.y * halfSize.y +
                        forward.y * forward.y * halfSize.z * halfSize.z),
                    Mathf.Sqrt(
                        right.z * right.z * halfSize.x * halfSize.x +
                        up.z * up.z * halfSize.y * halfSize.y +
                        forward.z * forward.z * halfSize.z * halfSize.z));

            case MPThreeDPartShape.Cylinder:
                return new Vector3(
                    Mathf.Abs(up.x) * halfSize.y + Mathf.Sqrt(
                        right.x * right.x * halfSize.x * halfSize.x +
                        forward.x * forward.x * halfSize.z * halfSize.z),
                    Mathf.Abs(up.y) * halfSize.y + Mathf.Sqrt(
                        right.y * right.y * halfSize.x * halfSize.x +
                        forward.y * forward.y * halfSize.z * halfSize.z),
                    Mathf.Abs(up.z) * halfSize.y + Mathf.Sqrt(
                        right.z * right.z * halfSize.x * halfSize.x +
                        forward.z * forward.z * halfSize.z * halfSize.z));

            default:
                return new Vector3(
                    Mathf.Abs(right.x) * halfSize.x +
                    Mathf.Abs(up.x) * halfSize.y +
                    Mathf.Abs(forward.x) * halfSize.z,
                    Mathf.Abs(right.y) * halfSize.x +
                    Mathf.Abs(up.y) * halfSize.y +
                    Mathf.Abs(forward.y) * halfSize.z,
                    Mathf.Abs(right.z) * halfSize.x +
                    Mathf.Abs(up.z) * halfSize.y +
                    Mathf.Abs(forward.z) * halfSize.z);
        }
    }

    private void BeginEdit(MPThreeDPart original)
    {
        CancelPreviewInternal(false);
        m_editingOriginal = original;
        original.SetRuntimeVisible(false);
        m_previewPart = MPThreeDPart.Create(
            m_previewRoot,
            original.Definition,
            original.InstanceId,
            false,
            true,
            m_placedMaterial,
            m_previewMaterial,
            m_selectionOutlineMaterial,
            GetSelectionOutlineMesh(original.Definition.Shape),
            GetCollisionMesh(original.Definition.Shape));
        m_previewPart.SetLocalPose(
            original.transform.localPosition,
            original.transform.localRotation);
        m_previewPart.SetConnection(original.ConnectedToInstanceId);
        m_previewPart.SetSelectionOutline(true);
        m_previewPlaneHeight = original.transform.localPosition.y;
        ValidatePreview();
        PublishMessage($"Editing {original.Definition.DisplayName} - release to place");
    }

    private bool BeginPlacedMove(
        MPThreeDPart part,
        Vector2 viewportPosition)
    {
        if (part == null || part.IsRoot)
        {
            return false;
        }

        ClearAttachmentSourceInternal(false);
        CaptureInteractionPart(part);
        if (m_interactionStartPoses.Count == 0)
        {
            return false;
        }

        m_movingPart = part;
        m_moveStartPosition = part.transform.localPosition;
        m_moveAxisPlane = GetCameraFacingMoveAxisPlane();
        Vector3 moveStartWorldPosition = m_worldRoot.transform.TransformPoint(
            m_moveStartPosition);
        Vector3 movePlaneWorldNormal = m_worldRoot.transform.TransformDirection(
            GetMoveAxisPlaneNormal(m_moveAxisPlane));
        // 根据按下瞬间最接近的正/侧/俯视方向锁定 XY、YZ 或 XZ 平面。
        m_moveDragPlane = new Plane(
            movePlaneWorldNormal,
            moveStartWorldPosition);
        Vector3 planePoint;
        if (!TryGetPointOnMoveDragPlane(viewportPosition, out planePoint))
        {
            planePoint = m_moveStartPosition;
        }

        m_moveDragOffset = m_moveStartPosition - planePoint;
        m_pointerManipulatesPart = true;
        m_pointerPartDragDistance = 0f;
        m_pointerMovedPart = false;
        m_moveSnapTargetId = null;
        part.SetSelectionOutline(true);
        PublishMessage(
            $"Moving {part.Definition.DisplayName} on {m_moveAxisPlane} plane");
        return true;
    }

    private void UpdatePlacedMove(Vector2 viewportPosition)
    {
        if (!m_pointerManipulatesPart || m_movingPart == null)
        {
            return;
        }

        if (!TryGetPointOnMoveDragPlane(
                viewportPosition,
                out Vector3 dragPlanePoint))
        {
            return;
        }

        Vector3 rawPosition = ConstrainToMoveAxisPlane(
            dragPlanePoint + m_moveDragOffset);
        Vector3 candidatePosition = TransformSnapEnabled
            ? GetGridThresholdPosition(
                m_moveStartPosition,
                rawPosition,
                m_movingPart.Definition.GridStep)
            : rawPosition;
        m_moveSnapTargetId = null;
        if (RequireConnection && TryFindSocketSnapForPart(
                m_movingPart,
                candidatePosition,
                m_movingPart.transform.localRotation,
                m_interactionPartIds,
                out Vector3 snappedPosition,
                out string snapTargetId))
        {
            candidatePosition = snappedPosition;
            m_moveSnapTargetId = snapTargetId;
        }

        Vector3 translation = candidatePosition - m_moveStartPosition;
        for (int i = 0; i < m_interactionStartPoses.Count; i++)
        {
            PartPoseSnapshot snapshot = m_interactionStartPoses[i];
            if (snapshot.Part != null)
            {
                snapshot.Part.SetLocalPose(
                    snapshot.Position + translation,
                    snapshot.Rotation);
            }
        }

        m_pointerMovedPart = translation.sqrMagnitude > 0.000001f;
    }

    private void EndPlacedMove(bool commit)
    {
        MPThreeDPart movedPart = m_movingPart;
        bool changed = m_pointerMovedPart && movedPart != null;
        if (!commit || !changed)
        {
            RestoreInteractionPoses();
        }
        else if (changed)
        {
            Physics.SyncTransforms();
            bool preservedSnapTarget = false;
            MPThreeDPart snapTarget = FindPart(m_moveSnapTargetId);
            if (snapTarget != null &&
                !WouldCreateConnectionCycle(movedPart, snapTarget.InstanceId) &&
                ArePartsSocketConnected(movedPart, snapTarget))
            {
                movedPart.SetConnection(snapTarget.InstanceId);
                preservedSnapTarget = true;
            }

            RefreshConnectionsAfterPartChanged(
                movedPart,
                !preservedSnapTarget);
        }

        if (movedPart != null)
        {
            movedPart.SetSelectionOutline(false);
        }

        m_movingPart = null;
        m_pointerManipulatesPart = false;
        m_pointerPartDragDistance = 0f;
        m_pointerMovedPart = false;
        m_moveDragOffset = Vector3.zero;
        m_moveSnapTargetId = null;
        ClearInteractionSnapshots();
        if (commit && changed)
        {
            CommitCurrentState(
                $"Moved {movedPart.Definition.DisplayName}",
                true,
                movedPart);
        }
        else
        {
            RefreshPlacedWarnings();
        }
    }

    private void CaptureInteractionPart(MPThreeDPart part)
    {
        ClearInteractionSnapshots();
        if (part == null)
        {
            return;
        }

        m_interactionPartIds.Add(part.InstanceId);
        m_interactionStartPoses.Add(new PartPoseSnapshot
        {
            Part = part,
            Position = part.transform.localPosition,
            Rotation = part.transform.localRotation,
            ConnectionId = part.ConnectedToInstanceId
        });
    }

    private bool TryAttachSourceToTarget(MPThreeDPart target)
    {
        MPThreeDPart source = m_attachmentSourcePart;
        if (source == null || target == null || target.IsRoot)
        {
            return false;
        }

        CaptureInteractionPart(source);
        if (source == target ||
            WouldCreateConnectionCycle(source, target.InstanceId))
        {
            ClearInteractionSnapshots();
            PublishMessage("Cannot attach a part to itself or its descendant");
            return false;
        }

        Vector3 sourceStartPosition = source.transform.localPosition;
        Vector3 centerDelta =
            target.transform.localPosition - sourceStartPosition;
        float maxTravel = centerDelta.magnitude;
        if (!TryGetLineSnapPosition(
                source,
                target,
                sourceStartPosition,
                source.transform.localRotation,
                maxTravel,
                out Vector3 snappedPosition,
                out _))
        {
            // 源与目标已经处于穿模状态时无法再寻找“首次接触点”。
            // 显式三击吸附仍允许直接建立连接，当前位置由警告系统标红。
            Physics.SyncTransforms();
            if (!TryGetPenetrationAtPose(
                    source,
                    sourceStartPosition,
                    source.transform.localRotation,
                    target,
                    out _))
            {
                RestoreInteractionPoses();
                ClearInteractionSnapshots();
                PublishMessage(
                    "No straight-line contact between these parts");
                return false;
            }

            snappedPosition = sourceStartPosition;
        }

        // 三击吸附是用户明确指定的直接重定位，不受源零件现有接触、
        // 中间路径障碍或最终第三方穿模限制；穿模会在提交后继续标红提示。
        ApplyInteractionTranslation(snappedPosition - sourceStartPosition);
        Physics.SyncTransforms();
        source.SetConnection(target.InstanceId);
        RefreshConnectionsAfterPartChanged(source, false);
        string displayName = source.Definition.DisplayName;
        ClearInteractionSnapshots();
        ClearAttachmentSourceInternal(false);
        CommitCurrentState(
            $"Attached {displayName} to {target.Definition.DisplayName}",
            true,
            source);
        return true;
    }

    private bool IsLinePathBlocked(
        MPThreeDPart source,
        MPThreeDPart target,
        Vector3 sourceStartPosition,
        Quaternion sourceRotation,
        Vector3 direction,
        float targetContactTravel)
    {
        float pathEndTravel = Mathf.Max(
            0f,
            targetContactTravel - LINE_SNAP_SKIN);
        if (direction.sqrMagnitude <= 0.000001f ||
            pathEndTravel <= CONTACT_PROBE_DISTANCE)
        {
            return false;
        }

        direction.Normalize();
        float initialProbe = Mathf.Min(0.01f, pathEndTravel * 0.5f);
        Vector3 probeStart = sourceStartPosition + direction * initialProbe;
        float remainingTravel = pathEndTravel - initialProbe;
        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart other = m_parts[i];
            if (other == null ||
                other == source ||
                other == target ||
                other.CollisionProxy == null ||
                !other.CollisionProxy.enabled)
            {
                continue;
            }

            float blockerProbeTravel = Mathf.Max(
                CONTACT_PROBE_DISTANCE,
                remainingTravel + CONTACT_PROBE_DISTANCE);
            if (!TryGetSweptAabbInterval(
                    source,
                    other,
                    probeStart,
                    sourceRotation,
                    direction,
                    blockerProbeTravel,
                    out float blockerIntervalEnter,
                    out _))
            {
                continue;
            }

            if (blockerIntervalEnter <= CONTACT_PROBE_DISTANCE &&
                TryGetPenetrationAtPose(
                    source,
                    probeStart,
                    sourceRotation,
                    other,
                    out float probeDepth) &&
                probeDepth > CONTACT_QUERY_TOLERANCE)
            {
                return true;
            }

            if (remainingTravel > 0f &&
                TryGetFirstContactTravel(
                    source,
                    other,
                    probeStart,
                    sourceRotation,
                    direction,
                    remainingTravel,
                    out float blockerTravel) &&
                blockerTravel <= remainingTravel + 0.0001f)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyInteractionTranslation(Vector3 translation)
    {
        for (int i = 0; i < m_interactionStartPoses.Count; i++)
        {
            PartPoseSnapshot snapshot = m_interactionStartPoses[i];
            if (snapshot.Part != null)
            {
                snapshot.Part.SetLocalPose(
                    snapshot.Position + translation,
                    snapshot.Rotation);
            }
        }
    }

    /// <summary>
    /// 显式三击吸附不限制移动距离或构建边界。
    /// 这里只拒绝最终落点的真实穿模；越界状态由提交后的
    /// RefreshPlacedWarnings 统一标红提示。
    /// </summary>
    private bool IsCurrentInteractionCollisionFree()
    {
        for (int i = 0; i < m_interactionStartPoses.Count; i++)
        {
            MPThreeDPart part = m_interactionStartPoses[i].Part;
            if (part == null)
            {
                return false;
            }

            Bounds bounds = CalculatePartBounds(part);
            for (int otherIndex = 0; otherIndex < m_parts.Count; otherIndex++)
            {
                MPThreeDPart other = m_parts[otherIndex];
                if (other == null ||
                    m_interactionPartIds.Contains(other.InstanceId) ||
                    other.CollisionProxy == null ||
                    !other.CollisionProxy.enabled ||
                    !bounds.Intersects(CalculatePartBounds(other)))
                {
                    continue;
                }

                if (TryGetPenetration(part, other, out _, out float depth) &&
                    depth > PENETRATION_TOLERANCE)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void RestoreInteractionPoses()
    {
        for (int i = 0; i < m_interactionStartPoses.Count; i++)
        {
            PartPoseSnapshot snapshot = m_interactionStartPoses[i];
            if (snapshot.Part == null)
            {
                continue;
            }

            snapshot.Part.SetLocalPose(snapshot.Position, snapshot.Rotation);
            snapshot.Part.SetConnection(snapshot.ConnectionId);
        }
    }

    private void ClearInteractionSnapshots()
    {
        m_interactionStartPoses.Clear();
        m_interactionPartIds.Clear();
    }

    private static Vector3 GetGridThresholdPosition(
        Vector3 start,
        Vector3 raw,
        float gridStep)
    {
        float step = Mathf.Max(0.05f, gridStep);
        return new Vector3(
            GetGridThresholdCoordinate(start.x, raw.x, step),
            GetGridThresholdCoordinate(start.y, raw.y, step),
            GetGridThresholdCoordinate(start.z, raw.z, step));
    }

    private static float GetGridThresholdCoordinate(
        float start,
        float raw,
        float step)
    {
        float dragDistance = raw - start;
        int stepCount = Mathf.FloorToInt(
            (Mathf.Abs(dragDistance) + 0.0001f) / step);
        if (stepCount <= 0)
        {
            return start;
        }

        int direction = dragDistance > 0f ? 1 : -1;
        float gridPosition = start / step;
        int firstTargetIndex = direction > 0
            ? Mathf.FloorToInt(gridPosition + ROTATION_SNAP_GRID_EPSILON) + 1
            : Mathf.CeilToInt(gridPosition - ROTATION_SNAP_GRID_EPSILON) - 1;
        int targetIndex = firstTargetIndex + direction * (stepCount - 1);
        return targetIndex * step;
    }

    private MPThreeDPart RaycastEditablePart(Vector2 viewportPosition)
    {
        MPThreeDPart part = RaycastPlacedPart(viewportPosition);
        if (part == null || part.IsPreview || part.IsRoot)
        {
            return null;
        }

        return part;
    }

    private MPThreeDPart RaycastPlacedPart(Vector2 viewportPosition)
    {
        // 工程关闭了 Physics.autoSyncTransforms；命中测试只发生在用户点击时，
        // 此处显式同步可避免刚移动/旋转后的静态碰撞体仍停留在旧物理姿态。
        Physics.SyncTransforms();
        Ray ray = ViewportRay(viewportPosition);
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                100f,
                1 << 0,
                QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        return hit.collider.GetComponentInParent<MPThreeDPart>();
    }

    private void BeginPersistentRotation(Vector2 viewportPosition)
    {
        if (m_persistentSelectedPart == null)
        {
            return;
        }

        CaptureInteractionPart(m_persistentSelectedPart);
        m_pointerRotatesPersistentPart = true;
        m_persistentRotationMoved = false;
        m_persistentRotationAxisLocked = false;
        m_persistentRotationUsesHorizontalDrag = false;
        m_persistentRotationStartViewport = viewportPosition;
        m_persistentRotationStartLocalRotation =
            m_persistentSelectedPart.transform.localRotation;
        ChoosePersistentRotationAxes();
    }

    private void ChoosePersistentRotationAxes()
    {
        if (m_persistentSelectedPart == null || m_camera == null)
        {
            m_verticalDragRotationLocalAxis = Vector3.right;
            m_horizontalDragRotationLocalAxis = Vector3.up;
            return;
        }

        Vector3 cameraRight = m_camera.transform.right.normalized;
        Vector3 cameraUp = m_camera.transform.up.normalized;
        Vector3[] worldAxes = new Vector3[s_partPrincipalAxes.Length];
        float[] rightScores = new float[s_partPrincipalAxes.Length];
        float[] upScores = new float[s_partPrincipalAxes.Length];
        for (int i = 0; i < s_partPrincipalAxes.Length; i++)
        {
            Vector3 worldAxis = m_persistentSelectedPart.transform
                .TransformDirection(s_partPrincipalAxes[i])
                .normalized;
            worldAxes[i] = worldAxis;
            // 直接比较三维方向，避免几乎朝向镜头的轴因极小屏幕投影被误判为满分。
            rightScores[i] = Mathf.Abs(Vector3.Dot(worldAxis, cameraRight));
            upScores[i] = Mathf.Abs(Vector3.Dot(worldAxis, cameraUp));
        }

        int rightAxisIndex = 0;
        int upAxisIndex = 1;
        float bestScore = float.MinValue;
        for (int rightIndex = 0; rightIndex < s_partPrincipalAxes.Length; rightIndex++)
        {
            for (int upIndex = 0; upIndex < s_partPrincipalAxes.Length; upIndex++)
            {
                if (rightIndex == upIndex)
                {
                    continue;
                }

                float score = rightScores[rightIndex] + upScores[upIndex];
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                rightAxisIndex = rightIndex;
                upAxisIndex = upIndex;
            }
        }

        float rightSign = Vector3.Dot(worldAxes[rightAxisIndex], cameraRight) < 0f
            ? -1f
            : 1f;
        float upSign = Vector3.Dot(worldAxes[upAxisIndex], cameraUp) < 0f
            ? -1f
            : 1f;
        // 上下拖拽绕屏幕横向主轴；左右拖拽绕屏幕纵向主轴。
        m_verticalDragRotationLocalAxis =
            s_partPrincipalAxes[rightAxisIndex] * rightSign;
        m_horizontalDragRotationLocalAxis =
            s_partPrincipalAxes[upAxisIndex] * upSign;
    }

    private void UpdatePersistentRotation(Vector2 viewportPosition)
    {
        if (!m_pointerRotatesPersistentPart || m_persistentSelectedPart == null)
        {
            return;
        }

        Vector2 totalDelta = viewportPosition - m_persistentRotationStartViewport;
        Vector2 totalPixelDelta = GetViewportScreenPixelDelta(totalDelta);
        if (!m_persistentRotationAxisLocked)
        {
            if (totalDelta.magnitude < PART_ROTATION_VIEWPORT_THRESHOLD)
            {
                return;
            }

            m_persistentRotationUsesHorizontalDrag =
                Mathf.Abs(totalPixelDelta.x) >= Mathf.Abs(totalPixelDelta.y);
            m_persistentRotationAxisLocked = true;
        }

        Vector3 localAxis = m_persistentRotationUsesHorizontalDrag
            ? m_horizontalDragRotationLocalAxis
            : m_verticalDragRotationLocalAxis;
        float dragAmount = m_persistentRotationUsesHorizontalDrag
            ? -totalDelta.x
            : totalDelta.y;
        float angle;
        if (TransformSnapEnabled)
        {
            float dragPixels = m_persistentRotationUsesHorizontalDrag
                ? -totalPixelDelta.x
                : totalPixelDelta.y;
            float startAxisAngle = GetSignedTwistAngle(
                m_persistentRotationStartLocalRotation,
                localAxis);
            angle = GetAbsoluteSnapRotationDelta(
                startAxisAngle,
                dragPixels);
        }
        else
        {
            angle = dragAmount * PART_ROTATION_SENSITIVITY;
        }

        Quaternion rotation =
            m_persistentRotationStartLocalRotation *
            Quaternion.AngleAxis(angle, localAxis);
        m_persistentRotationMoved = Quaternion.Angle(
            m_persistentRotationStartLocalRotation,
            rotation) > 0.01f;
        if (Quaternion.Angle(
                m_persistentSelectedPart.transform.localRotation,
                rotation) <= 0.001f)
        {
            // 取整模式在同一档位内不重复做碰撞和连接校验。
            return;
        }

        Quaternion groupRotation =
            rotation *
            Quaternion.Inverse(m_persistentRotationStartLocalRotation);
        Vector3 pivot = m_persistentSelectedPart.transform.localPosition;
        for (int i = 0; i < m_interactionStartPoses.Count; i++)
        {
            PartPoseSnapshot snapshot = m_interactionStartPoses[i];
            if (snapshot.Part == null)
            {
                continue;
            }

            snapshot.Part.SetLocalPose(
                pivot + groupRotation * (snapshot.Position - pivot),
                groupRotation * snapshot.Rotation);
        }
    }

    /// <summary>
    /// 从局部旋转中提取绕指定局部轴的有符号 twist 角度。
    /// 旋转操作采用 startRotation * AngleAxis(delta, localAxis)，因此只改变该 twist，
    /// 可以在保留其余朝向的同时把当前轴吸附到绝对 10 度网格。
    /// </summary>
    private static float GetSignedTwistAngle(
        Quaternion rotation,
        Vector3 localAxis)
    {
        if (localAxis.sqrMagnitude < 0.000001f)
        {
            return 0f;
        }

        localAxis.Normalize();
        Vector3 rotationVector = new Vector3(
            rotation.x,
            rotation.y,
            rotation.z);
        Vector3 projected = Vector3.Project(rotationVector, localAxis);
        float magnitude = Mathf.Sqrt(
            projected.sqrMagnitude + rotation.w * rotation.w);
        if (magnitude < 0.000001f)
        {
            // 180 度 swing 与目标轴正交时标准 twist 分解退化，
            // 改用旋转后的正交参考向量定义稳定 roll，避免直接返回任意角度。
            return GetReferenceAxisAngle(rotation, localAxis);
        }

        projected /= magnitude;
        float twistW = rotation.w / magnitude;
        float sinHalfAngle = Vector3.Dot(projected, localAxis);
        float signedAngle =
            2f * Mathf.Atan2(sinHalfAngle, twistW) * Mathf.Rad2Deg;
        return Mathf.DeltaAngle(0f, signedAngle);
    }

    private static float GetReferenceAxisAngle(
        Quaternion rotation,
        Vector3 localAxis)
    {
        Vector3 localReference = Mathf.Abs(
            Vector3.Dot(localAxis, Vector3.up)) < 0.9f
            ? Vector3.up
            : Vector3.right;
        localReference = Vector3.ProjectOnPlane(
            localReference,
            localAxis).normalized;

        Vector3 rotatedAxis = (rotation * localAxis).normalized;
        Vector3 rotatedReference = (rotation * localReference).normalized;
        Vector3 seed;
        float absX = Mathf.Abs(rotatedAxis.x);
        float absY = Mathf.Abs(rotatedAxis.y);
        float absZ = Mathf.Abs(rotatedAxis.z);
        if (absX <= absY && absX <= absZ)
        {
            seed = Vector3.right;
        }
        else if (absY <= absZ)
        {
            seed = Vector3.up;
        }
        else
        {
            seed = Vector3.forward;
        }

        Vector3 absoluteReference = Vector3.ProjectOnPlane(
            seed,
            rotatedAxis).normalized;
        return Vector3.SignedAngle(
            absoluteReference,
            rotatedReference,
            rotatedAxis);
    }

    /// <summary>
    /// 计算从当前绝对轴角到 10 度网格目标的旋转增量。
    /// 例如起始 5 度：正向第一档为 +5 度（到 10），负向第一档为 -5 度（到 0）。
    /// </summary>
    private static float GetAbsoluteSnapRotationDelta(
        float startAxisAngle,
        float dragPixels)
    {
        int stepCount = Mathf.FloorToInt(
            (Mathf.Abs(dragPixels) + 0.0001f) /
            ROTATION_SNAP_DRAG_THRESHOLD_PIXELS);
        if (stepCount <= 0)
        {
            return 0f;
        }

        int direction = dragPixels > 0f ? 1 : -1;
        float normalizedStart = Mathf.DeltaAngle(0f, startAxisAngle);
        float gridPosition = normalizedStart / ROTATION_SNAP_ANGLE;
        int firstTargetIndex = direction > 0
            ? Mathf.FloorToInt(gridPosition + ROTATION_SNAP_GRID_EPSILON) + 1
            : Mathf.CeilToInt(gridPosition - ROTATION_SNAP_GRID_EPSILON) - 1;
        int targetIndex = firstTargetIndex + direction * (stepCount - 1);
        float targetAngle = targetIndex * ROTATION_SNAP_ANGLE;
        return targetAngle - normalizedStart;
    }

    private Vector2 GetViewportScreenPixelDelta(Vector2 viewportDelta)
    {
        float width = Screen.width > 0
            ? Screen.width
            : RenderTexture != null
                ? RenderTexture.width
                : 1080f;
        float height = Screen.height > 0
            ? Screen.height
            : RenderTexture != null
                ? RenderTexture.height
                : 1080f;

        return new Vector2(
            viewportDelta.x * width,
            viewportDelta.y * height);
    }

    private void EndPersistentRotation(bool commit)
    {
        MPThreeDPart selectedPart = m_persistentSelectedPart;
        bool changed = m_persistentRotationMoved && selectedPart != null;
        if ((!commit || !changed) && selectedPart != null)
        {
            RestoreInteractionPoses();
        }

        m_pointerRotatesPersistentPart = false;
        m_persistentRotationMoved = false;
        m_persistentRotationAxisLocked = false;
        ClearInteractionSnapshots();
        if (commit && changed)
        {
            CommitCurrentState($"Rotated {selectedPart.Definition.DisplayName}", true);
        }
        else
        {
            RefreshPlacedWarnings();
        }
    }

    private void FinalizePersistentPartChange(string message)
    {
        if (m_persistentSelectedPart == null)
        {
            return;
        }

        RefreshPersistentPartDerivedState(m_persistentSelectedPart);
        CommitCurrentState(message, true);
    }

    private void RefreshPersistentPartDerivedState(MPThreeDPart part)
    {
        if (part == null)
        {
            return;
        }

        RefreshConnectionsAfterPartChanged(part, true);
        bool hasWarning =
            !IsInsideBuildBounds(CalculatePartBounds(part)) ||
            !IsConnectedToRoot(part) ||
            HasCollision(part);
        part.SetPlacementWarning(hasWarning);
    }

    /// <summary>
    /// 当前零件独立变换后只立即重算它自身的父连接。
    /// 其他零件不跟随变换，也不改写原逻辑父子关系；
    /// 当前是否真实接触交给派生警告计算。
    /// </summary>
    private void RefreshConnectionsAfterPartChanged(
        MPThreeDPart changedPart,
        bool resolveChangedPart)
    {
        if (changedPart == null)
        {
            return;
        }

        // 工程关闭了 autoSyncTransforms；位移或高度变化后的
        // 连接查询必须先同步物理姿态。纯旋转不进入此方法，
        // 以保留用户已指定的逻辑父子关系。
        Physics.SyncTransforms();
        if (resolveChangedPart)
        {
            ResolvePartConnection(changedPart);
        }

        BreakConnectionCycles();
    }

    private void ResolvePartConnection(MPThreeDPart part)
    {
        if (part == null || part.IsRoot)
        {
            return;
        }

        MPThreeDPart currentTarget = FindPart(part.ConnectedToInstanceId);
        if (currentTarget != null &&
            currentTarget != part &&
            !WouldCreateConnectionCycle(part, currentTarget.InstanceId) &&
            ArePartsSocketConnected(part, currentTarget))
        {
            return;
        }

        bool connected = TryResolveConnection(
            part,
            part,
            out string targetId);
        part.SetConnection(connected ? targetId : null);
    }

    private bool WouldCreateConnectionCycle(
        MPThreeDPart source,
        string targetId)
    {
        if (source == null || string.IsNullOrEmpty(targetId))
        {
            return false;
        }

        HashSet<string> visited = new HashSet<string>();
        MPThreeDPart current = FindPart(targetId);
        while (current != null && !current.IsRoot)
        {
            if (current == source ||
                current.InstanceId == source.InstanceId ||
                !visited.Add(current.InstanceId))
            {
                return true;
            }

            current = FindPart(current.ConnectedToInstanceId);
        }

        return false;
    }

    private bool HasCollision(MPThreeDPart part)
    {
        if (part == null)
        {
            return false;
        }

        Bounds bounds = CalculatePartBounds(part);
        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart other = m_parts[i];
            if (other == null ||
                other == part ||
                other.CollisionProxy == null ||
                !other.CollisionProxy.enabled ||
                !bounds.Intersects(CalculatePartBounds(other)))
            {
                continue;
            }

            if (TryGetPenetration(part, other, out _, out float depth) &&
                depth > PENETRATION_TOLERANCE)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyCandidatePose(Vector3 rawPosition, Quaternion rawRotation)
    {
        if (m_previewPart == null)
        {
            return;
        }

        Vector3 candidatePosition = rawPosition;
        Quaternion candidateRotation = rawRotation.normalized;
        Vector3 snappedPosition = rawPosition;
        string snapTargetId = null;
        bool snapped = RequireConnection && TryFindSocketSnap(
            rawPosition,
            candidateRotation,
            out snappedPosition,
            out snapTargetId);
        if (snapped)
        {
            candidatePosition = snappedPosition;
        }

        m_previewSnapTargetId = snapped ? snapTargetId : null;
        m_previewPart.SetLocalPose(candidatePosition, candidateRotation);
        // 吸附时保留本次实际命中的目标，避免多目标邻接时视觉目标与逻辑父对象不一致。
        if (snapped)
        {
            ValidatePreview(true, snapTargetId);
        }
        else
        {
            ValidatePreview();
        }
    }

    private bool TryFindSocketSnap(
        Vector3 rawPosition,
        Quaternion rawRotation,
        out Vector3 snappedPosition,
        out string targetId)
    {
        snappedPosition = rawPosition;
        targetId = null;
        if (m_previewPart == null)
        {
            return false;
        }

        float bestTravel = float.MaxValue;
        bool found = false;
        for (int partIndex = 0; partIndex < m_parts.Count; partIndex++)
        {
            MPThreeDPart target = m_parts[partIndex];
            if (target == null ||
                target == m_editingOriginal ||
                target.CollisionProxy == null ||
                WouldCreateConnectionCycle(
                    m_editingOriginal ?? m_previewPart,
                    target.InstanceId) ||
                !target.CollisionProxy.enabled)
            {
                continue;
            }

            if (!TryGetLineSnapPosition(
                    m_previewPart,
                    target,
                    rawPosition,
                    rawRotation,
                    SNAP_DISTANCE,
                    out Vector3 candidatePosition,
                    out float travel) ||
                travel >= bestTravel)
            {
                continue;
            }

            bestTravel = travel;
            snappedPosition = candidatePosition;
            targetId = target.InstanceId;
            found = true;
        }

        return found;
    }

    private bool TryFindSocketSnapForPart(
        MPThreeDPart source,
        Vector3 rawPosition,
        Quaternion rawRotation,
        HashSet<string> excludedPartIds,
        out Vector3 snappedPosition,
        out string targetId)
    {
        snappedPosition = rawPosition;
        targetId = null;
        if (source == null)
        {
            return false;
        }

        float bestTravel = float.MaxValue;
        bool found = false;
        for (int partIndex = 0; partIndex < m_parts.Count; partIndex++)
        {
            MPThreeDPart target = m_parts[partIndex];
            if (target == null ||
                target == source ||
                target.CollisionProxy == null ||
                !target.CollisionProxy.enabled ||
                (excludedPartIds != null &&
                 excludedPartIds.Contains(target.InstanceId)) ||
                WouldCreateConnectionCycle(source, target.InstanceId))
            {
                continue;
            }

            if (!TryGetLineSnapPosition(
                    source,
                    target,
                    rawPosition,
                    rawRotation,
                    SNAP_DISTANCE,
                    out Vector3 candidatePosition,
                    out float travel) ||
                travel >= bestTravel)
            {
                continue;
            }

            bestTravel = travel;
            snappedPosition = candidatePosition;
            targetId = target.InstanceId;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// 保持零件当前朝向，沿源到目标的三维中心连线移动，
    /// 通过凸碰撞代理二分寻找首次表面接触点。结果向外保留少量安全间隙，
    /// 避免接触面的浮点误差被后续穿模校验误判。
    /// </summary>
    private bool TryGetLineSnapPosition(
        MPThreeDPart source,
        MPThreeDPart target,
        Vector3 rawPosition,
        Quaternion rawRotation,
        float maxTravel,
        out Vector3 snappedPosition,
        out float contactTravel)
    {
        snappedPosition = rawPosition;
        contactTravel = float.MaxValue;
        if (source == null ||
            target == null ||
            source == target ||
            source.CollisionProxy == null ||
            target.CollisionProxy == null)
        {
            return false;
        }

        Vector3 toTarget = target.transform.localPosition - rawPosition;
        float centerDistance = toTarget.magnitude;
        if (centerDistance <= 0.0001f)
        {
            if (TryGetConnectionDistanceAtPose(
                    source,
                    target,
                    rawPosition,
                    rawRotation,
                    out _))
            {
                contactTravel = 0f;
                return true;
            }

            return false;
        }

        float allowedTravel = Mathf.Min(Mathf.Max(0f, maxTravel), centerDistance);
        if (allowedTravel <= 0.0001f)
        {
            return false;
        }

        Vector3 direction = toTarget / centerDistance;
        if (!TryGetFirstContactTravel(
                source,
                target,
                rawPosition,
                rawRotation,
                direction,
                allowedTravel,
                out contactTravel) ||
            contactTravel > allowedTravel + 0.0001f)
        {
            return false;
        }

        float safeTravel = Mathf.Max(0f, contactTravel - LINE_SNAP_SKIN);
        snappedPosition = rawPosition + direction * safeTravel;
        return true;
    }

    private bool TryGetFirstContactTravel(
        MPThreeDPart source,
        MPThreeDPart target,
        Vector3 sourceLocalPosition,
        Quaternion sourceLocalRotation,
        Vector3 localDirection,
        float maxTravel,
        out float contactTravel)
    {
        contactTravel = float.MaxValue;
        if (source == null ||
            target == null ||
            localDirection.sqrMagnitude <= 0.000001f ||
            maxTravel <= 0f)
        {
            return false;
        }

        Vector3 direction = localDirection.normalized;
        float probeTravel = maxTravel + CONTACT_PROBE_DISTANCE;
        if (!TryGetSweptAabbInterval(
                source,
                target,
                sourceLocalPosition,
                sourceLocalRotation,
                direction,
                probeTravel,
                out float intervalEnter,
                out float intervalExit))
        {
            return false;
        }

        if (intervalEnter <= 0.0001f &&
            TryGetPenetrationAtPose(
                source,
                sourceLocalPosition,
                sourceLocalRotation,
                target,
                out float startDepth))
        {
            if (startDepth <= PENETRATION_TOLERANCE)
            {
                contactTravel = 0f;
                return true;
            }

            return false;
        }

        float scanStart = Mathf.Max(
            0f,
            intervalEnter - CONTACT_PROBE_DISTANCE);
        float scanEnd = Mathf.Min(
            probeTravel,
            intervalExit + CONTACT_PROBE_DISTANCE);
        if (scanEnd <= scanStart)
        {
            return false;
        }

        float low = scanStart;
        float high = scanEnd;
        Vector3 endPosition =
            sourceLocalPosition + direction * scanEnd;
        bool endOverlaps = TryGetPenetrationAtPose(
            source,
            endPosition,
            sourceLocalRotation,
            target,
            out float endDepth) &&
            endDepth > CONTACT_QUERY_TOLERANCE;
        if (!endOverlaps)
        {
            Vector3 sourceSize = source.Definition.Size;
            Vector3 targetSize = target.Definition.Size;
            float sourceMinimum = Mathf.Min(
                sourceSize.x,
                Mathf.Min(sourceSize.y, sourceSize.z));
            float targetMinimum = Mathf.Min(
                targetSize.x,
                Mathf.Min(targetSize.y, targetSize.z));
            float minimumFeature = Mathf.Min(sourceMinimum, targetMinimum);
            float scanStep = Mathf.Clamp(
                minimumFeature * 0.1f,
                0.01f,
                MAX_CONTACT_SCAN_STEP);
            float scanLength = scanEnd - scanStart;
            int scanSteps = Mathf.Clamp(
                Mathf.CeilToInt(scanLength / scanStep),
                1,
                MAX_CONTACT_SCAN_STEPS);
            float previousTravel = scanStart;
            bool foundInterval = false;
            for (int step = 1; step <= scanSteps; step++)
            {
                float sampleTravel =
                    scanStart + scanLength * step / scanSteps;
                Vector3 samplePosition =
                    sourceLocalPosition + direction * sampleTravel;
                if (TryGetPenetrationAtPose(
                        source,
                        samplePosition,
                        sourceLocalRotation,
                        target,
                        out float sampleDepth) &&
                    sampleDepth > CONTACT_QUERY_TOLERANCE)
                {
                    low = previousTravel;
                    high = sampleTravel;
                    foundInterval = true;
                    break;
                }

                previousTravel = sampleTravel;
            }

            if (!foundInterval)
            {
                return false;
            }
        }

        for (int i = 0; i < CONTACT_BINARY_SEARCH_ITERATIONS; i++)
        {
            float middle = (low + high) * 0.5f;
            Vector3 candidatePosition =
                sourceLocalPosition + direction * middle;
            if (TryGetPenetrationAtPose(
                    source,
                    candidatePosition,
                    sourceLocalRotation,
                    target,
                    out float depth) &&
                depth > CONTACT_QUERY_TOLERANCE)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        contactTravel = high;
        return true;
    }

    /// <summary>
    /// 用源零件的旋转后 AABB 扩张目标 AABB，再做线段 slab 求交。
    /// 返回的区间是窄相检测的保守候选范围，可避免长距离三击时扫描整条路径。
    /// </summary>
    private static bool TryGetSweptAabbInterval(
        MPThreeDPart source,
        MPThreeDPart target,
        Vector3 sourceLocalPosition,
        Quaternion sourceLocalRotation,
        Vector3 direction,
        float maxTravel,
        out float enter,
        out float exit)
    {
        enter = 0f;
        exit = Mathf.Max(0f, maxTravel);
        if (source == null ||
            target == null ||
            exit <= 0f ||
            direction.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Bounds sourceBounds = CalculatePartBoundsAtPose(
            source,
            sourceLocalPosition,
            sourceLocalRotation);
        Bounds targetBounds = CalculatePartBoundsAtPose(
            target,
            target.transform.localPosition,
            target.transform.localRotation);
        Vector3 padding = sourceBounds.extents +
            Vector3.one * CONTACT_PROBE_DISTANCE;
        Vector3 expandedMin = targetBounds.min - padding;
        Vector3 expandedMax = targetBounds.max + padding;
        Vector3 normalizedDirection = direction.normalized;
        return IntersectSweptSlab(
                   sourceLocalPosition.x,
                   normalizedDirection.x,
                   expandedMin.x,
                   expandedMax.x,
                   ref enter,
                   ref exit) &&
               IntersectSweptSlab(
                   sourceLocalPosition.y,
                   normalizedDirection.y,
                   expandedMin.y,
                   expandedMax.y,
                   ref enter,
                   ref exit) &&
               IntersectSweptSlab(
                   sourceLocalPosition.z,
                   normalizedDirection.z,
                   expandedMin.z,
                   expandedMax.z,
                   ref enter,
                   ref exit) &&
               enter <= exit;
    }

    private static bool IntersectSweptSlab(
        float origin,
        float direction,
        float minimum,
        float maximum,
        ref float enter,
        ref float exit)
    {
        if (Mathf.Abs(direction) <= 0.000001f)
        {
            return origin >= minimum && origin <= maximum;
        }

        float first = (minimum - origin) / direction;
        float second = (maximum - origin) / direction;
        if (first > second)
        {
            float swap = first;
            first = second;
            second = swap;
        }

        enter = Mathf.Max(enter, first);
        exit = Mathf.Min(exit, second);
        return enter <= exit;
    }

    private bool TryGetPenetrationAtPose(
        MPThreeDPart source,
        Vector3 sourceLocalPosition,
        Quaternion sourceLocalRotation,
        MPThreeDPart target,
        out float depth)
    {
        depth = 0f;
        if (m_worldRoot == null ||
            source == null ||
            target == null ||
            source.CollisionProxy == null ||
            target.CollisionProxy == null)
        {
            return false;
        }

        Transform sourceTransform = source.transform;
        Transform sourceColliderTransform = source.CollisionProxy.transform;
        Vector3 colliderLocalPosition = sourceTransform.InverseTransformPoint(
            sourceColliderTransform.position);
        Quaternion colliderLocalRotation =
            Quaternion.Inverse(sourceTransform.rotation) *
            sourceColliderTransform.rotation;
        Quaternion sourceWorldRotation =
            m_worldRoot.transform.rotation * sourceLocalRotation;
        Vector3 sourceWorldPosition =
            m_worldRoot.transform.TransformPoint(sourceLocalPosition) +
            sourceWorldRotation * colliderLocalPosition;
        Quaternion colliderWorldRotation =
            sourceWorldRotation * colliderLocalRotation;

        return Physics.ComputePenetration(
            source.CollisionProxy,
            sourceWorldPosition,
            colliderWorldRotation,
            target.CollisionProxy,
            target.CollisionProxy.transform.position,
            target.CollisionProxy.transform.rotation,
            out _,
            out depth);
    }

    private void ValidatePreview()
    {
        bool snapped = false;
        string targetId = null;
        if (m_previewPart != null)
        {
            snapped = TryResolveCurrentConnection(out targetId);
        }

        ValidatePreview(snapped, targetId);
    }

    private void ValidatePreview(bool snapped, string targetId)
    {
        if (m_previewPart == null)
        {
            SetIdleValidation(string.Empty);
            return;
        }

        ClearConflictHighlight();
        Bounds previewBounds = CalculatePartBounds(m_previewPart);
        if (!IsInsideBuildBounds(previewBounds))
        {
            SetValidation(new MPThreeDValidationResult(
                MPThreeDPlacementState.OutOfBounds,
                false,
                snapped,
                "Out of bounds - release to place with warning",
                targetId));
            return;
        }

        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart other = m_parts[i];
            if (other == null ||
                other == m_editingOriginal ||
                other.CollisionProxy == null ||
                !other.CollisionProxy.enabled)
            {
                continue;
            }

            if (!previewBounds.Intersects(CalculatePartBounds(other)))
            {
                continue;
            }

            if (TryGetPenetration(m_previewPart, other, out _, out float depth) &&
                depth > PENETRATION_TOLERANCE)
            {
                other.SetConflictHighlight(true);
                m_highlightedConflict = other;
                SetValidation(new MPThreeDValidationResult(
                    MPThreeDPlacementState.Collision,
                    false,
                    snapped,
                    $"Overlaps {other.Definition.DisplayName} - release to place with warning",
                    targetId,
                    other));
                return;
            }
        }

        MPThreeDPart connectionTarget = FindPart(targetId);
        bool connectedToRoot =
            snapped &&
            connectionTarget != null &&
            IsConnectedToRoot(connectionTarget);
        if (!connectedToRoot)
        {
            SetValidation(new MPThreeDValidationResult(
                MPThreeDPlacementState.NoConnection,
                false,
                snapped,
                "Disconnected from base - release to place with warning",
                null));
            return;
        }

        SetValidation(new MPThreeDValidationResult(
            snapped ? MPThreeDPlacementState.SnappedValid : MPThreeDPlacementState.FreeValid,
            true,
            snapped,
            snapped ? "Connected - release to place" : "Release to place",
            targetId));
    }

    private bool TryResolveCurrentConnection(out string targetId)
    {
        return TryResolveConnection(m_previewPart, m_editingOriginal, out targetId);
    }

    private bool TryResolveConnection(
        MPThreeDPart source,
        MPThreeDPart ignoredPart,
        out string targetId)
    {
        return TryResolveConnection(
            source,
            ignoredPart,
            null,
            out targetId);
    }

    private bool TryResolveConnection(
        MPThreeDPart source,
        MPThreeDPart ignoredPart,
        HashSet<string> excludedPartIds,
        out string targetId)
    {
        targetId = null;
        if (source == null)
        {
            return false;
        }

        float bestDistance = float.MaxValue;
        for (int partIndex = 0; partIndex < m_parts.Count; partIndex++)
        {
            MPThreeDPart target = m_parts[partIndex];
            if (target == null ||
                target == source ||
                target == ignoredPart ||
                (excludedPartIds != null &&
                 excludedPartIds.Contains(target.InstanceId)) ||
                WouldCreateConnectionCycle(source, target.InstanceId) ||
                target.CollisionProxy == null ||
                !target.CollisionProxy.enabled)
            {
                continue;
            }

            if (!TryGetConnectionDistance(
                    source,
                    target,
                    out float distance) ||
                distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            targetId = target.InstanceId;
        }

        return targetId != null;
    }

    /// <summary>
    /// 连接只要求两个真实碰撞表面在容差内可接触，不再要求六个面中心重合。
    /// 这样沿中心连线吸附后即使接触点偏离目标中心，也能保持逻辑连接。
    /// </summary>
    private bool TryGetConnectionDistance(
        MPThreeDPart source,
        MPThreeDPart target,
        out float distance)
    {
        if (source == null)
        {
            distance = float.MaxValue;
            return false;
        }

        return TryGetConnectionDistanceAtPose(
            source,
            target,
            source.transform.localPosition,
            source.transform.localRotation,
            out distance);
    }

    private bool TryGetConnectionDistanceAtPose(
        MPThreeDPart source,
        MPThreeDPart target,
        Vector3 sourceLocalPosition,
        Quaternion sourceLocalRotation,
        out float distance)
    {
        distance = float.MaxValue;
        if (source == null ||
            target == null ||
            source == target ||
            source.CollisionProxy == null ||
            target.CollisionProxy == null)
        {
            return false;
        }

        Vector3 centerDelta =
            target.transform.localPosition - sourceLocalPosition;
        float centerDistance = centerDelta.magnitude;
        if (centerDistance <= 0.0001f)
        {
            bool overlaps = TryGetPenetrationAtPose(
                source,
                sourceLocalPosition,
                sourceLocalRotation,
                target,
                out float overlapDepth);
            if (!overlaps || overlapDepth > PENETRATION_TOLERANCE)
            {
                return false;
            }

            distance = 0f;
            return true;
        }

        float connectionTravel = Mathf.Min(
            centerDistance,
            SOCKET_CONNECTION_TOLERANCE + LINE_SNAP_SKIN);
        if (!TryGetFirstContactTravel(
                source,
                target,
                sourceLocalPosition,
                sourceLocalRotation,
                centerDelta,
                connectionTravel,
                out distance))
        {
            return false;
        }

        return distance <= SOCKET_CONNECTION_TOLERANCE + LINE_SNAP_SKIN;
    }

    private static bool TryGetPenetration(
        MPThreeDPart a,
        MPThreeDPart b,
        out Vector3 direction,
        out float depth)
    {
        direction = Vector3.zero;
        depth = 0f;
        if (a == null || b == null || a.CollisionProxy == null || b.CollisionProxy == null)
        {
            return false;
        }

        return Physics.ComputePenetration(
            a.CollisionProxy,
            a.CollisionProxy.transform.position,
            a.CollisionProxy.transform.rotation,
            b.CollisionProxy,
            b.CollisionProxy.transform.position,
            b.CollisionProxy.transform.rotation,
            out direction,
            out depth);
    }

    private Bounds CalculatePartBounds(MPThreeDPart part)
    {
        if (part == null || part.Definition == null)
        {
            return new Bounds();
        }

        return CalculatePartBoundsAtPose(
            part,
            part.transform.localPosition,
            part.transform.localRotation);
    }

    private static Bounds CalculatePartBoundsAtPose(
        MPThreeDPart part,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        if (part == null || part.Definition == null)
        {
            return new Bounds();
        }

        Vector3 extents = GetShapeAabbExtents(
            part.Definition.Shape,
            part.Definition.Size,
            localRotation);
        return new Bounds(localPosition, extents * 2f);
    }

    private bool IsInsideBuildBounds(Bounds bounds)
    {
        const float tolerance = 0.001f;
        return bounds.min.x >= m_buildBounds.min.x - tolerance &&
               bounds.max.x <= m_buildBounds.max.x + tolerance &&
               bounds.min.y >= m_buildBounds.min.y - tolerance &&
               bounds.max.y <= m_buildBounds.max.y + tolerance &&
               bounds.min.z >= m_buildBounds.min.z - tolerance &&
               bounds.max.z <= m_buildBounds.max.z + tolerance;
    }

    private bool IsConnectedToRoot(MPThreeDPart part)
    {
        if (part == null || part.IsRoot)
        {
            return true;
        }

        HashSet<string> visited = new HashSet<string>();
        MPThreeDPart current = part;
        while (current != null && !current.IsRoot)
        {
            if (string.IsNullOrEmpty(current.ConnectedToInstanceId) ||
                !visited.Add(current.InstanceId))
            {
                return false;
            }

            MPThreeDPart connectionTarget = FindPart(current.ConnectedToInstanceId);
            if (connectionTarget == null ||
                !ArePartsSocketConnected(current, connectionTarget))
            {
                return false;
            }

            current = connectionTarget;
        }

        return current != null && current.IsRoot;
    }

    private bool ArePartsSocketConnected(MPThreeDPart source, MPThreeDPart target)
    {
        return TryGetConnectionDistance(source, target, out _);
    }

    /// <summary>
    /// 警告外观由当前姿态和连接关系派生，不写入 DTO。
    /// 仅在提交、载入和结构检查时执行，避免在 Update 中做两两碰撞检测。
    /// </summary>
    private void RefreshPlacedWarnings()
    {
        // 编辑预览期间原零件会临时隐藏，此时重算会丢失它原有的碰撞状态。
        if (m_previewPart != null)
        {
            return;
        }

        // 项目关闭了 autoSyncTransforms；提交后先同步一次，
        // 再做权威碰撞和连接有效性检查，避免读取旧物理姿态。
        Physics.SyncTransforms();
        HashSet<string> collidingPartIds = new HashSet<string>();
        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart a = m_parts[i];
            if (a == null || a.CollisionProxy == null || !a.CollisionProxy.enabled)
            {
                continue;
            }

            Bounds boundsA = CalculatePartBounds(a);
            for (int j = i + 1; j < m_parts.Count; j++)
            {
                MPThreeDPart b = m_parts[j];
                if (b == null || b.CollisionProxy == null || !b.CollisionProxy.enabled ||
                    !boundsA.Intersects(CalculatePartBounds(b)))
                {
                    continue;
                }

                if (!TryGetPenetration(a, b, out _, out float depth) ||
                    depth <= PENETRATION_TOLERANCE)
                {
                    continue;
                }

                collidingPartIds.Add(a.InstanceId);
                collidingPartIds.Add(b.InstanceId);
            }
        }

        // 每个零件只写一次最终警告状态，旧碰撞标记会被当前姿态完整覆盖。
        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart part = m_parts[i];
            if (part == null || part.IsRoot)
            {
                continue;
            }

            bool hasWarning =
                !IsInsideBuildBounds(CalculatePartBounds(part)) ||
                !IsConnectedToRoot(part) ||
                collidingPartIds.Contains(part.InstanceId);
            part.SetPlacementWarning(hasWarning);
        }
    }

    private MPThreeDPart FindPart(string instanceId)
    {
        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart part = m_parts[i];
            if (part != null && part.InstanceId == instanceId)
            {
                return part;
            }
        }

        return null;
    }

    private MPThreeDPart GetRootPart()
    {
        for (int i = 0; i < m_parts.Count; i++)
        {
            if (m_parts[i] != null && m_parts[i].IsRoot)
            {
                return m_parts[i];
            }
        }

        return null;
    }

    private void ApplyState(MPThreeDAssemblySaveDto source, bool resetHistory)
    {
        if (m_pointerManipulatesPart && m_movingPart != null)
        {
            EndPlacedMove(false);
        }

        ClearPersistentSelectionInternal(false);
        ClearAttachmentSourceInternal(false);
        CancelPreviewInternal(false);
        ClearConflictHighlight();
        ClearPlacedParts();

        MPThreeDAssemblySaveDto data = source ?? MPThreeDAssemblySaveDto.CreateEmpty();
        m_title = string.IsNullOrEmpty(data.title) ? "My 3D Build" : data.title;
        RequireConnection = data.requireConnection;
        GridVisible = data.gridVisible;
        if (m_gridObject != null)
        {
            m_gridObject.SetActive(GridVisible);
        }

        // 根零件不信任存档姿态，始终使用目录定义的固定原点姿态重建。
        m_loadIds.Clear();
        m_loadIds.Add(ROOT_INSTANCE_ID);
        CreateDefaultRoot();
        int loadedNonRootParts = 0;
        if (data.placedParts != null)
        {
            for (int i = 0;
                 i < data.placedParts.Count &&
                 loadedNonRootParts < MAX_LOADED_NON_ROOT_PARTS;
                 i++)
            {
                MPThreeDPlacedPartDto dto = data.placedParts[i];
                if (dto == null ||
                    string.IsNullOrEmpty(dto.instanceId) ||
                    !MPThreeDPartCatalog.TryGet(dto.partId, out MPThreeDPartDefinition definition))
                {
                    continue;
                }

                bool isRoot = definition.Id == MPThreeDPartCatalog.RootPartId;
                if (isRoot || !m_loadIds.Add(dto.instanceId))
                {
                    continue;
                }

                MPThreeDPart part = MPThreeDPart.Create(
                    m_partsRoot,
                    definition,
                    dto.instanceId,
                    false,
                    false,
                    m_placedMaterial,
                    m_previewMaterial,
                    m_selectionOutlineMaterial,
                    GetSelectionOutlineMesh(definition.Shape),
                    GetCollisionMesh(definition.Shape));
                Vector3 position = IsFinite(dto.GetPosition())
                    ? dto.GetPosition()
                    : Vector3.zero;
                Quaternion rotation = dto.GetRotation();
                part.SetLocalPose(position, rotation);
                part.SetConnection(dto.connectedToInstanceId);
                m_parts.Add(part);
                loadedNonRootParts++;
            }
        }

        // ConnectedTo 表示用户建立的逻辑父子关系。加载时只清理
        // 自连接和丢失目标；暂时分离/穿模交给派生警告判定，
        // 这样零件恢复原姿态时可以沿原拓扑自动恢复有效。
        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart part = m_parts[i];
            if (part == null || part.IsRoot)
            {
                continue;
            }

            MPThreeDPart connectionTarget = FindPart(part.ConnectedToInstanceId);
            if (part.ConnectedToInstanceId == part.InstanceId ||
                connectionTarget == null)
            {
                part.SetConnection(null);
            }
        }

        BreakConnectionCycles();

        RefreshPlacedWarnings();

        if (resetHistory)
        {
            if (m_history == null)
            {
                m_history = new MPThreeDCommandHistory(50);
            }

            m_history.Reset(CaptureState());
        }
    }

    private void BreakConnectionCycles()
    {
        HashSet<string> chain = new HashSet<string>();
        for (int i = 0; i < m_parts.Count; i++)
        {
            MPThreeDPart current = m_parts[i];
            if (current == null || current.IsRoot)
            {
                continue;
            }

            chain.Clear();
            MPThreeDPart previous = null;
            while (current != null && !current.IsRoot)
            {
                if (!chain.Add(current.InstanceId))
                {
                    if (previous != null)
                    {
                        previous.SetConnection(null);
                    }

                    break;
                }

                previous = current;
                if (string.IsNullOrEmpty(current.ConnectedToInstanceId))
                {
                    break;
                }

                current = FindPart(current.ConnectedToInstanceId);
            }
        }
    }

    private void ClearPlacedParts()
    {
        for (int i = 0; i < m_parts.Count; i++)
        {
            if (m_parts[i] != null)
            {
                DestroyRuntimeObject(m_parts[i].gameObject);
            }
        }

        m_parts.Clear();
    }

    private void ClearPersistentSelectionInternal(bool publish)
    {
        bool hadSelection = m_persistentSelectedPart != null;
        if (m_pointerRotatesPersistentPart)
        {
            EndPersistentRotation(false);
        }

        if (m_persistentSelectedPart != null)
        {
            m_persistentSelectedPart.SetSelectionOutline(false);
        }

        m_persistentSelectedPart = null;
        m_pointerRotatesPersistentPart = false;
        m_persistentRotationMoved = false;
        m_persistentRotationAxisLocked = false;
        if (publish && hadSelection)
        {
            SetIdleValidation("Selection cleared - drag to orbit");
        }
    }

    private void ClearAttachmentSourceInternal(bool publish)
    {
        bool hadSource = m_attachmentSourcePart != null;
        if (m_attachmentSourcePart != null)
        {
            m_attachmentSourcePart.SetAttachmentOutline(false);
        }

        m_attachmentSourcePart = null;
        if (publish && hadSource)
        {
            SetIdleValidation("Attach selection cancelled");
        }
    }

    private void CancelPreviewInternal(bool publish)
    {
        if (m_editingOriginal != null)
        {
            m_editingOriginal.SetRuntimeVisible(true);
        }

        if (m_previewPart != null)
        {
            DestroyRuntimeObject(m_previewPart.gameObject);
        }

        m_previewPart = null;
        m_editingOriginal = null;
        m_previewSnapTargetId = null;
        m_pointerManipulatesPart = false;
        m_pointerPartDragDistance = 0f;
        m_pointerMovedPart = false;
        m_previewDragOffset = Vector3.zero;
        ClearConflictHighlight();
        if (publish)
        {
            SetIdleValidation("Cancelled");
        }
    }

    private void CommitCurrentState(
        string message,
        bool recordHistory,
        MPThreeDPart statusPart = null)
    {
        RefreshPlacedWarnings();
        MPThreeDPart warningPart = statusPart ?? m_persistentSelectedPart;
        string warningSummary = GetPartWarningSummary(warningPart);
        if (!string.IsNullOrEmpty(warningSummary))
        {
            message = $"{message} - Warning: {warningSummary}";
        }

        MPThreeDAssemblySaveDto state = CaptureState();
        if (recordHistory)
        {
            if (m_history == null)
            {
                m_history = new MPThreeDCommandHistory(50);
                m_history.Reset(state);
            }
            else
            {
                m_history.Record(state);
            }
        }

        if (m_previewPart != null)
        {
            // 网格/模式切换可以发生在预览事务中，不能清掉当前合法性结果。
            PublishMessage(message);
        }
        else
        {
            SetIdleValidation(message);
        }

        StateCommitted?.Invoke();
    }

    private string GetPartWarningSummary(MPThreeDPart part)
    {
        if (part == null || part.IsRoot)
        {
            return string.Empty;
        }

        string summary = string.Empty;
        if (!IsInsideBuildBounds(CalculatePartBounds(part)))
        {
            summary = "out of bounds";
        }

        if (!IsConnectedToRoot(part))
        {
            summary = string.IsNullOrEmpty(summary)
                ? "disconnected"
                : $"{summary}, disconnected";
        }

        if (HasCollision(part))
        {
            summary = string.IsNullOrEmpty(summary)
                ? "overlap"
                : $"{summary}, overlap";
        }

        return summary;
    }

    private void SetValidation(MPThreeDValidationResult result, bool updatePreview = true)
    {
        m_validation = result;
        if (updatePreview && m_previewPart != null && result != null)
        {
            m_previewPart.SetPreviewState(result.State);
        }

        ValidationChanged?.Invoke(result);
        if (result != null)
        {
            MessageChanged?.Invoke(result.Message);
        }
    }

    private void SetIdleValidation(string message)
    {
        ClearConflictHighlight();
        SetValidation(new MPThreeDValidationResult(
            MPThreeDPlacementState.Unknown,
            false,
            false,
            message ?? string.Empty),
            false);
    }

    private void PublishMessage(string message)
    {
        MessageChanged?.Invoke(message ?? string.Empty);
    }

    private void ClearConflictHighlight()
    {
        if (m_highlightedConflict != null)
        {
            m_highlightedConflict.SetConflictHighlight(false);
        }

        m_highlightedConflict = null;
    }

    private Vector3 GetPointOnWorkPlane(Vector2 viewportPosition, float planeHeight)
    {
        Ray ray = ViewportRay(viewportPosition);
        Vector3 planeWorldPoint = m_worldRoot.transform.TransformPoint(
            new Vector3(0f, planeHeight, 0f));
        Plane plane = new Plane(m_worldRoot.transform.up, planeWorldPoint);
        if (plane.Raycast(ray, out float distance))
        {
            return m_worldRoot.transform.InverseTransformPoint(ray.GetPoint(distance));
        }

        return new Vector3(m_cameraTarget.x, planeHeight, m_cameraTarget.z);
    }

    private bool TryGetPointOnMoveDragPlane(
        Vector2 viewportPosition,
        out Vector3 localPoint)
    {
        localPoint = m_moveStartPosition;
        if (m_worldRoot == null || m_camera == null)
        {
            return false;
        }

        Ray ray = ViewportRay(viewportPosition);
        if (!m_moveDragPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        localPoint = m_worldRoot.transform.InverseTransformPoint(
            ray.GetPoint(distance));
        return true;
    }

    private MoveAxisPlane GetCameraFacingMoveAxisPlane()
    {
        Vector3 localForward = m_worldRoot.transform.InverseTransformDirection(
            m_camera.transform.forward).normalized;
        float absX = Mathf.Abs(localForward.x);
        float absY = Mathf.Abs(localForward.y);
        float absZ = Mathf.Abs(localForward.z);
        if (absY >= absX && absY >= absZ)
        {
            // 俯视或仰视：沿地面的 XZ 平面移动。
            return MoveAxisPlane.XZ;
        }

        if (absX >= absZ)
        {
            // 从世界 X 轴方向侧视：沿 YZ 平面移动。
            return MoveAxisPlane.YZ;
        }

        // 从世界 Z 轴方向正视：沿 XY 平面移动。
        return MoveAxisPlane.XY;
    }

    private static Vector3 GetMoveAxisPlaneNormal(MoveAxisPlane movePlane)
    {
        switch (movePlane)
        {
            case MoveAxisPlane.XZ:
                return Vector3.up;

            case MoveAxisPlane.YZ:
                return Vector3.right;

            default:
                return Vector3.forward;
        }
    }

    private Vector3 ConstrainToMoveAxisPlane(Vector3 position)
    {
        switch (m_moveAxisPlane)
        {
            case MoveAxisPlane.XZ:
                position.y = m_moveStartPosition.y;
                break;

            case MoveAxisPlane.YZ:
                position.x = m_moveStartPosition.x;
                break;

            default:
                position.z = m_moveStartPosition.z;
                break;
        }

        return position;
    }

    private Ray ViewportRay(Vector2 viewportPosition)
    {
        return m_camera.ViewportPointToRay(new Vector3(
            Mathf.Clamp01(viewportPosition.x),
            Mathf.Clamp01(viewportPosition.y),
            0f));
    }

    private void UpdateCameraTransform()
    {
        if (m_camera == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(m_cameraPitch, m_cameraYaw, 0f);
        m_camera.transform.localRotation = rotation;
        m_camera.transform.localPosition =
            m_cameraTarget - rotation * Vector3.forward * m_cameraDistance;
    }

    private bool EnsureInitialized()
    {
        if (m_initialized && !m_isShuttingDown && m_worldRoot != null && m_camera != null)
        {
            return true;
        }

        Debug.LogWarning("[MPThreeD] WorldController is not initialized or already released.");
        return false;
    }

    private static Vector2 ClampViewport(Vector2 value)
    {
        return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
