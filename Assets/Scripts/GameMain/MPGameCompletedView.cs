using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPGameCompletedView")]
public class MPGameCompletedView : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>
    /// 图片节点从结算框移动到最终位置的动画时长。
    /// </summary>
    private const float PICTURE_MOVE_DURATION = 0.45f;

    /// <summary>
    /// 大图从当前局部放大状态缩回完整图片的动画时长。
    /// </summary>
    private const float LARGE_IMAGE_ZOOM_BACK_DURATION = 0.5f;

    /// <summary>
    /// 底部按钮、标题和 Stars 容器缩放显示的动画时长。
    /// </summary>
    private const float ELEMENT_SHOW_DURATION = 0.28f;

    private const float LIGHT_FADE_DURATION = 0.35f;
    private const float LIGHT_ROTATION_DURATION = 18f;
    private const float STAR_DROP_START_SCALE = 3f;
    private const float STAR_DROP_DURATION = 0.14f;
    private const float STAR_SETTLE_DURATION = 0.08f;
    private const float STAR_DROP_INTERVAL = 0.03f;

    [TransformPath("View/Head")]
    private MPHead m_head;

    /// <summary>
    /// 重玩当前关卡按钮。
    /// </summary>
    [TransformPath("View/ReplayBtn")]
    private Button m_replayBtn;

    /// <summary>
    /// 进入下一关按钮。
    /// </summary>
    [TransformPath("View/NextBtn")]
    private Button m_nextBtn;

    /// <summary>
    /// 完成图片所在节点，用于从游戏页结算框位置移动到当前页面初始位置。
    /// </summary>
    [TransformPath("View/PictureNode")]
    private RectTransform m_pictureNode;

    /// <summary>图片归位后，光芒同时开始淡入和循环旋转。</summary>
    [TransformPath("View/Light")]
    private Image m_light;

    /// <summary>
    /// 通关完成图片。
    /// </summary>
    [TransformPath("View/PictureNode/Picture")]
    private Image m_picture;

    /// <summary>
    /// 普通主关卡和自定义关卡的完成像素网格。
    /// 大图模式仍使用 Picture 图片节点。
    /// </summary>
    [TransformPath("View/PictureNode/Grid")]
    private GridLayoutGroup m_pictureGrid;

    /// <summary>
    /// 完成图片的 RectTransform。大图模式会单独控制它的位置和缩放。
    /// </summary>
    private RectTransform m_pictureTransform;

    /// <summary>
    /// 大图模式运行时创建的裁剪区域，保持与旧大图结算页相同的局部展示范围。
    /// </summary>
    private RectTransform m_largeImagePictureMask;

    /// <summary>
    /// 星星父节点。
    /// </summary>
    [TransformPath("View/Stars")]
    private RectTransform m_stars;

    /// <summary>
    /// 标题节点。
    /// </summary>
    [TransformPath("View/TitleFrame")]
    private RectTransform m_title;

    /// <summary>
    /// 标题文本，用于淡入显示。
    /// </summary>
    [TransformPath("View/TitleFrame/Title")]
    private TMP_Text m_titleText;

    /// <summary>
    /// 当前完成的主线关卡配置。
    /// </summary>
    private MPMainBlockInfo m_blockInfo;

    /// <summary>
    /// 当前完成的大图关卡配置。
    /// </summary>
    private MPLargeImageBlockInfo m_largeImageBlockInfo;

    /// <summary>
    /// 当前完成的自定义关卡数据。
    /// </summary>
    private MPCustomLevelInfo m_customLevelInfo;

    /// <summary>
    /// 当前完成页是否来自自定义关卡。
    /// </summary>
    private bool m_isCustomLevel;

    /// <summary>
    /// 当前完成页是否来自大图模式。
    /// </summary>
    private bool m_isLargeImageLevel;

    /// <summary>
    /// 当前完成的关卡下标。
    /// </summary>
    private int m_index;

    /// <summary>
    /// 通关时剩余生命值，用于换算通关星星数。
    /// </summary>
    private int m_lovesCount;

    /// <summary>
    /// 大图通关时可视区域左上角在完整图片中的行列坐标。
    /// </summary>
    private Vector2Int m_largeImageViewHead;

    /// <summary>
    /// 完整大图的行列尺寸。
    /// </summary>
    private int m_largeImageSize;

    /// <summary>
    /// 大图模式可视区域的行列尺寸。
    /// </summary>
    private int m_largeImageVisibleSize;

    /// <summary>
    /// 返回主页或重开关卡时用于刷新关卡列表的回调。
    /// </summary>
    private Action m_refreshAction;

    /// <summary>
    /// 图片节点在结算页中的目标位置，也就是预制体内配置的初始位置。
    /// </summary>
    private Vector2 m_pictureTargetPosition;

    /// <summary>
    /// 图片节点进入结算页时的起始位置，对齐游戏页的CompletedFrame。
    /// </summary>
    private Vector2 m_pictureStartPosition;

    /// <summary>
    /// 大图完成图片在结算页中心的原始位置。
    /// </summary>
    private Vector2 m_largeImageTargetPosition;

    /// <summary>
    /// 大图完成图片放大到通关可视区域时的起始位置。
    /// </summary>
    private Vector2 m_largeImageStartPosition;

    /// <summary>
    /// 大图完成图片在结算页中的原始缩放。
    /// </summary>
    private Vector3 m_largeImageTargetScale;

    /// <summary>
    /// 大图完成图片覆盖通关可视区域时的起始缩放。
    /// </summary>
    private Vector3 m_largeImageStartScale;

    /// <summary>
    /// 每颗星星的节点。
    /// </summary>
    private readonly List<RectTransform> m_starNodes = new List<RectTransform>();

    /// <summary>
    /// 每颗星星点亮状态的Open节点。
    /// </summary>
    private readonly List<RectTransform> m_starOpenNodes = new List<RectTransform>();

    /// <summary>
    /// 每颗点亮星星在预制体中的原始缩放，默认为 1。
    /// </summary>
    private readonly List<Vector3> m_starOpenOriginalScales = new List<Vector3>();

    private Vector3 m_starsOriginalScale;
    private float m_lightTargetAlpha;
    private Quaternion m_lightOriginalRotation;

    /// <summary>
    /// 重玩按钮在预制体中的原始缩放。
    /// </summary>
    private Vector3 m_replayOriginalScale;

    /// <summary>
    /// 下一关按钮在预制体中的原始缩放。
    /// </summary>
    private Vector3 m_nextOriginalScale;

    /// <summary>
    /// 标题在预制体中的原始缩放。
    /// </summary>
    private Vector3 m_titleOriginalScale;

    /// <summary>
    /// 页面入场动画序列，关闭页面时需要主动清理。
    /// </summary>
    private Sequence m_enterSequence;

    // 无限旋转单独持有，不放入入场 Sequence，避免阻塞后续星星动画。
    private Tween m_lightFadeTween;
    private Tween m_lightRotationTween;

    /// <summary>
    /// 当前完成页动态创建的像素格，页面刷新或销毁时统一清理。
    /// </summary>
    private readonly List<GameObject> m_runtimePixelCells = new List<GameObject>();

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        KillAnimations();
        MPLoad.ReleaseAll(this);
        MPGameCompletedViewUIMsgData data = uiMsg as MPGameCompletedViewUIMsgData;
        if (data == null)
        {
            ReturnHome();
            return;
        }

        m_blockInfo = data.blockInfo;
        m_largeImageBlockInfo = data.largeImageBlockInfo;
        m_customLevelInfo = data.customLevelInfo;
        m_isCustomLevel = data.isCustomLevel;
        m_isLargeImageLevel = data.isLargeImageLevel && !m_isCustomLevel;
        m_index = data.index;
        m_lovesCount = data.lovesCount;
        m_largeImageViewHead = data.largeImageViewHead;
        m_largeImageSize = data.largeImageSize;
        m_largeImageVisibleSize = data.largeImageVisibleSize;
        m_pictureStartPosition = ResolvePictureStartPosition(data);
        m_refreshAction = data.refresh;

        RefreshCustomModeLayout();
        CacheOriginalState();
        RegisterUI();
        RefreshStars();
        RefreshPicture();
        PrepareAnimationState();
        PlayEnterAnimation();
    }

    /// <summary>
    /// 缓存预制体中配置好的初始状态，供入场动画恢复到正确位置和缩放。
    /// </summary>
    private void CacheOriginalState()
    {
        if (m_pictureNode != null)
        {
            m_pictureTargetPosition = m_pictureNode.anchoredPosition;
        }

        m_pictureTransform = m_picture == null ? null : m_picture.rectTransform;
        if (m_isLargeImageLevel)
        {
            CreateLargeImagePictureMask();
            if (m_pictureTransform != null)
            {
                m_largeImageTargetPosition = m_pictureTransform.anchoredPosition;
                m_largeImageTargetScale = m_pictureTransform.localScale;
                CalculateLargeImageStartState();
            }
        }

        m_replayOriginalScale = m_replayBtn == null ? Vector3.one : m_replayBtn.transform.localScale;
        m_nextOriginalScale = m_nextBtn == null ? Vector3.one : m_nextBtn.transform.localScale;
        m_titleOriginalScale = m_title == null ? Vector3.one : m_title.localScale;
        m_starsOriginalScale = m_stars == null ? Vector3.one : m_stars.localScale;

        if (m_light != null)
        {
            m_lightTargetAlpha = m_light.color.a;
            m_lightOriginalRotation = m_light.rectTransform.localRotation;
        }

        m_starNodes.Clear();
        m_starOpenNodes.Clear();
        m_starOpenOriginalScales.Clear();

        if (m_stars == null)
            return;

        for (int i = 0; i < m_stars.childCount; i++)
        {
            RectTransform star = m_stars.GetChild(i) as RectTransform;
            if (star == null)
                continue;

            m_starNodes.Add(star);
        }

        // 不依赖 Hierarchy 中的排列顺序，始终从左向右逐颗落下。
        m_starNodes.Sort((left, right) => left.anchoredPosition.x.CompareTo(right.anchoredPosition.x));
        foreach (RectTransform star in m_starNodes)
        {
            RectTransform open = star.Find("Open") as RectTransform;
            m_starOpenNodes.Add(open);
            m_starOpenOriginalScales.Add(open == null ? Vector3.one : open.localScale);
        }
    }

    /// <summary>
    /// 为统一结算页动态创建大图裁剪区域。
    /// 旧大图结算 Prefab 使用 800×800 Mask 包裹图片，这里运行时复原相同结构，
    /// 不修改主关卡结算 Prefab，也不会影响主关卡和自定义关卡。
    /// </summary>
    private void CreateLargeImagePictureMask()
    {
        if (m_pictureNode == null || m_pictureTransform == null || m_largeImagePictureMask != null)
            return;

        Vector2 originalPosition = m_pictureTransform.anchoredPosition;
        Vector3 originalScale = m_pictureTransform.localScale;
        Vector2 pictureSize = m_pictureTransform.rect.size;
        int originalSiblingIndex = m_pictureTransform.GetSiblingIndex();

        GameObject maskObject = new GameObject(
            "LargeImagePictureMask",
            typeof(RectTransform),
            typeof(RectMask2D));
        maskObject.layer = m_picture.gameObject.layer;

        m_largeImagePictureMask = maskObject.GetComponent<RectTransform>();
        m_largeImagePictureMask.SetParent(m_pictureNode, false);
        m_largeImagePictureMask.SetSiblingIndex(originalSiblingIndex);
        m_largeImagePictureMask.anchorMin = new Vector2(0.5f, 0.5f);
        m_largeImagePictureMask.anchorMax = new Vector2(0.5f, 0.5f);
        m_largeImagePictureMask.pivot = new Vector2(0.5f, 0.5f);
        m_largeImagePictureMask.anchoredPosition = originalPosition;
        m_largeImagePictureMask.sizeDelta = pictureSize;
        m_largeImagePictureMask.localScale = Vector3.one;

        m_pictureTransform.SetParent(m_largeImagePictureMask, false);
        m_pictureTransform.anchorMin = new Vector2(0.5f, 0.5f);
        m_pictureTransform.anchorMax = new Vector2(0.5f, 0.5f);
        m_pictureTransform.pivot = new Vector2(0.5f, 0.5f);
        m_pictureTransform.anchoredPosition = Vector2.zero;
        m_pictureTransform.localScale = originalScale;
    }

    /// <summary>
    /// 按照旧大图结算算法计算内部图片的放大倍数和偏移位置，
    /// 使动画开始时展示的局部内容与通关时的 10×10 可视区域一致。
    /// </summary>
    private void CalculateLargeImageStartState()
    {
        int imageSize = Mathf.Max(1, m_largeImageSize);
        int visibleSize = Mathf.Clamp(m_largeImageVisibleSize, 1, imageSize);
        float zoomScale = Mathf.Max(1f, imageSize / (float)visibleSize);

        Vector2 contentSize = GetLargeImagePictureSize();
        float centerColumn = Mathf.Clamp(m_largeImageViewHead.y + visibleSize * 0.5f, 0f, imageSize);
        float centerRow = Mathf.Clamp(m_largeImageViewHead.x + visibleSize * 0.5f, 0f, imageSize);
        float columnPercent = centerColumn / imageSize;
        float rowPercent = centerRow / imageSize;

        Vector2 offset = new Vector2(
            (0.5f - columnPercent) * contentSize.x * zoomScale,
            (rowPercent - 0.5f) * contentSize.y * zoomScale);

        m_largeImageStartPosition = m_largeImageTargetPosition + offset;
        m_largeImageStartScale = new Vector3(
            m_largeImageTargetScale.x * zoomScale,
            m_largeImageTargetScale.y * zoomScale,
            m_largeImageTargetScale.z);
    }

    /// <summary>获取大图完成图片的实际显示尺寸。</summary>
    private Vector2 GetLargeImagePictureSize()
    {
        if (m_pictureTransform != null)
        {
            Vector2 size = m_pictureTransform.rect.size;
            if (size.x > 0f && size.y > 0f)
            {
                return size;
            }
        }

        if (m_largeImagePictureMask != null)
        {
            return m_largeImagePictureMask.rect.size;
        }

        return Vector2.one;
    }

    /// <summary>
    /// 自定义关卡没有下一关按钮，重玩按钮需要居中显示。
    /// </summary>
    private void RefreshCustomModeLayout()
    {
        if (!m_isCustomLevel)
            return;

        if (m_nextBtn != null)
        {
            m_nextBtn.gameObject.SetActive(false);
        }

        if (m_replayBtn != null)
        {
            RectTransform replayRect = m_replayBtn.transform as RectTransform;
            if (replayRect != null)
            {
                Vector2 anchoredPosition = replayRect.anchoredPosition;
                anchoredPosition.x = 0f;
                replayRect.anchoredPosition = anchoredPosition;
            }
        }
    }

    /// <summary>
    /// 将游戏页完成图片的屏幕坐标转换到当前 PictureNode 父节点的本地坐标。
    /// </summary>
    /// <param name="data">游戏结算页打开时传入的数据。</param>
    /// <returns>PictureNode 入场动画的起始锚点位置。</returns>
    private Vector2 ResolvePictureStartPosition(MPGameCompletedViewUIMsgData data)
    {
        if (m_pictureNode == null || !data.hasPictureStartScreenPosition)
        {
            return data.pictureStartAnchoredPosition;
        }

        RectTransform parent = m_pictureNode.parent as RectTransform;
        if (parent == null)
        {
            return data.pictureStartAnchoredPosition;
        }

        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.pictureStartScreenPosition, camera, out Vector2 localPoint))
        {
            return localPoint;
        }

        return data.pictureStartAnchoredPosition;
    }

    /// <summary>
    /// 注册结算页按钮事件。
    /// </summary>
    private void RegisterUI()
    {
        m_head.Init(ReturnHome, OnSettingClick, MPUserPop.Show);

        if (m_replayBtn != null)
        {
            m_replayBtn.onClick.RemoveListener(OnReplayClick);
            m_replayBtn.onClick.AddListener(OnReplayClick);
        }

        if (m_nextBtn != null)
        {
            m_nextBtn.onClick.RemoveListener(OnNextLevelClick);
            m_nextBtn.onClick.AddListener(OnNextLevelClick);
        }
    }

    public override void OnFocus(bool focus)
    {
        if (focus)
        {
            m_head?.Refresh();
        }
    }

    /// <summary>
    /// 点亮星星先全部隐藏，获得的星星由掉落序列逐颗打开，避免提前闪现。
    /// </summary>
    private void RefreshStars()
    {
        for (int i = 0; i < m_starOpenNodes.Count; i++)
        {
            if (m_starOpenNodes[i] != null)
            {
                m_starOpenNodes[i].gameObject.SetActive(false);
                m_starOpenNodes[i].localScale = m_starOpenOriginalScales[i] * STAR_DROP_START_SCALE;
            }
        }
    }

    /// <summary>
    /// 刷新主线、自定义或大图关卡完成图。
    /// </summary>
    private void RefreshPicture()
    {
        ClearPixelGrid();

        if (m_picture != null)
        {
            m_picture.sprite = null;
            m_picture.gameObject.SetActive(m_isLargeImageLevel);
        }

        if (m_pictureGrid != null)
        {
            m_pictureGrid.gameObject.SetActive(!m_isLargeImageLevel);
        }

        if (m_isLargeImageLevel)
        {
            if (m_picture == null || m_largeImageBlockInfo == null)
                return;

            m_picture.sprite = MPLoad.Load<Sprite>("icon_" + m_largeImageBlockInfo.ID, this);
            m_picture.preserveAspect = true;
            return;
        }

        if (m_pictureGrid == null)
            return;

        if (m_isCustomLevel)
        {
            RefreshCustomLevelGrid();
        }
        else
        {
            RefreshMainLevelGrid();
        }
    }

    /// <summary>
    /// 根据自定义关卡配置直接生成完成像素格，不再依赖本地缓存图片。
    /// </summary>
    private void RefreshCustomLevelGrid()
    {
        if (m_customLevelInfo == null || m_customLevelInfo.Size <= 0)
            return;

        int size = m_customLevelInfo.Size;
        int cellCount = size * size;
        Color[] pixelColors = new Color[cellCount];
        for (int i = 0; i < pixelColors.Length; i++)
        {
            pixelColors[i] = Color.white;
        }

        List<MPCustomLevelColorInfo> colors = m_customLevelInfo.Colors;
        if (colors != null)
        {
            for (int i = 0; i < colors.Count; i++)
            {
                MPCustomLevelColorInfo colorInfo = colors[i];
                if (colorInfo == null || colorInfo.Index < 0 || colorInfo.Index >= cellCount)
                    continue;

                if (!string.IsNullOrEmpty(colorInfo.Color) &&
                    ColorUtility.TryParseHtmlString(colorInfo.Color, out Color color))
                {
                    pixelColors[colorInfo.Index] = NormalizePixelColor(color);
                }
            }
        }

        CreatePixelGrid(size, pixelColors);
    }

    /// <summary>
    /// 读取主关卡原始像素纹理并按游戏网格尺寸采样生成Image格子。
    /// </summary>
    private void RefreshMainLevelGrid()
    {
        if (m_blockInfo == null)
            return;

        Texture2D sourceTexture = MPLoad.Load<Texture2D>(m_blockInfo.ID, this);
        if (sourceTexture == null)
            return;

        int size = Mathf.Max(1, sourceTexture.height);
        Texture2D readableTexture = CreateReadableTexture(sourceTexture);
        if (readableTexture == null)
            return;

        try
        {
            Color[] pixelColors = new Color[size * size];
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    int index = row * size + column;
                    int x = Mathf.Clamp(column * readableTexture.width / size, 0, readableTexture.width - 1);
                    int y = Mathf.Clamp(readableTexture.height - 1 - row * readableTexture.height / size, 0, readableTexture.height - 1);
                    pixelColors[index] = NormalizePixelColor(readableTexture.GetPixel(x, y));
                }
            }

            CreatePixelGrid(size, pixelColors);
        }
        finally
        {
            Destroy(readableTexture);
        }
    }

    /// <summary>
    /// 创建纯UGUI Image像素网格。
    /// </summary>
    private void CreatePixelGrid(int size, IReadOnlyList<Color> pixelColors)
    {
        if (m_pictureGrid == null || size <= 0 || pixelColors == null)
            return;

        RectTransform gridTransform = m_pictureGrid.transform as RectTransform;
        if (gridTransform == null)
            return;

        float gridWidth = gridTransform.rect.width > 0f ? gridTransform.rect.width : gridTransform.sizeDelta.x;
        float gridHeight = gridTransform.rect.height > 0f ? gridTransform.rect.height : gridTransform.sizeDelta.y;
        float cellSize = Mathf.Min(gridWidth, gridHeight) / size;

        m_pictureGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        m_pictureGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        m_pictureGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        m_pictureGrid.constraintCount = size;
        m_pictureGrid.spacing = Vector2.zero;
        m_pictureGrid.cellSize = Vector2.one * cellSize;

        int cellCount = Mathf.Min(size * size, pixelColors.Count);
        for (int i = 0; i < cellCount; i++)
        {
            GameObject pixelObject = new GameObject(
                $"Pixel_{i}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            pixelObject.layer = m_pictureGrid.gameObject.layer;
            pixelObject.transform.SetParent(m_pictureGrid.transform, false);

            Image pixelImage = pixelObject.GetComponent<Image>();
            pixelImage.color = pixelColors[i];
            pixelImage.raycastTarget = false;
            m_runtimePixelCells.Add(pixelObject);
        }
    }

    /// <summary>
    /// 将透明像素与白色背景合成，保持与主游戏结算动画一致的最终颜色。
    /// </summary>
    private static Color NormalizePixelColor(Color color)
    {
        Color result = Color.Lerp(Color.white, new Color(color.r, color.g, color.b, 1f), color.a);
        result.a = 1f;
        return result;
    }

    /// <summary>
    /// 复制不可读纹理，避免为了结算页修改资源导入设置。
    /// </summary>
    private static Texture2D CreateReadableTexture(Texture2D source)
    {
        if (source == null)
            return null;

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32);
        Texture2D readableTexture = null;

        try
        {
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;
            readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readableTexture.Apply(false, false);
            return readableTexture;
        }
        catch
        {
            if (readableTexture != null)
            {
                Destroy(readableTexture);
            }

            throw;
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    /// <summary>
    /// 清理当前页面生成的像素格。
    /// </summary>
    private void ClearPixelGrid()
    {
        for (int i = 0; i < m_runtimePixelCells.Count; i++)
        {
            GameObject pixelCell = m_runtimePixelCells[i];
            if (pixelCell == null)
                continue;

            pixelCell.SetActive(false);
            Destroy(pixelCell);
        }

        m_runtimePixelCells.Clear();
    }

    /// <summary>
    /// 将需要播放入场动画的节点预设到动画开始状态。
    /// </summary>
    private void PrepareAnimationState()
    {
        if (m_pictureNode != null)
        {
            m_pictureNode.anchoredPosition = m_pictureStartPosition;
        }

        if (m_isLargeImageLevel && m_pictureTransform != null)
        {
            m_pictureTransform.anchoredPosition = m_largeImageStartPosition;
            m_pictureTransform.localScale = m_largeImageStartScale;
        }

        if (m_replayBtn != null)
        {
            m_replayBtn.transform.localScale = Vector3.zero;
        }

        if (m_nextBtn != null)
        {
            m_nextBtn.transform.localScale = Vector3.zero;
        }

        if (m_title != null)
        {
            m_title.localScale = Vector3.zero;
        }

        if (m_titleText != null)
        {
            Color color = m_titleText.color;
            color.a = 0;
            m_titleText.color = color;
        }

        if (m_stars != null)
        {
            m_stars.localScale = Vector3.zero;
        }

        if (m_light != null)
        {
            m_light.raycastTarget = false;
            m_light.rectTransform.localRotation = m_lightOriginalRotation;
            Color color = m_light.color;
            color.a = 0f;
            m_light.color = color;
        }
    }

    /// <summary>
    /// 播放结算页入场动画。
    /// 主关卡直接移动完成图；大图模式会先移动放大的局部图，再缩回完整图片，
    /// 图片归位即淡入光芒；大图缩回完整图片后显示标题、Stars 和底部按钮，
    /// 等 Stars 缩放完毕，再逐颗播放点亮星星的掉落动画。
    /// </summary>
    private void PlayEnterAnimation()
    {
        KillAnimations();
        m_enterSequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.PauseOnDisablePlayOnEnable);
        bool hasPictureMoveAnimation = false;

        if (m_pictureNode != null)
        {
            m_pictureNode.DOKill();
            m_enterSequence.Append(m_pictureNode.DOAnchorPos(m_pictureTargetPosition, PICTURE_MOVE_DURATION).SetEase(Ease.Linear));
            hasPictureMoveAnimation = true;
        }

        // Light 的出现只等待 PictureNode 归位，不额外等待大图的缩回动画。
        m_enterSequence.AppendCallback(PlayLightReveal);

        if (m_isLargeImageLevel)
        {
            Tween zoomBackTween = CreateLargeImageZoomBackTween();
            if (zoomBackTween != null)
            {
                m_enterSequence.Append(zoomBackTween);
                hasPictureMoveAnimation = true;
            }
        }

        if (hasPictureMoveAnimation)
        {
            // 完成图片到达结算页最终位置时触发一次落位反馈。
            m_enterSequence.AppendCallback(MPVibrationManager.Instance.PlayMediumImpact);
        }

        m_enterSequence.Append(CreateElementShowTween());
        Tween starDropTween = CreateStarDropTween();
        if (starDropTween != null)
            m_enterSequence.Append(starDropTween);

        MPAudioManager.Instance.PlaySound(MPSound.MPSoundGameCompleted);
    }

    private void PlayLightReveal()
    {
        KillLightTweens();
        if (this == null || IsDestoried || m_light == null)
            return;

        m_light.gameObject.SetActive(true);
        m_lightFadeTween = m_light.DOFade(m_lightTargetAlpha, LIGHT_FADE_DURATION)
            .SetEase(Ease.Linear)
            .SetLink(gameObject, LinkBehaviour.PauseOnDisablePlayOnEnable)
            .OnComplete(() => m_lightFadeTween = null);
        StartLightRotation();
    }

    private void StartLightRotation()
    {
        if (this == null || IsDestoried || m_light == null)
            return;

        m_lightRotationTween?.Kill();
        m_lightRotationTween = m_light.rectTransform
            .DOLocalRotate(new Vector3(0f, 0f, -360f), LIGHT_ROTATION_DURATION, RotateMode.LocalAxisAdd)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetLink(gameObject, LinkBehaviour.PauseOnDisablePlayOnEnable);
    }

    private void KillLightTweens()
    {
        m_lightFadeTween?.Kill();
        m_lightFadeTween = null;
        m_lightRotationTween?.Kill();
        m_lightRotationTween = null;
    }

    /// <summary>统一停止入场、光芒淡入和循环旋转，不执行完成回调。</summary>
    private void KillAnimations()
    {
        m_enterSequence?.Kill();
        m_enterSequence = null;
        KillLightTweens();
    }

    /// <summary>创建大图从放大局部区域缩回原始大小和中心位置的动画。</summary>
    private Tween CreateLargeImageZoomBackTween()
    {
        if (m_pictureTransform == null)
        {
            return null;
        }

        m_pictureTransform.DOKill();
        Sequence sequence = DOTween.Sequence();
        sequence.Join(m_pictureTransform
            .DOAnchorPos(m_largeImageTargetPosition, LARGE_IMAGE_ZOOM_BACK_DURATION)
            .SetEase(Ease.Linear));
        sequence.Join(m_pictureTransform
            .DOScale(m_largeImageTargetScale, LARGE_IMAGE_ZOOM_BACK_DURATION)
            .SetEase(Ease.Linear));
        return sequence;
    }

    /// <summary>
    /// 创建标题、Stars 容器和底部按钮同时缩放显示的动画，此时 Open 仍保持隐藏。
    /// </summary>
    private Tween CreateElementShowTween()
    {
        Sequence sequence = DOTween.Sequence();

        if (m_replayBtn != null)
        {
            m_replayBtn.transform.DOKill();
            sequence.Join(m_replayBtn.transform.DOScale(m_replayOriginalScale, ELEMENT_SHOW_DURATION).SetEase(Ease.OutBack));
        }

        if (m_nextBtn != null)
        {
            m_nextBtn.transform.DOKill();
            sequence.Join(m_nextBtn.transform.DOScale(m_nextOriginalScale, ELEMENT_SHOW_DURATION).SetEase(Ease.OutBack));
        }

        if (m_title != null)
        {
            m_title.DOKill();
            sequence.Join(m_title.DOScale(m_titleOriginalScale, ELEMENT_SHOW_DURATION).SetEase(Ease.OutBack));
        }

        if (m_titleText != null)
        {
            m_titleText.DOKill();
            sequence.Join(m_titleText.DOFade(1f, ELEMENT_SHOW_DURATION).SetEase(Ease.Linear));
        }

        if (m_stars != null)
        {
            m_stars.DOKill();
            sequence.Join(m_stars.DOScale(m_starsOriginalScale, ELEMENT_SHOW_DURATION).SetEase(Ease.OutBack));
        }

        return sequence;
    }

    /// <summary>按获得的星数串行播放：3 倍加速砸落、轻微压缩回弹，再开始下一颗。</summary>
    private Tween CreateStarDropTween()
    {
        int stars = Mathf.Clamp(m_lovesCount, 0, m_starOpenNodes.Count);
        Sequence sequence = null;
        for (int i = 0; i < stars; i++)
        {
            RectTransform open = m_starOpenNodes[i];
            if (open == null)
                continue;

            if (sequence == null)
                sequence = DOTween.Sequence();
            else
                sequence.AppendInterval(STAR_DROP_INTERVAL);

            Vector3 targetScale = m_starOpenOriginalScales[i];
            open.DOKill();
            sequence.AppendCallback(() =>
            {
                if (open == null || this == null || IsDestoried)
                    return;

                open.localScale = targetScale * STAR_DROP_START_SCALE;
                open.gameObject.SetActive(true);
            });
            sequence.Append(open.DOScale(targetScale * 0.9f, STAR_DROP_DURATION).SetEase(Ease.InQuad));
            sequence.Append(open.DOScale(targetScale, STAR_SETTLE_DURATION).SetEase(Ease.OutBack));
        }

        return sequence;
    }

    /// <summary>
    /// 点击重玩按钮，重新打开当前关卡。
    /// </summary>
    private void OnReplayClick()
    {
        if (m_isCustomLevel)
        {
            OpenCustomLevel();
            return;
        }

        if (m_isLargeImageLevel)
        {
            OpenLargeImageLevel(m_largeImageBlockInfo, m_index);
            return;
        }

        OpenMainLevel(m_blockInfo, m_index);
    }

    /// <summary>
    /// 点击下一关按钮，如果下一关不存在或尚未解锁，则返回上一层关卡列表。
    /// </summary>
    private void OnNextLevelClick()
    {
        if (m_isCustomLevel)
        {
            ReturnHome();
            return;
        }

        if (m_isLargeImageLevel)
        {
            OpenNextLargeImageLevel();
            return;
        }

        List<MPMainBlockInfo> levels = MPDataManager.Instance.m_mainLevelModel.blockInfos;
        int nextIndex = m_index + 1;

        if (levels == null || nextIndex >= levels.Count)
        {
            ReturnHome();
            return;
        }

        MPMainBlockInfo nextLevel = levels[nextIndex];
        if (nextLevel == null || !MPUser.instance.MainLevelIsUnlock(nextLevel.ID))
        {
            ReturnHome();
            return;
        }

        OpenMainLevel(nextLevel, nextIndex);
    }

    /// <summary>
    /// 打开下一张大图关卡；不存在或尚未解锁时返回大图关卡列表。
    /// </summary>
    private void OpenNextLargeImageLevel()
    {
        List<MPLargeImageBlockInfo> levels = MPDataManager.Instance.m_largeImageModel.blockInfos;
        int nextIndex = m_index + 1;

        if (levels == null || nextIndex >= levels.Count)
        {
            ReturnHome();
            return;
        }

        MPLargeImageBlockInfo nextLevel = levels[nextIndex];
        if (nextLevel == null || !MPUser.instance.LargeImageLevelIsUnlock(nextLevel.ID))
        {
            ReturnHome();
            return;
        }

        OpenLargeImageLevel(nextLevel, nextIndex);
    }

    /// <summary>
    /// 打开设置弹窗。
    /// </summary>
    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }

    private void UnregisterUI()
    {
        m_head?.Release();

        if (m_replayBtn != null)
        {
            m_replayBtn.onClick.RemoveListener(OnReplayClick);
        }

        if (m_nextBtn != null)
        {
            m_nextBtn.onClick.RemoveListener(OnNextLevelClick);
        }
    }

    /// <summary>
    /// 打开指定主线关卡。
    /// </summary>
    private void OpenMainLevel(MPMainBlockInfo blockInfo, int index)
    {
        if (blockInfo == null)
        {
            ReturnHome();
            return;
        }

        MPGameViewUIMsgData data = new MPGameViewUIMsgData()
        {
            blockInfo = blockInfo,
            index = index,
            refresh = m_refreshAction,
        };

        MPNewGamePop.EnterMainLevel(data, this, closeSource: true);
    }

    /// <summary>
    /// 打开指定大图关卡。
    /// </summary>
    private void OpenLargeImageLevel(MPLargeImageBlockInfo blockInfo, int index)
    {
        if (blockInfo == null)
        {
            ReturnHome();
            return;
        }

        MPLargeImageGameViewUIMsgData data = new MPLargeImageGameViewUIMsgData()
        {
            blockInfo = blockInfo,
            index = index,
            refresh = m_refreshAction,
        };

        MPNewGamePop.EnterLargeImageLevel(data, this, closeSource: true);
    }

    /// <summary>
    /// 重新打开当前自定义关卡。
    /// </summary>
    private void OpenCustomLevel()
    {
        if (m_customLevelInfo == null)
        {
            ReturnHome();
            return;
        }

        MPGameViewUIMsgData data = new MPGameViewUIMsgData()
        {
            customLevelInfo = m_customLevelInfo,
            isCustomLevel = true,
            index = m_index,
            refresh = m_refreshAction,
        };

        MPNewGamePop.EnterCustomLevel(data, this, closeSource: true);
    }

    /// <summary>
    /// 返回上一层关卡列表，并触发列表刷新。
    /// </summary>
    private void ReturnHome()
    {
        MPTransitionView.Play(() =>
        {
            DestroyWindow();
            m_refreshAction?.Invoke();

            MPAudioManager.Instance.PlayBGM(MPMusic.MPBGMMain);
        });
    }

    public override void OnRelease()
    {
        MPNoNetworkPop.DismissLevelEntry(this);
        UnregisterUI();
        KillAnimations();
        ClearPixelGrid();
        MPLoad.ReleaseAll(this);
        base.OnRelease();
    }

    private void OnDestroy()
    {
        KillAnimations();
        ClearPixelGrid();
        MPLoad.ReleaseAll(this);
    }
}

