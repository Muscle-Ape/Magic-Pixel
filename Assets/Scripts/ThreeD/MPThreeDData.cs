using System;
using System.Collections.Generic;
using UnityEngine;

public enum MPThreeDPartShape
{
    Cube,
    Cylinder,
    Sphere
}

public enum MPThreeDPlacementState
{
    Unknown,
    Loading,
    SnappedValid,
    FreeValid,
    NoConnection,
    Collision,
    OutOfBounds,
    Invalid
}

/// <summary>
/// 运行时基础零件定义。当前模块使用 Unity Primitive 作为占位资产，
/// 后续替换为 YooAsset Prefab 时可保持 PartId、尺寸和连接器规则不变。
/// </summary>
public sealed class MPThreeDPartDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public MPThreeDPartShape Shape { get; }
    public Vector3 Size { get; }
    public Vector3 DefaultEuler { get; }
    public Color Color { get; }
    public float GridStep { get; }
    public float RotationStep { get; }

    public MPThreeDPartDefinition(
        string id,
        string displayName,
        MPThreeDPartShape shape,
        Vector3 size,
        Vector3 defaultEuler,
        Color color,
        float gridStep = 0.5f,
        float rotationStep = 90f)
    {
        Id = id;
        DisplayName = displayName;
        Shape = shape;
        Size = size;
        DefaultEuler = defaultEuler;
        Color = color;
        GridStep = Mathf.Max(0.05f, gridStep);
        RotationStep = Mathf.Clamp(rotationStep, 1f, 180f);
    }

    public Vector3 GetVisualScale()
    {
        switch (Shape)
        {
            case MPThreeDPartShape.Cylinder:
                // Unity Cylinder 原始高度为 2，X/Z 直径为 1。
                return new Vector3(Size.x, Size.y * 0.5f, Size.z);
            default:
                return Size;
        }
    }
}

public static class MPThreeDPartCatalog
{
    public const string RootPartId = "root_base";

    private static readonly IReadOnlyList<MPThreeDPartDefinition> s_buildableParts =
        new List<MPThreeDPartDefinition>
        {
            new MPThreeDPartDefinition(
                "block", "BLOCK", MPThreeDPartShape.Cube,
                new Vector3(1f, 1f, 1f), Vector3.zero,
                new Color32(255, 142, 100, 255)),
            new MPThreeDPartDefinition(
                "beam", "BEAM", MPThreeDPartShape.Cube,
                new Vector3(3f, 0.5f, 0.5f), Vector3.zero,
                new Color32(84, 194, 196, 255)),
            new MPThreeDPartDefinition(
                "plate", "PLATE", MPThreeDPartShape.Cube,
                new Vector3(2f, 0.25f, 2f), Vector3.zero,
                new Color32(255, 198, 92, 255)),
            new MPThreeDPartDefinition(
                "pillar", "PILLAR", MPThreeDPartShape.Cylinder,
                new Vector3(0.8f, 2f, 0.8f), Vector3.zero,
                new Color32(117, 103, 217, 255)),
            new MPThreeDPartDefinition(
                "wheel", "WHEEL", MPThreeDPartShape.Cylinder,
                new Vector3(1.3f, 0.45f, 1.3f), new Vector3(0f, 0f, 90f),
                new Color32(62, 69, 88, 255)),
            new MPThreeDPartDefinition(
                "connector", "LINK", MPThreeDPartShape.Cube,
                new Vector3(0.5f, 0.5f, 1.5f), Vector3.zero,
                new Color32(80, 162, 224, 255)),
            new MPThreeDPartDefinition(
                "dome", "DOME", MPThreeDPartShape.Sphere,
                new Vector3(1.5f, 0.75f, 1.5f), Vector3.zero,
                new Color32(236, 105, 153, 255)),
            new MPThreeDPartDefinition(
                "antenna", "ANTENNA", MPThreeDPartShape.Cylinder,
                new Vector3(0.3f, 2f, 0.3f), Vector3.zero,
                new Color32(91, 206, 145, 255))
        }.AsReadOnly();

    private static readonly MPThreeDPartDefinition s_rootDefinition =
        new MPThreeDPartDefinition(
            RootPartId,
            "BASE",
            MPThreeDPartShape.Cube,
            new Vector3(3f, 1f, 3f),
            Vector3.zero,
            new Color32(151, 159, 176, 255));

    public static IReadOnlyList<MPThreeDPartDefinition> BuildableParts => s_buildableParts;
    public static MPThreeDPartDefinition RootDefinition => s_rootDefinition;

    public static bool TryGet(string partId, out MPThreeDPartDefinition definition)
    {
        if (partId == RootPartId)
        {
            definition = s_rootDefinition;
            return true;
        }

        for (int i = 0; i < s_buildableParts.Count; i++)
        {
            if (s_buildableParts[i].Id == partId)
            {
                definition = s_buildableParts[i];
                return true;
            }
        }

        definition = null;
        return false;
    }
}

public readonly struct MPThreeDSocket
{
    public readonly int Index;
    public readonly Vector3 LocalPosition;
    public readonly Vector3 LocalDirection;

    public MPThreeDSocket(int index, Vector3 localPosition, Vector3 localDirection)
    {
        Index = index;
        LocalPosition = localPosition;
        LocalDirection = localDirection;
    }
}

public sealed class MPThreeDValidationResult
{
    public MPThreeDPlacementState State { get; }
    public bool CanConfirm { get; }
    public bool IsSnapped { get; }
    public string Message { get; }
    public string ConnectedToInstanceId { get; }
    public MPThreeDPart ConflictPart { get; }

