using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D 模块运行时零件。视觉使用 Unity Primitive；方体使用 BoxCollider，
/// 圆柱和椭球使用与可见形状一致的低模凸 MeshCollider。
/// 正式零件不运行 Update，所有交互由 MPThreeDWorldController 集中驱动。
/// </summary>
public sealed class MPThreeDPart : MonoBehaviour
{
    private static readonly Vector3[] s_socketDirections =
    {
        Vector3.right,
        Vector3.left,
        Vector3.up,
        Vector3.down,
        Vector3.forward,
        Vector3.back
    };

    private MaterialPropertyBlock m_propertyBlock;
    private MaterialPropertyBlock m_outlinePropertyBlock;

    private Renderer m_visualRenderer;
    private Renderer m_selectionOutlineRenderer;
    private Material m_placedMaterial;
    private Material m_previewMaterial;
    private MPThreeDSocket[] m_sockets;
    private bool m_isConflictHighlighted;
    private bool m_hasPlacementWarning;
    private bool m_runtimeVisible = true;
    private bool m_selectionOutlineVisible;
    private bool m_attachmentOutlineVisible;

    private static readonly Color s_operationOutlineColor =
        new Color(1f, 0.78f, 0.12f, 1f);
    private static readonly Color s_attachmentOutlineColor =
        new Color(0.16f, 0.58f, 1f, 1f);

    public string InstanceId { get; private set; }
    public string ConnectedToInstanceId { get; private set; }
    public MPThreeDPartDefinition Definition { get; private set; }
    public Collider CollisionProxy { get; private set; }
    public bool IsRoot { get; private set; }
    public bool IsPreview { get; private set; }

    public static MPThreeDPart Create(
        Transform parent,
        MPThreeDPartDefinition definition,
        string instanceId,
        bool isRoot,
        bool isPreview,
        Material placedMaterial,
        Material previewMaterial,
        Material selectionOutlineMaterial,
        Mesh selectionOutlineMesh,
        Mesh collisionMesh)
    {
        GameObject root = new GameObject(isPreview ? $"Preview_{definition.Id}" : $"Part_{instanceId}");
        root.transform.SetParent(parent, false);

        MPThreeDPart part = root.AddComponent<MPThreeDPart>();
        part.Initialize(
            definition,
            instanceId,
            isRoot,
            isPreview,
            placedMaterial,
            previewMaterial,
            selectionOutlineMaterial,
            selectionOutlineMesh,
            collisionMesh);
        return part;
    }

    private void Initialize(
        MPThreeDPartDefinition definition,
        string instanceId,
        bool isRoot,
        bool isPreview,
        Material placedMaterial,
        Material previewMaterial,
        Material selectionOutlineMaterial,
        Mesh selectionOutlineMesh,
        Mesh collisionMesh)
    {
        // MaterialPropertyBlock 会在内部创建 Unity 原生对象，不能在
        // MonoBehaviour 的字段初始化器中构造，必须等 AddComponent 完成后再创建。
        m_propertyBlock = new MaterialPropertyBlock();

        Definition = definition;
        InstanceId = instanceId;
        IsRoot = isRoot;
        IsPreview = isPreview;
        m_placedMaterial = placedMaterial;
        m_previewMaterial = previewMaterial;

        m_sockets = CreateSockets(definition.Size);

        GameObject visual = GameObject.CreatePrimitive(ToPrimitiveType(definition.Shape));
        visual.name = "Visual";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = definition.GetVisualScale();

        // 显式引用 CreatePrimitive 依赖的具体组件类型，避免 IL2CPP Engine
        // Stripping 只看到基类后裁掉 Player 运行时所需类型。
        MeshFilter meshFilter = visual.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = visual.GetComponent<MeshRenderer>();
        Collider primitiveCollider = GetPrimitiveCollider(visual, definition.Shape);
        if (primitiveCollider != null)
        {
            primitiveCollider.enabled = false;
            Destroy(primitiveCollider);
        }

        if (definition.Shape == MPThreeDPartShape.Cube || collisionMesh == null)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.center = Vector3.zero;
            boxCollider.size = definition.Size;
            CollisionProxy = boxCollider;
        }
        else
        {
            MeshCollider meshCollider = visual.AddComponent<MeshCollider>();
            meshCollider.convex = true;
            meshCollider.sharedMesh = collisionMesh;
            CollisionProxy = meshCollider;
        }

        if (meshFilter == null || meshRenderer == null)
        {
            Debug.LogError($"[MPThreeD] Primitive {definition.Id} is missing render components.");
        }

