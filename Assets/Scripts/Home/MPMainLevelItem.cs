using HQ.UIManager;
using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;

public class MPMainLevelItem : MonoBehaviour
{
    private const int LEVEL_POSITION_GROUP_SIZE = 12;
    private const float LEVEL_MIN_X = -150f;
    private const float LEVEL_VERTICAL_OFFSET_LIMIT = 25f;
    private const float BOX_HORIZONTAL_GAP = 140f;

    /// <summary>
    /// 六组左右交替且不对称的关卡位置。偶数项位于左侧，奇数项位于右侧；
    /// Y 偏移保持在较小范围内，让纵向间距有变化但不会忽远忽近。
    /// </summary>
    private static readonly Vector2[][] LEVEL_POSITION_GROUPS =
    {
        new Vector2[]
        {
            new Vector2(-120f, -18f),
            new Vector2(150f, -8f),
            new Vector2(-145f, 7f),
            new Vector2(190f, 18f),
            new Vector2(-95f, 5f),
            new Vector2(130f, -10f),
            new Vector2(-150f, -22f),
            new Vector2(215f, -7f),
            new Vector2(-80f, 9f),
            new Vector2(170f, 24f),
            new Vector2(-135f, 11f),
            new Vector2(200f, -4f),
        },
        new Vector2[]
        {
            new Vector2(-105f, -17f),
            new Vector2(220f, -3f),
            new Vector2(-140f, 13f),
            new Vector2(165f, 25f),
            new Vector2(-75f, 10f),
            new Vector2(195f, -5f),
            new Vector2(-150f, -20f),
            new Vector2(125f, -8f),
            new Vector2(-90f, 6f),
            new Vector2(230f, 19f),
            new Vector2(-130f, 8f),
            new Vector2(180f, -6f),
        },
        new Vector2[]
        {
            new Vector2(-150f, -21f),
            new Vector2(175f, -9f),
            new Vector2(-115f, 4f),
            new Vector2(225f, 17f),
            new Vector2(-85f, 3f),
            new Vector2(145f, -12f),
            new Vector2(-138f, -24f),
            new Vector2(205f, -10f),
            new Vector2(-70f, 5f),
            new Vector2(185f, 20f),
            new Vector2(-125f, 7f),
            new Vector2(240f, -8f),
        },
        new Vector2[]
        {
            new Vector2(-98f, -19f),
            new Vector2(205f, -5f),
            new Vector2(-148f, 11f),
            new Vector2(135f, 23f),
            new Vector2(-110f, 9f),
            new Vector2(230f, -6f),
            new Vector2(-78f, -18f),
            new Vector2(180f, -4f),
            new Vector2(-142f, 12f),
            new Vector2(155f, 22f),
            new Vector2(-90f, 8f),
            new Vector2(215f, -7f),
        },
        new Vector2[]
        {
            new Vector2(-135f, -23f),
            new Vector2(160f, -11f),
            new Vector2(-72f, 2f),
            new Vector2(220f, 16f),
            new Vector2(-150f, 6f),
            new Vector2(185f, -9f),
            new Vector2(-102f, -21f),
            new Vector2(240f, -6f),
            new Vector2(-145f, 10f),
            new Vector2(130f, 25f),
            new Vector2(-82f, 13f),
            new Vector2(200f, -2f),
        },
        new Vector2[]
        {
            new Vector2(-88f, -16f),
            new Vector2(235f, -1f),
            new Vector2(-148f, 14f),
            new Vector2(170f, 24f),
            new Vector2(-118f, 12f),
            new Vector2(210f, -3f),
            new Vector2(-75f, -19f),
            new Vector2(150f, -7f),
            new Vector2(-137f, 8f),
            new Vector2(225f, 21f),
            new Vector2(-100f, 5f),
            new Vector2(190f, -10f),
        },
    };

