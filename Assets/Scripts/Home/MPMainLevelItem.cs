using HQ.UIManager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MPMainLevelItem : MonoBehaviour
{
    private const int PATH_POINT_COUNT = 3;
    private const float PATH_ENDPOINT_GAP = 6f;
    private const float INCOMPLETE_PATH_ALPHA = 0.3f;
    private const int LEVEL_POSITION_GROUP_SIZE = 12;
    private const float LEVEL_VERTICAL_OFFSET_SCALE = 0.8f;
    private const float HORIZONTAL_LINK_RATIO = 1.15f;
    private const float HORIZONTAL_PATH_CURVE_OFFSET = 60f;
    private const float CLOSE_LINK_ANCHOR_THRESHOLD = 150f;

    private enum CloseLinkMode
    {
        None,
        LeftToBottom,
        RightToBottom,
        TopToLeft,
        TopToRight,
    }

    /// <summary>
    /// 六组不同节奏的二维关卡轨迹，依次表现宽幅折返、侧边攀爬、交叉跳转、
    /// 中轴突刺、离散岛链和左右平台，降低长距离滑动时的重复感。
    /// </summary>
    private static readonly Vector2[][] LEVEL_POSITION_GROUPS =
    {
        new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(-250f, 20f),
            new Vector2(-310f, 135f),
            new Vector2(250f, -140f),
            new Vector2(305f, -45f),
            new Vector2(-220f, 125f),
            new Vector2(275f, -125f),
            new Vector2(120f, 70f),
            new Vector2(-305f, 135f),
            new Vector2(285f, -135f),
            new Vector2(225f, -10f),
            new Vector2(-270f, 120f),
        },
        new Vector2[]
        {
            new Vector2(280f, -130f),
            new Vector2(315f, 90f),
            new Vector2(100f, 140f),
            new Vector2(-300f, -125f),
            new Vector2(-260f, 20f),
            new Vector2(-70f, 130f),
            new Vector2(300f, -140f),
            new Vector2(250f, 40f),
            new Vector2(40f, 120f),
            new Vector2(-310f, -130f),
            new Vector2(-280f, -10f),
            new Vector2(160f, 135f),
        },
        new Vector2[]
        {
            new Vector2(-270f, -135f),
            new Vector2(-315f, 70f),
            new Vector2(-140f, 135f),
            new Vector2(290f, -120f),
            new Vector2(220f, 10f),
            new Vector2(100f, 120f),
            new Vector2(-310f, -130f),
            new Vector2(-260f, 60f),
            new Vector2(-50f, 135f),
            new Vector2(310f, -140f),
            new Vector2(260f, 20f),
            new Vector2(-130f, 125f),
        },
        new Vector2[]
        {
            new Vector2(300f, -125f),
            new Vector2(140f, 30f),
            new Vector2(60f, 135f),
            new Vector2(-300f, -135f),
            new Vector2(-80f, 20f),
            new Vector2(-60f, 125f),
            new Vector2(310f, -125f),
            new Vector2(120f, 35f),
            new Vector2(50f, 130f),
            new Vector2(-310f, -130f),
            new Vector2(-100f, 10f),
            new Vector2(30f, 120f),
        },
        new Vector2[]
        {
            new Vector2(-320f, -120f),
            new Vector2(-180f, 80f),
            new Vector2(-60f, 135f),
            new Vector2(300f, -135f),
            new Vector2(250f, 0f),
            new Vector2(80f, 120f),
            new Vector2(-300f, -125f),
            new Vector2(-120f, 50f),
            new Vector2(230f, 130f),
            new Vector2(-290f, -140f),
            new Vector2(-240f, 30f),
            new Vector2(250f, 120f),
        },
        new Vector2[]
        {
            new Vector2(-250f, -130f),
            new Vector2(-300f, 40f),
            new Vector2(260f, 130f),
            new Vector2(-280f, -120f),
            new Vector2(-60f, 0f),
            new Vector2(300f, 135f),
            new Vector2(-260f, -135f),
            new Vector2(-310f, 60f),
            new Vector2(-80f, 125f),
            new Vector2(300f, -130f),
            new Vector2(250f, 20f),
            new Vector2(-250f, 90f),
        },
    };

    /// <summary>
    /// 三个连接点在同一条平滑曲线上的采样位置。
    /// </summary>
    private static readonly float[] PATH_PROGRESS =
    {
        0f, 0.50f, 1f,
    };

    /// <summary>
    /// 非横向连接使用不同的曲线侧向幅度，避免所有弧线朝向和弯曲程度相同。
    /// </summary>
    private static readonly float[] PATH_CURVE_OFFSETS =
    {
        48f, -62f, 42f, -52f, 68f, -40f, 56f,
        -70f, 45f, -55f, 64f, -42f, 58f,
    };

    [SerializeField]
    private Material m_completedLevelIndexMaterial;

    [SerializeField]
    private Material m_currentLevelIndexMaterial;

    /// <summary>
    /// 关卡主体节点。
    /// </summary>
    private RectTransform m_levelRoot;

    /// <summary>
    /// 当前关卡到下一关之间的路线节点。
    /// </summary>
    private RectTransform m_pathRoot;

    /// <summary>
    /// 路线点 Image。节点固定存在于 Prefab 中，代码只更新位置和颜色。
    /// </summary>
    private Image[] m_pathPoints;

    /// <summary>
    /// 通关状态节点。
    /// </summary>
    private GameObject m_completed;

    /// <summary>
    /// 未通关时的关卡序号节点。
    /// </summary>
    private GameObject m_levelIndex;

    /// <summary>
    /// 当前关卡从1开始显示的下标文本。
    /// </summary>
    private TMP_Text m_levelIndexText;

    /// <summary>
    /// 关卡序号的阴影文字。
    /// </summary>
    private TMP_Text m_levelIndexShadowText;

    /// <summary>
    /// 通关星星显示节点。
    /// </summary>
    private GameObject m_stars;

    /// <summary>
    /// 通关星星文本数组，数量根据剩余生命显示。
    /// </summary>
    private GameObject[] m_starObj;

    /// <summary>
    /// 解锁未通关状态
    /// </summary>
    private GameObject m_unlock;

    /// <summary>
    /// 未解锁状态
    /// </summary>
    private GameObject m_lock;

    /// <summary>
    /// 关卡点击按钮
    /// </summary>
    private Button m_levelBtn;

    /// <summary>
    /// MainLevel Data
    /// </summary>
    private MPMainBlockInfo m_data;

    /// <summary>
    /// 当前关卡下标
    /// </summary>
    private int m_index;

    /// <summary>
    /// 标记是否解锁
    /// </summary>
    private bool m_isUnlock;

    /// <summary>
    /// 刷新页面回调
    /// </summary>
    private Action m_refresh;


    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize(Action refresh)
    {
        m_refresh = refresh;

        m_levelRoot = transform.Find("Level") as RectTransform;
        m_completed = transform.Find("Level/Completed").gameObject;
        m_levelIndex = transform.Find("Level/Index").gameObject;
        m_levelIndexText = transform.Find("Level/Index/Text").GetComponent<TMP_Text>();
        m_levelIndexShadowText = transform.Find("Level/Index/Shadow").GetComponent<TMP_Text>();

        Transform starsTransform = transform.Find("Level/Stars");
        if (starsTransform != null)
        {
            m_stars = starsTransform.gameObject;
            m_starObj = new GameObject[starsTransform.childCount];
            for (int i = 0; i < starsTransform.childCount; i++)
            {
                Transform light = starsTransform.GetChild(i).Find("Light");
                m_starObj[i] = light != null ? light.gameObject : null;
            }
        }

        m_unlock = transform.Find("Level/Unlock").gameObject;
        m_lock = transform.Find("Level/Lock").gameObject;
        m_levelBtn = m_levelRoot.GetComponent<Button>();

        CachePathVisuals();

        m_levelBtn.onClick.RemoveListener(OnLevelClick);
        m_levelBtn.onClick.AddListener(OnLevelClick);
    }

    /// <summary>
    /// 刷新
    /// </summary>
    public void Refresh(MPMainBlockInfo data, int index, int totalCount)
    {
        m_data = data;
        m_index = index;

        RefreshLevelPosition(index);

        string indexText = (m_index + 1).ToString();
        m_levelIndexText.text = indexText;
        m_levelIndexShadowText.text = indexText;

        m_isUnlock = MPUser.instance.MainLevelIsUnlock(m_data.ID);
        bool isPass = m_isUnlock && MPUser.instance.MainLevelIsPass(m_data.ID);

        m_lock.SetActive(!m_isUnlock);
        m_unlock.SetActive(m_isUnlock && !isPass);
        m_levelIndex.SetActive(m_isUnlock);
        m_completed.SetActive(isPass);
        RefreshLevelIndexMaterial(isPass);

        int stars = isPass ? MPUser.instance.GetMainLevelStars(m_data.ID) : 0;
        RefreshStars(m_isUnlock, stars);
        RefreshPath(index, totalCount, isPass);
    }

    /// <summary>
    /// 根据关卡下标设置横向位置，形成非对称的探索路线。
    /// </summary>
    private void RefreshLevelPosition(int index)
    {
        if (m_levelRoot == null)
            return;

        m_levelRoot.anchoredPosition = GetLevelPosition(index);
    }

    /// <summary>
    /// 缓存 Prefab 中已经搭建好的三个路线点，不在运行时创建 UI 节点。
    /// </summary>
    private void CachePathVisuals()
    {
        m_pathRoot = transform.Find("Path") as RectTransform;
        m_pathPoints = new Image[PATH_POINT_COUNT];

        if (m_pathRoot == null)
        {
            Debug.LogError("MPMainLevelItem Prefab 缺少 Path 节点", this);
            return;
        }

        for (int i = 0; i < m_pathPoints.Length; i++)
        {
            string pointName = $"Point_{i + 1:00}";
            Transform pointTransform = m_pathRoot.Find(pointName);
            m_pathPoints[i] = pointTransform != null
                ? pointTransform.GetComponent<Image>()
                : null;

            if (m_pathPoints[i] == null)
            {
                Debug.LogError($"MPMainLevelItem Prefab 缺少路线点 {pointName}", this);
            }
        }
    }

    /// <summary>
    /// 刷新当前关卡到下一关的三个连接点。
    /// 先从两个关卡框边缘取得连接锚点，再在锚点之间均匀采样曲线。
    /// </summary>
    private void RefreshPath(int index, int totalCount, bool isPass)
    {
        if (m_pathRoot == null || m_pathPoints == null)
            return;

        bool showPath = index >= 0 && index < totalCount - 1;
        m_pathRoot.gameObject.SetActive(showPath);
        if (!showPath)
            return;

        float rowHeight = ((RectTransform)transform).rect.height;
        if (rowHeight <= 0f)
        {
            rowHeight = ((RectTransform)transform).sizeDelta.y;
        }

        Vector3 levelCenterInPath = m_pathRoot.InverseTransformPoint(m_levelRoot.position);
        Vector2 start = new Vector2(levelCenterInPath.x, levelCenterInPath.y);
        Vector2 nextLevelPosition = GetLevelPosition(index + 1);
        Vector2 end = new Vector2(nextLevelPosition.x, rowHeight + nextLevelPosition.y);
        Vector2 maxPointExtents = GetMaxPathPointExtents();
        Vector2 startPointExtents = m_pathPoints[0] != null
            ? GetPointExtents(m_pathPoints[0].rectTransform)
            : maxPointExtents;
        Vector2 endPointExtents = m_pathPoints[PATH_POINT_COUNT - 1] != null
            ? GetPointExtents(m_pathPoints[PATH_POINT_COUNT - 1].rectTransform)
            : maxPointExtents;
        Vector2 startSafeHalfSize = GetLevelSafeHalfSize(startPointExtents);
        Vector2 endSafeHalfSize = GetLevelSafeHalfSize(endPointExtents);
        Vector2 startAnchor = GetRectEdgePoint(start, end - start, startSafeHalfSize);
        Vector2 endAnchor = GetRectEdgePoint(end, start - end, endSafeHalfSize);
        CloseLinkMode closeLinkMode = CloseLinkMode.None;
        if (Vector2.Distance(startAnchor, endAnchor) < CLOSE_LINK_ANCHOR_THRESHOLD)
        {
            bool nextLevelOnLeft = Mathf.Abs(end.x - start.x) > Mathf.Epsilon
                ? end.x < start.x
                : (index & 1) == 0;
            bool useSideToBottom = ShouldUseSideToBottomConnection(index);
            if (useSideToBottom)
            {
                closeLinkMode = nextLevelOnLeft
                    ? CloseLinkMode.LeftToBottom
                    : CloseLinkMode.RightToBottom;
                startAnchor = start + new Vector2(
                    nextLevelOnLeft
                        ? -startSafeHalfSize.x
                        : startSafeHalfSize.x,
                    0f);
                endAnchor = end + new Vector2(0f, -endSafeHalfSize.y);
            }
            else
            {
                closeLinkMode = nextLevelOnLeft
                    ? CloseLinkMode.TopToRight
                    : CloseLinkMode.TopToLeft;
                startAnchor = start + new Vector2(0f, startSafeHalfSize.y);
                endAnchor = end + new Vector2(
                    nextLevelOnLeft
                        ? endSafeHalfSize.x
                        : -endSafeHalfSize.x,
                    0f);
            }
        }

        Vector2 controlPoint = GetPathControlPoint(
            index,
            start,
            end,
            startAnchor,
            endAnchor,
            closeLinkMode);
        Color pointColor = Color.white;
        pointColor.a = isPass ? 1f : INCOMPLETE_PATH_ALPHA;

        for (int i = 0; i < m_pathPoints.Length; i++)
        {
            Image point = m_pathPoints[i];
            if (point == null)
                continue;

            float progress = PATH_PROGRESS[i];
            Vector2 position = GetQuadraticBezierPoint(
                startAnchor,
                controlPoint,
                endAnchor,
                progress);

            point.rectTransform.anchoredPosition = position;
            point.color = pointColor;
        }
    }

    /// <summary>
    /// 以 Item/Level 节点的实际尺寸为基准，只在点与关卡框之间保留少量缝隙。
    /// </summary>
    private Vector2 GetLevelSafeHalfSize(Vector2 pointExtents)
    {
        return new Vector2(
            m_levelRoot.rect.width * 0.5f + pointExtents.x + PATH_ENDPOINT_GAP,
            m_levelRoot.rect.height * 0.5f + pointExtents.y + PATH_ENDPOINT_GAP);
    }

    /// <summary>
    /// 近距离连接混用“左右侧到下侧”和“上侧到左右侧”，其余连接使用交替侧弯曲线。
    /// </summary>
    private static Vector2 GetPathControlPoint(
        int index,
        Vector2 start,
        Vector2 end,
        Vector2 startAnchor,
        Vector2 endAnchor,
        CloseLinkMode closeLinkMode)
    {
        Vector2 centerDelta = end - start;
        Vector2 anchorCenter = (startAnchor + endAnchor) * 0.5f;
        if (closeLinkMode == CloseLinkMode.LeftToBottom
            || closeLinkMode == CloseLinkMode.RightToBottom)
        {
            // 水平离开当前关卡，随后垂直接近下一关卡下侧。
            return new Vector2(endAnchor.x, startAnchor.y);
        }

        if (closeLinkMode == CloseLinkMode.TopToLeft
            || closeLinkMode == CloseLinkMode.TopToRight)
        {
            // 垂直离开当前关卡上侧，随后水平接近下一关卡侧边。
            return new Vector2(startAnchor.x, endAnchor.y);
        }

        bool isHorizontalLink = Mathf.Abs(centerDelta.x)
            >= Mathf.Abs(centerDelta.y) * HORIZONTAL_LINK_RATIO;
        if (isHorizontalLink)
        {
            anchorCenter.y -= HORIZONTAL_PATH_CURVE_OFFSET;
            return anchorCenter;
        }

        Vector2 normal = new Vector2(-centerDelta.y, centerDelta.x).normalized;
        float curveOffset = PATH_CURVE_OFFSETS[index % PATH_CURVE_OFFSETS.Length];
        return anchorCenter + normal * curveOffset;
    }

    /// <summary>
    /// 固定节奏交替两类近距离路线，避免 LoopListView2 复用时连接方式随机跳变。
    /// </summary>
    private static bool ShouldUseSideToBottomConnection(int index)
    {
        int patternIndex = index % 7;
        return patternIndex == 1 || patternIndex == 4 || patternIndex == 6;
    }

    private static Vector2 GetRectEdgePoint(
        Vector2 center,
        Vector2 direction,
        Vector2 halfSize)
    {
        float scaleX = Mathf.Abs(direction.x) > Mathf.Epsilon
            ? halfSize.x / Mathf.Abs(direction.x)
            : float.PositiveInfinity;
        float scaleY = Mathf.Abs(direction.y) > Mathf.Epsilon
            ? halfSize.y / Mathf.Abs(direction.y)
            : float.PositiveInfinity;
        float scale = Mathf.Min(scaleX, scaleY);
        return float.IsInfinity(scale)
            ? center
            : center + direction * scale;
    }

    /// <summary>
    /// 二次贝塞尔曲线保证三个连接点始终属于同一条连续弧线。
    /// </summary>
    private static Vector2 GetQuadraticBezierPoint(
        Vector2 start,
        Vector2 control,
        Vector2 end,
        float progress)
    {
        float inverseProgress = 1f - progress;
        return inverseProgress * inverseProgress * start
            + 2f * inverseProgress * progress * control
            + progress * progress * end;
    }

    private Vector2 GetMaxPathPointExtents()
    {
        Vector2 maxExtents = Vector2.zero;
        for (int i = 0; i < m_pathPoints.Length; i++)
        {
            Image point = m_pathPoints[i];
            if (point == null)
                continue;

            Vector2 pointExtents = GetPointExtents(point.rectTransform);
            maxExtents.x = Mathf.Max(maxExtents.x, pointExtents.x);
            maxExtents.y = Mathf.Max(maxExtents.y, pointExtents.y);
        }

        return maxExtents;
    }

    private void RefreshLevelIndexMaterial(bool isPass)
    {
        Material targetMaterial = isPass
            ? m_completedLevelIndexMaterial
            : m_currentLevelIndexMaterial;
        if (m_levelIndexText != null
            && targetMaterial != null
            && m_levelIndexText.fontSharedMaterial != targetMaterial)
        {
            m_levelIndexText.fontSharedMaterial = targetMaterial;
        }
    }

    /// <summary>
    /// 计算路线点旋转后的二维包围范围。
    /// </summary>
    private static Vector2 GetPointExtents(RectTransform point)
    {
        Rect rect = point.rect;
        float radians = point.localEulerAngles.z * Mathf.Deg2Rad;
        float width = rect.width * Mathf.Abs(point.localScale.x);
        float height = rect.height * Mathf.Abs(point.localScale.y);
        float horizontalExtent = Mathf.Abs(Mathf.Cos(radians)) * width * 0.5f
            + Mathf.Abs(Mathf.Sin(radians)) * height * 0.5f;
        float verticalExtent = Mathf.Abs(Mathf.Cos(radians))
                * height
                * 0.5f
            + Mathf.Abs(Mathf.Sin(radians))
                * width
                * 0.5f;
        return new Vector2(horizontalExtent, verticalExtent);
    }

    private static Vector2 GetLevelPosition(int index)
    {
        int normalizedIndex = Mathf.Abs(index);
        int positionsPerCycle = LEVEL_POSITION_GROUP_SIZE * LEVEL_POSITION_GROUPS.Length;
        int cycleIndex = normalizedIndex / positionsPerCycle;
        int indexInCycle = normalizedIndex % positionsPerCycle;
        int groupIndex = indexInCycle / LEVEL_POSITION_GROUP_SIZE;
        int positionIndex = indexInCycle % LEVEL_POSITION_GROUP_SIZE;
        Vector2 position = LEVEL_POSITION_GROUPS[groupIndex][positionIndex];

        // 完成六组后水平镜像下一轮，把完全相同的路线重复周期延长到 144 关。
        if ((cycleIndex & 1) != 0)
        {
            position.x = -position.x;
        }

        position.y *= LEVEL_VERTICAL_OFFSET_SCALE;
        return position;
    }

    public static float GetLevelVerticalOffset(int index)
    {
        return GetLevelPosition(index).y;
    }

    /// <summary>
    /// 刷新星星显示。已解锁未通关时显示三颗未点亮的星星。
    /// </summary>
    /// <param name="show">是否显示星星节点。</param>
    /// <param name="stars">通关时剩余生命对应的星数。</param>
    private void RefreshStars(bool show, int stars)
    {
        if (m_stars == null || m_starObj == null)
            return;

        m_stars.SetActive(show);
        stars = Mathf.Clamp(stars, 0, m_starObj.Length);
        for (int i = 0; i < m_starObj.Length; i++)
        {
            if (m_starObj[i] != null)
            {
                m_starObj[i].SetActive(i < stars);
            }
        }
    }

    private void OnLevelClick()
    {
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);

        if (m_isUnlock)
        {
            MPGameViewUIMsgData data = new MPGameViewUIMsgData()
            {
                blockInfo = m_data,
                index = m_index,
                refresh = m_refresh,
            };
            MPTransitionView.OpenWindow<MPGameView>(data, GetComponentInParent<AWindow>());
            return;
        }

        MPLevelUnlockPopUIMsgData unlockData = new MPLevelUnlockPopUIMsgData()
        {
            levelInfo = m_data,
            index = m_index,
            refresh = m_refresh,
        };
        UIManager.Inst.ShowWindow<MPLevelUnlockPop>(unlockData, true, UILayer.Top);
    }

    private void OnDestroy()
    {
        if (m_levelBtn != null)
        {
            m_levelBtn.onClick.RemoveListener(OnLevelClick);
        }
    }
}