    public MPThreeDValidationResult(
        MPThreeDPlacementState state,
        bool canConfirm,
        bool isSnapped,
        string message,
        string connectedToInstanceId = null,
        MPThreeDPart conflictPart = null)
    {
        State = state;
        CanConfirm = canConfirm;
        IsSnapped = isSnapped;
        Message = message ?? string.Empty;
        ConnectedToInstanceId = connectedToInstanceId;
        ConflictPart = conflictPart;
    }
}

[Serializable]
public sealed class MPThreeDPlacedPartDto
{
    public string instanceId;
    public string partId;
    public float positionX;
    public float positionY;
    public float positionZ;
    public float rotationX;
    public float rotationY;
    public float rotationZ;
    public float rotationW = 1f;
    public string connectedToInstanceId;

    public Vector3 GetPosition()
    {
        return new Vector3(positionX, positionY, positionZ);
    }

    public Quaternion GetRotation()
    {
        Quaternion rotation = new Quaternion(rotationX, rotationY, rotationZ, rotationW);
        float magnitudeSquared = Quaternion.Dot(rotation, rotation);
        if (float.IsNaN(magnitudeSquared) ||
            float.IsInfinity(magnitudeSquared) ||
            magnitudeSquared < 0.01f)
        {
            return Quaternion.identity;
        }

        return rotation.normalized;
    }

    public static MPThreeDPlacedPartDto Create(
        string instanceId,
        string partId,
        Vector3 position,
        Quaternion rotation,
        string connectedToInstanceId)
    {
        return new MPThreeDPlacedPartDto
        {
            instanceId = instanceId,
            partId = partId,
            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,
            rotationX = rotation.x,
            rotationY = rotation.y,
            rotationZ = rotation.z,
            rotationW = rotation.w,
            connectedToInstanceId = connectedToInstanceId
        };
    }

    public MPThreeDPlacedPartDto Clone()
    {
        return Create(instanceId, partId, GetPosition(), GetRotation(), connectedToInstanceId);
    }
}

[Serializable]
public sealed class MPThreeDAssemblySaveDto
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string title = "My 3D Build";
    public bool requireConnection = true;
    public bool gridVisible = true;
    public List<MPThreeDPlacedPartDto> placedParts = new List<MPThreeDPlacedPartDto>();

    public static MPThreeDAssemblySaveDto CreateEmpty()
    {
        return new MPThreeDAssemblySaveDto();
    }

    public MPThreeDAssemblySaveDto Clone()
    {
        MPThreeDAssemblySaveDto clone = new MPThreeDAssemblySaveDto
        {
            schemaVersion = schemaVersion,
            title = title,
            requireConnection = requireConnection,
            gridVisible = gridVisible,
            placedParts = new List<MPThreeDPlacedPartDto>()
        };

        if (placedParts == null)
        {
            return clone;
        }

        for (int i = 0; i < placedParts.Count; i++)
        {
            if (placedParts[i] != null)
            {
                clone.placedParts.Add(placedParts[i].Clone());
            }
        }

        return clone;
    }
}

/// <summary>
/// 独立模块只依赖存储接口；项目侧适配层负责 Newtonsoft.Json 与 ES3。
/// </summary>
public interface IMPThreeDStorage
{
    MPThreeDAssemblySaveDto Load();
    void Save(MPThreeDAssemblySaveDto data);
}

public static class MPThreeDModuleServices
{
    public static IMPThreeDStorage Storage { get; set; }
}

public sealed class MPThreeDCommandHistory
{
    private readonly int m_capacity;
    private readonly List<MPThreeDAssemblySaveDto> m_states =
        new List<MPThreeDAssemblySaveDto>();
    private int m_index = -1;

    public MPThreeDCommandHistory(int capacity = 50)
    {
        m_capacity = Mathf.Max(2, capacity);
    }

    public bool CanUndo => m_index > 0;
    public bool CanRedo => m_index >= 0 && m_index < m_states.Count - 1;

    public void Reset(MPThreeDAssemblySaveDto initialState)
    {
        m_states.Clear();
        m_index = -1;
        Record(initialState ?? MPThreeDAssemblySaveDto.CreateEmpty());
    }

    public void Record(MPThreeDAssemblySaveDto state)
    {
        if (state == null)
        {
            return;
        }

        if (m_index < m_states.Count - 1)
        {
            m_states.RemoveRange(m_index + 1, m_states.Count - m_index - 1);
        }

        m_states.Add(state.Clone());
        if (m_states.Count > m_capacity)
        {
            m_states.RemoveAt(0);
        }

        m_index = m_states.Count - 1;
    }

    public bool TryUndo(out MPThreeDAssemblySaveDto state)
    {
        if (!CanUndo)
        {
            state = null;
            return false;
        }

        m_index--;
        state = m_states[m_index].Clone();
        return true;
    }

    public bool TryRedo(out MPThreeDAssemblySaveDto state)
    {
        if (!CanRedo)
        {
            state = null;
            return false;
        }

        m_index++;
        state = m_states[m_index].Clone();
        return true;
    }
}

public static class MPThreeDMath
{
    public static float Quantize(float value, float step)
    {
        if (step <= 0f)
        {
            return value;
        }

        return Mathf.Round(value / step) * step;
    }

    public static Vector3 QuantizeXZ(Vector3 position, float step)
    {
        position.x = Quantize(position.x, step);
        position.z = Quantize(position.z, step);
        return position;
    }
}