        m_visualRenderer = meshRenderer;
        if (m_visualRenderer != null)
        {
            m_visualRenderer.sharedMaterial = isPreview ? m_previewMaterial : m_placedMaterial;
        }

        CreateSelectionOutline(
            definition,
            selectionOutlineMaterial,
            selectionOutlineMesh);

        SetLayerRecursively(gameObject, isPreview ? 2 : 0);
        ApplyColor(isPreview ? WithAlpha(definition.Color, 0.58f) : definition.Color);
    }

    public void SetIdentity(string instanceId, string connectedToInstanceId)
    {
        InstanceId = instanceId;
        ConnectedToInstanceId = connectedToInstanceId;
        gameObject.name = IsPreview ? $"Preview_{Definition.Id}" : $"Part_{instanceId}";
    }

    public void SetConnection(string connectedToInstanceId)
    {
        ConnectedToInstanceId = connectedToInstanceId;
    }

    public void SetLocalPose(Vector3 localPosition, Quaternion localRotation)
    {
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
    }

    public void SetRuntimeVisible(bool visible)
    {
        m_runtimeVisible = visible;
        if (m_visualRenderer != null)
        {
            m_visualRenderer.enabled = visible;
        }

        RefreshSelectionOutline();

        if (CollisionProxy != null)
        {
            CollisionProxy.enabled = visible;
        }
    }

    /// <summary>
    /// 选中描边与放置警告外观相互独立，长按预览和双击持续选择均可复用。
    /// </summary>
    public void SetSelectionOutline(bool visible)
    {
        m_selectionOutlineVisible = visible;
        RefreshSelectionOutline();
    }

    /// <summary>
    /// 三击选择的待吸附源使用蓝色描边。它与普通操作描边分开保存，
    /// 因此清除双击/长按操作状态不会误清掉待吸附状态。
    /// </summary>
    public void SetAttachmentOutline(bool visible)
    {
        m_attachmentOutlineVisible = visible;
        RefreshSelectionOutline();
    }

    public void SetPreviewState(MPThreeDPlacementState state)
    {
        if (!IsPreview)
        {
            return;
        }

        Color color;
        switch (state)
        {
            case MPThreeDPlacementState.SnappedValid:
                color = new Color(0.29f, 0.74f, 0.47f, 0.58f);
                break;
            case MPThreeDPlacementState.FreeValid:
                color = new Color(0.30f, 0.55f, 0.86f, 0.58f);
                break;
            case MPThreeDPlacementState.NoConnection:
            case MPThreeDPlacementState.Collision:
            case MPThreeDPlacementState.OutOfBounds:
            case MPThreeDPlacementState.Invalid:
                color = new Color(0.90f, 0.28f, 0.27f, 0.58f);
                break;
            default:
                color = WithAlpha(Definition.Color, 0.48f);
                break;
        }

        ApplyColor(color);
    }

    public void SetConflictHighlight(bool highlighted)
    {
        if (IsPreview || m_isConflictHighlighted == highlighted)
        {
            return;
        }

        m_isConflictHighlighted = highlighted;
        RefreshPlacedAppearance();
    }

    /// <summary>
    /// 正式零件的派生警告状态。不会写入存档，加载或结构变化后由控制器重新计算。
    /// </summary>
    public void SetPlacementWarning(bool hasWarning)
    {
        if (IsPreview || IsRoot || m_hasPlacementWarning == hasWarning)
        {
            return;
        }

        m_hasPlacementWarning = hasWarning;
        RefreshPlacedAppearance();
    }

    public void ConvertPreviewToPlaced(
        string instanceId,
        string connectedToInstanceId)
    {
        IsPreview = false;
        SetSelectionOutline(false);
        SetAttachmentOutline(false);
        SetIdentity(instanceId, connectedToInstanceId);
        SetLayerRecursively(gameObject, 0);
        m_hasPlacementWarning = false;
        m_isConflictHighlighted = false;
        RefreshPlacedAppearance();
    }

    public IReadOnlyList<MPThreeDSocket> GetSockets()
    {
        return m_sockets;
    }

    public Vector3 GetSocketWorldPosition(MPThreeDSocket socket)
    {
        return transform.TransformPoint(socket.LocalPosition);
    }

    public Vector3 GetSocketWorldDirection(MPThreeDSocket socket)
    {
        return transform.TransformDirection(socket.LocalDirection).normalized;
    }

    public MPThreeDPlacedPartDto CaptureDto()
    {
        return MPThreeDPlacedPartDto.Create(
            InstanceId,
            Definition.Id,
            transform.localPosition,
            transform.localRotation,
            ConnectedToInstanceId);
    }

    private void ApplyColor(Color color)
    {
        if (m_visualRenderer == null)
        {
            return;
        }

        // 兼容 Unity 序列化恢复组件或初始化流程尚未完成的情况。
        if (m_propertyBlock == null)
        {
            m_propertyBlock = new MaterialPropertyBlock();
        }

        m_visualRenderer.GetPropertyBlock(m_propertyBlock);
        m_propertyBlock.SetColor("_Color", color);
        m_visualRenderer.SetPropertyBlock(m_propertyBlock);
    }

    private void RefreshPlacedAppearance()
    {
        if (IsPreview || m_visualRenderer == null)
        {
            return;
        }

        bool showWarning = m_hasPlacementWarning || m_isConflictHighlighted;
        m_visualRenderer.sharedMaterial = showWarning
            ? m_previewMaterial
            : m_placedMaterial;
        ApplyColor(showWarning
            ? new Color(0.90f, 0.20f, 0.20f, 0.58f)
            : Definition.Color);
    }

    private void CreateSelectionOutline(
        MPThreeDPartDefinition definition,
        Material selectionOutlineMaterial,
        Mesh selectionOutlineMesh)
    {
        if (definition == null ||
            selectionOutlineMaterial == null ||
            selectionOutlineMesh == null)
        {
            return;
        }

        GameObject outline = new GameObject(
            "SelectionOutline",
            typeof(MeshFilter),
            typeof(MeshRenderer));
        outline.transform.SetParent(transform, false);
        outline.transform.localPosition = Vector3.zero;
        outline.transform.localRotation = Quaternion.identity;

        const float padding = 0.08f;
        Vector3 outlineSize = definition.Size + Vector3.one * padding;
        outline.transform.localScale = definition.Shape == MPThreeDPartShape.Cylinder
            ? new Vector3(outlineSize.x, outlineSize.y * 0.5f, outlineSize.z)
            : outlineSize;

        MeshFilter filter = outline.GetComponent<MeshFilter>();
        filter.sharedMesh = selectionOutlineMesh;

        MeshRenderer renderer = outline.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = selectionOutlineMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.enabled = false;
        m_selectionOutlineRenderer = renderer;
    }

    private void RefreshSelectionOutline()
    {
        if (m_selectionOutlineRenderer == null)
        {
            return;
        }

        bool visible =
            m_runtimeVisible &&
            (m_selectionOutlineVisible || m_attachmentOutlineVisible);
        m_selectionOutlineRenderer.enabled = visible;
        if (!visible)
        {
            return;
        }

        if (m_outlinePropertyBlock == null)
        {
            m_outlinePropertyBlock = new MaterialPropertyBlock();
        }

        m_selectionOutlineRenderer.GetPropertyBlock(m_outlinePropertyBlock);
        m_outlinePropertyBlock.SetColor(
            "_Color",
            m_attachmentOutlineVisible
                ? s_attachmentOutlineColor
                : s_operationOutlineColor);
        m_selectionOutlineRenderer.SetPropertyBlock(m_outlinePropertyBlock);
    }

    private static PrimitiveType ToPrimitiveType(MPThreeDPartShape shape)
    {
        switch (shape)
        {
            case MPThreeDPartShape.Cylinder:
                return PrimitiveType.Cylinder;
            case MPThreeDPartShape.Sphere:
                return PrimitiveType.Sphere;
            default:
                return PrimitiveType.Cube;
        }
    }

    private static Collider GetPrimitiveCollider(
        GameObject visual,
        MPThreeDPartShape shape)
    {
        switch (shape)
        {
            case MPThreeDPartShape.Cylinder:
                return visual.GetComponent<CapsuleCollider>();
            case MPThreeDPartShape.Sphere:
                return visual.GetComponent<SphereCollider>();
            default:
                return visual.GetComponent<BoxCollider>();
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static MPThreeDSocket[] CreateSockets(Vector3 size)
    {
        Vector3 extents = size * 0.5f;
        MPThreeDSocket[] sockets = new MPThreeDSocket[s_socketDirections.Length];
        for (int i = 0; i < s_socketDirections.Length; i++)
        {
            Vector3 direction = s_socketDirections[i];
            sockets[i] = new MPThreeDSocket(
                i,
                Vector3.Scale(direction, extents),
                direction);
        }

        return sockets;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        Transform targetTransform = target.transform;
        for (int i = 0; i < targetTransform.childCount; i++)
        {
            SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
        }
    }
}
