using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 让 4×9 云幕切片等比铺满 Grid，在编辑器预览和运行时使用同一套布局。
/// 挂在预制体 View/Grid 上，代替普通 GridLayoutGroup。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("MagicPixel/UI/Transition Grid Layout")]
public sealed class MPTransitionGridLayout : GridLayoutGroup
{
    private const int ART_COLUMNS = 4;
    private const int ART_ROWS = 9;
    private const float ART_TILE_PIXELS = 209f;
    // 运行时动画与编辑器终点烘焙共用参数；调整之后重新执行 Bake Cloud Curtain。
    public const float CLOUD_BACK_TRAVEL = 18f;
    public const float CLOUD_MIDDLE_TRAVEL = -25f;
    public const float CLOUD_FRONT_TRAVEL = 32f;
    [SerializeField] private RectTransform m_artwork;
    [SerializeField] private Sprite[] m_exitSprites;
    private Vector2 m_appliedCellSize;

    public bool HasExitSprites(int itemCount)
    {
        if (itemCount != ART_COLUMNS * ART_ROWS || m_exitSprites == null || m_exitSprites.Length != itemCount)
            return false;
        for (int i = 0; i < m_exitSprites.Length; i++)
        {
            if (m_exitSprites[i] == null)
                return false;
        }
        return true;
    }

    public Sprite GetExitSprite(int index)
    {
        return m_exitSprites != null && index >= 0 && index < m_exitSprites.Length ? m_exitSprites[index] : null;
    }

    public override void CalculateLayoutInputHorizontal()
    {
        RefreshGridSettings();
        base.CalculateLayoutInputHorizontal();
    }

    public override void CalculateLayoutInputVertical()
    {
        RefreshGridSettings();
        base.CalculateLayoutInputVertical();
    }

    public override void SetLayoutHorizontal()
    {
        RefreshGridSettings();
        base.SetLayoutHorizontal();
        m_appliedCellSize = m_CellSize;
        AlignArtwork();
    }

    public override void SetLayoutVertical()
    {
        // 父布局可能在纵向布局阶段才确定最终高度，此时同步更新两个方向的方块尺寸。
        RefreshGridSettings();
        if (m_appliedCellSize != m_CellSize)
        {
            base.SetLayoutHorizontal();
            m_appliedCellSize = m_CellSize;
        }

        base.SetLayoutVertical();
        AlignArtwork();
    }

    private void AlignArtwork()
    {
        if (m_artwork == null)
            return;

        // 前景与 36 块拼图共用同一格子边长，绝不再按屏幕尺寸独立做一次适配。
        m_artwork.sizeDelta = new Vector2(ART_COLUMNS * ART_TILE_PIXELS, ART_ROWS * ART_TILE_PIXELS);
        m_artwork.localScale = Vector3.one * (m_CellSize.x / ART_TILE_PIXELS);
        m_artwork.position = rectTransform.TransformPoint(rectTransform.rect.center);
    }

    private void RefreshGridSettings()
    {
        // 已经处于 UGUI 布局流程中，直接设置继承字段，避免属性 setter 反复请求下一帧重建。
        m_Constraint = Constraint.FixedColumnCount;
        m_ConstraintCount = ART_COLUMNS;
        m_StartCorner = Corner.UpperLeft;
        m_StartAxis = Axis.Horizontal;
        m_ChildAlignment = TextAnchor.MiddleCenter;
        m_Spacing = Vector2.zero;
        m_Padding.left = m_Padding.right = m_Padding.top = m_Padding.bottom = 0;

        // 使用当前 RectTransform 的 UI 尺寸，兼容 CanvasScaler，不能使用固定手机宽度。
        Vector2 size = rectTransform.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            return;

        float side = Mathf.Ceil(Mathf.Max(size.x / ART_COLUMNS, size.y / ART_ROWS));
        m_CellSize = new Vector2(side, side);
    }
}