public class MPGameCompletedViewUIMsgData : UIMsgData
{
    /// <summary>
    /// 当前完成的主线关卡配置。
    /// </summary>
    public MPMainBlockInfo blockInfo;

    /// <summary>
    /// 当前完成的大图关卡配置。
    /// </summary>
    public MPLargeImageBlockInfo largeImageBlockInfo;

    /// <summary>
    /// 当前完成的自定义关卡数据。
    /// </summary>
    public MPCustomLevelInfo customLevelInfo;

    /// <summary>
    /// 当前完成页是否来自自定义关卡。
    /// </summary>
    public bool isCustomLevel;

    /// <summary>
    /// 当前完成页是否来自大图模式。
    /// </summary>
    public bool isLargeImageLevel;

    /// <summary>
    /// 当前完成的关卡下标。
    /// </summary>
    public int index;

    /// <summary>
    /// 通关时剩余生命值，用于显示星星数量。
    /// </summary>
    public int lovesCount;

    /// <summary>
    /// 大图通关时可视区域左上角坐标，仅大图模式使用。
    /// </summary>
    public Vector2Int largeImageViewHead;

    /// <summary>
    /// 完整大图行列尺寸，仅大图模式使用。
    /// </summary>
    public int largeImageSize;

    /// <summary>
    /// 大图可视区域行列尺寸，仅大图模式使用。
    /// </summary>
    public int largeImageVisibleSize;

    /// <summary>
    /// 完成图片入场动画的起始锚点位置，对齐MPGameView中的CompletedFrame。
    /// </summary>
    public Vector2 pictureStartAnchoredPosition;

    /// <summary>
    /// 完成图片入场动画的起始屏幕坐标，用于跨页面转换到正确位置。
    /// </summary>
    public Vector2 pictureStartScreenPosition;

    /// <summary>
    /// 是否存在有效的起始屏幕坐标。
    /// </summary>
    public bool hasPictureStartScreenPosition;

    /// <summary>
    /// 页面返回上一层时刷新对应关卡列表的回调。
    /// </summary>
    public Action refresh;
}