    /// <summary>
    /// 与位置组一一对应的 Level 节点 Z 轴旋转角度。
    /// </summary>
    private static readonly float[][] LEVEL_ROTATION_GROUPS =
    {
        new float[] { -7f, 4f, 2f, -6f, -3f, 7f, -8f, 3f, 5f, -5f, -2f, 6f },
        new float[] { 4f, -7f, -5f, 3f, 7f, -2f, -6f, 5f, 2f, -8f, 6f, -3f },
        new float[] { -5f, 8f, 3f, -4f, -7f, 2f, 6f, -6f, -2f, 7f, 4f, -8f },
        new float[] { 6f, -3f, -8f, 4f, 2f, -7f, -4f, 8f, 5f, -2f, -6f, 3f },
        new float[] { -8f, 5f, 2f, -6f, 7f, -3f, -5f, 4f, 8f, -4f, -1f, 6f },
        new float[] { 3f, -6f, -2f, 8f, -5f, 4f, 7f, -8f, -3f, 5f, 2f, -4f },
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
    /// 宝箱以底部中心为 Pivot，与关卡底边对齐并放在关卡另一侧。
    /// </summary>
    private RectTransform m_boxRoot;

    private Image m_boxImage;

    private Button m_boxBtn;

    private Tween m_boxShakeTween;

    /// <summary>
    /// Prefab 中宝箱关闭状态的半宽，用于稳定计算与关卡之间的水平间隔。
    /// </summary>
    private float m_boxReferenceHalfWidth;

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
    /// 当前关卡是否已经通关，只有通关后才能点击领取宝箱。
    /// </summary>
    private bool m_isPass;

    /// <summary>
    /// 刷新页面回调
    /// </summary>
    private Action m_refresh;

    /// <summary>
    /// 未满足领取条件时，请求上层展示宝箱奖励信息。
    /// 当前只预留调用接口，不实现具体弹窗。
    /// </summary>
    private Action<MPMainBlockInfo> m_boxAwardInfoRequested;

    public RectTransform LevelRoot => m_levelRoot;

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize(
        Action refresh,
        Action<MPMainBlockInfo> boxAwardInfoRequested = null)
    {
        m_refresh = refresh;
        m_boxAwardInfoRequested = boxAwardInfoRequested;

        m_levelRoot = transform.Find("Level") as RectTransform;
        m_boxRoot = transform.Find("Box") as RectTransform;
        m_boxImage = m_boxRoot != null ? m_boxRoot.GetComponent<Image>() : null;
        m_boxBtn = m_boxRoot != null ? m_boxRoot.GetComponent<Button>() : null;
        m_boxReferenceHalfWidth = m_boxRoot != null
            ? Mathf.Abs(m_boxRoot.rect.width) * 0.5f
            : 0f;
        if (m_boxImage != null)
            m_boxImage.raycastTarget = false;
        if (m_boxBtn != null)
        {
            m_boxBtn.onClick.RemoveListener(OnBoxClick);
            m_boxBtn.onClick.AddListener(OnBoxClick);
        }

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

        m_levelBtn.onClick.RemoveListener(OnLevelClick);
        m_levelBtn.onClick.AddListener(OnLevelClick);
    }

    /// <summary>
    /// 刷新
    /// </summary>
    public void Refresh(MPMainBlockInfo data, int index)
    {
        m_data = data;
        m_index = index;

        RefreshLevelTransform(index);

        string indexText = (m_index + 1).ToString();
        m_levelIndexText.text = indexText;
        m_levelIndexShadowText.text = indexText;

        m_isUnlock = MPUser.instance.MainLevelIsUnlock(m_data.ID);
        m_isPass = m_isUnlock && MPUser.instance.MainLevelIsPass(m_data.ID);

        m_lock.SetActive(!m_isUnlock);
        m_unlock.SetActive(m_isUnlock && !m_isPass);
        m_levelIndex.SetActive(m_isUnlock);
        m_completed.SetActive(m_isPass);
        RefreshLevelIndexMaterial(m_isPass);

        int stars = m_isPass ? MPUser.instance.GetMainLevelStars(m_data.ID) : 0;
        RefreshStars(m_isUnlock, stars);
        RefreshBox(index);
    }

    /// <summary>
    /// 根据关卡下标设置位置和旋转，形成左右交替但不完全对称的探索路线。
    /// </summary>
    private void RefreshLevelTransform(int index)
    {
        if (m_levelRoot == null)
            return;

        m_levelRoot.anchoredPosition = GetLevelPosition(index);
        m_levelRoot.localRotation = Quaternion.Euler(
            0f,
            0f,
            GetLevelRotation(index));
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
    /// 仅配置了有效 box_award 的关卡显示宝箱。
    /// 宝箱放在 Level 对侧，并利用底部 Pivot 与 Level 底边对齐。
    /// </summary>
    private void RefreshBox(int index)
    {
        if (m_boxRoot == null || m_boxImage == null)
            return;

        KillBoxShakeTween();
        MPMainLevelBoxAward award = m_data?.BoxAward;
        if (award == null || !award.IsValid)
        {
            SetBoxInteractable(false);
            m_boxRoot.gameObject.SetActive(false);
            return;
        }

        bool isClaimed = MPUser.instance.MainLevelBoxAwardIsClaimed(m_data.ID);
        string rewardType = award.Type.Trim().ToLowerInvariant();
        string state = isClaimed ? "open" : "close";
        string spriteLocation = $"box_{rewardType}_{state}";
        if (!YooAssets.CheckLocationValid(spriteLocation))
        {
            Debug.LogWarning(
                $"主线宝箱图片不存在或尚未加入 YooAsset：{spriteLocation}",
                this);
            SetBoxInteractable(false);
            m_boxRoot.gameObject.SetActive(false);
            return;
        }

        try
        {
            Sprite boxSprite = MPLoad.Load<Sprite>(spriteLocation, this);
            if (boxSprite == null)
            {
                SetBoxInteractable(false);
                m_boxRoot.gameObject.SetActive(false);
                return;
            }

            m_boxImage.sprite = boxSprite;
            // 开启图包含更大的光效区域，按原图尺寸切换，并依靠底部 Pivot 向上展开。
            m_boxImage.SetNativeSize();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"主线宝箱图片加载失败：{spriteLocation}，{exception.Message}",
                this);
            SetBoxInteractable(false);
            m_boxRoot.gameObject.SetActive(false);
            return;
        }

        Vector2 levelPosition = GetLevelPosition(index);
        float levelBottomY = levelPosition.y - m_levelRoot.rect.height * 0.5f;
        float direction = levelPosition.x <= 0f ? 1f : -1f;
        float centerDistance = Mathf.Abs(m_levelRoot.rect.width) * 0.5f
            + m_boxReferenceHalfWidth
            + BOX_HORIZONTAL_GAP;
        m_boxRoot.anchoredPosition = new Vector2(
            levelPosition.x + direction * centerDistance,
            levelBottomY);
        m_boxRoot.localRotation = Quaternion.identity;
        m_boxRoot.gameObject.SetActive(true);
        SetBoxInteractable(true);
    }

    private void SetBoxInteractable(bool interactable)
    {
        if (m_boxBtn != null)
            m_boxBtn.interactable = interactable;
        if (m_boxImage != null)
            m_boxImage.raycastTarget = interactable;
    }

    private void OnBoxClick()
    {
        MPMainLevelBoxAward award = m_data?.BoxAward;
        if (award == null || !award.IsValid)
            return;

        if (MPUser.instance.MainLevelBoxAwardIsClaimed(m_data.ID))
        {
            PlayBoxClaimedShake();
            return;
        }

        if (!m_isPass)
        {
            m_boxAwardInfoRequested?.Invoke(m_data);
            return;
        }

        if (!MPUser.instance.TryClaimMainLevelBoxAward(m_data, out MPRewardReceipt receipt))
            return;

        RefreshBox(m_index);
        m_refresh?.Invoke();
        MPRewardsClaimPop.Show(receipt);
    }

    private void PlayBoxClaimedShake()
    {
        KillBoxShakeTween();
        if (m_boxRoot == null)
            return;

        m_boxShakeTween = m_boxRoot
            .DOPunchRotation(new Vector3(0f, 0f, 5f), 0.35f, 6, 0.5f)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                m_boxRoot.localRotation = Quaternion.identity;
                m_boxShakeTween = null;
            });
    }

    private void KillBoxShakeTween()
    {
        if (m_boxShakeTween != null && m_boxShakeTween.IsActive())
            m_boxShakeTween.Kill();

        m_boxShakeTween = null;
        if (m_boxRoot != null)
            m_boxRoot.localRotation = Quaternion.identity;
    }

    private static Vector2 GetLevelPosition(int index)
    {
        GetLevelLayoutIndex(index, out int groupIndex, out int positionIndex);
        Vector2 position = LEVEL_POSITION_GROUPS[groupIndex][positionIndex];
        position.x = Mathf.Max(LEVEL_MIN_X, position.x);
        position.y = Mathf.Clamp(
            position.y,
            -LEVEL_VERTICAL_OFFSET_LIMIT,
            LEVEL_VERTICAL_OFFSET_LIMIT);
        return position;
    }

    private static float GetLevelRotation(int index)
    {
        GetLevelLayoutIndex(index, out int groupIndex, out int positionIndex);
        return LEVEL_ROTATION_GROUPS[groupIndex][positionIndex];
    }

    private static void GetLevelLayoutIndex(
        int index,
        out int groupIndex,
        out int positionIndex)
    {
        int normalizedIndex = Mathf.Abs(index);
        int positionsPerCycle = LEVEL_POSITION_GROUP_SIZE
            * LEVEL_POSITION_GROUPS.Length;
        int indexInCycle = normalizedIndex % positionsPerCycle;
        groupIndex = indexInCycle / LEVEL_POSITION_GROUP_SIZE;
        positionIndex = indexInCycle % LEVEL_POSITION_GROUP_SIZE;
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
            MPNewGamePop.EnterMainLevel(data, GetComponentInParent<AWindow>());
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
        KillBoxShakeTween();
        if (m_levelBtn != null)
        {
            m_levelBtn.onClick.RemoveListener(OnLevelClick);
        }
        if (m_boxBtn != null)
        {
            m_boxBtn.onClick.RemoveListener(OnBoxClick);
        }

        m_refresh = null;
        m_boxAwardInfoRequested = null;
        MPLoad.ReleaseAll(this);
    }
}
