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
    /// <summary>
    /// 图片节点从结算框移动到最终位置的动画时长。
    /// </summary>
    private const float PICTURE_MOVE_DURATION = 0.45f;

    /// <summary>
    /// 底部按钮、标题和星星缩放显示的动画时长。
    /// </summary>
    private const float ELEMENT_SHOW_DURATION = 0.28f;

    /// <summary>
    /// 金币数量文本。
    /// </summary>
    [TransformPath("View/Up/Coin/Count")]
    private TMP_Text m_coinText;

    /// <summary>
    /// 钻石数量文本。
    /// </summary>
    [TransformPath("View/Up/Diamond/Count")]
    private TMP_Text m_diamondText;

    /// <summary>
    /// 返回按钮。
    /// </summary>
    [TransformPath("View/Up/BackBtn")]
    private Button m_backBtn;

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

    /// <summary>
    /// 通关完成图片。
    /// </summary>
    [TransformPath("View/PictureNode/Picture")]
    private Image m_picture;

    /// <summary>
    /// 星星父节点。
    /// </summary>
    [TransformPath("View/Stars")]
    private RectTransform m_stars;

    /// <summary>
    /// 标题节点。
    /// </summary>
    [TransformPath("View/Title")]
    private RectTransform m_title;

    /// <summary>
    /// 标题文本，用于淡入显示。
    /// </summary>
    [TransformPath("View/Title")]
    private TMP_Text m_titleText;

    /// <summary>
    /// 当前完成的主线关卡配置。
    /// </summary>
    private MPMainBlockInfo m_blockInfo;

    /// <summary>
    /// 当前完成的主线关卡下标。
    /// </summary>
    private int m_index;

    /// <summary>
    /// 通关时剩余生命值，用于换算通关星星数。
    /// </summary>
    private int m_lovesCount;

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
    /// 每颗星星的节点。
    /// </summary>
    private readonly List<RectTransform> m_starNodes = new List<RectTransform>();

    /// <summary>
    /// 每颗星星点亮状态的Open节点。
    /// </summary>
    private readonly List<GameObject> m_starOpenNodes = new List<GameObject>();

    /// <summary>
    /// 每颗星星在预制体中的原始缩放。
    /// </summary>
    private readonly List<Vector3> m_starOriginalScales = new List<Vector3>();

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

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPGameCompletedViewUIMsgData data = uiMsg as MPGameCompletedViewUIMsgData;
        if (data == null)
        {
            ReturnHome();
            return;
        }

        m_blockInfo = data.blockInfo;
        m_index = data.index;
        m_lovesCount = data.lovesCount;
        m_pictureStartPosition = ResolvePictureStartPosition(data);
        m_refreshAction = data.refresh;

        CacheOriginalState();
        RegisterUI();
        RefreshUI();
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

        m_replayOriginalScale = m_replayBtn == null ? Vector3.one : m_replayBtn.transform.localScale;
        m_nextOriginalScale = m_nextBtn == null ? Vector3.one : m_nextBtn.transform.localScale;
        m_titleOriginalScale = m_title == null ? Vector3.one : m_title.localScale;

        m_starNodes.Clear();
        m_starOpenNodes.Clear();
        m_starOriginalScales.Clear();

        if (m_stars == null)
            return;

        for (int i = 0; i < m_stars.childCount; i++)
        {
            RectTransform star = m_stars.GetChild(i) as RectTransform;
            if (star == null)
                continue;

            m_starNodes.Add(star);
            m_starOriginalScales.Add(star.localScale);

            Transform open = star.Find("Open");
            m_starOpenNodes.Add(open == null ? null : open.gameObject);
        }
    }

    /// <summary>
    /// 将游戏页CompletedFrame的屏幕坐标转换到当前PictureNode父节点的本地坐标。
    /// </summary>
    /// <param name="data">游戏结算页打开时传入的数据。</param>
    /// <returns>PictureNode入场动画的起始锚点位置。</returns>
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
        if (m_backBtn != null)
        {
            m_backBtn.onClick.RemoveListener(ReturnHome);
            m_backBtn.onClick.AddListener(ReturnHome);
        }

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

    /// <summary>
    /// 刷新顶部资源数量显示。
    /// </summary>
    private void RefreshUI()
    {
        if (m_coinText != null)
        {
            m_coinText.text = MPUser.instance.GetCoins().ToString();
        }

        if (m_diamondText != null)
        {
            m_diamondText.text = MPUser.instance.GetDiamond().ToString();
        }
    }

    /// <summary>
    /// 根据通关时剩余生命值显示对应数量的星星。
    /// </summary>
    private void RefreshStars()
    {
        int stars = Mathf.Clamp(m_lovesCount, 0, m_starOpenNodes.Count);
        for (int i = 0; i < m_starOpenNodes.Count; i++)
        {
            if (m_starOpenNodes[i] != null)
            {
                m_starOpenNodes[i].SetActive(i < stars);
            }
        }
    }

    /// <summary>
    /// 刷新主线关卡完成图。
    /// </summary>
    private void RefreshPicture()
    {
        if (m_picture == null || m_blockInfo == null)
            return;

        m_picture.sprite = MPLoad.Load<Sprite>("icon_" + m_blockInfo.ID);
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

        for (int i = 0; i < m_starNodes.Count; i++)
        {
            if (m_starNodes[i] != null)
            {
                m_starNodes[i].localScale = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// 播放结算页入场动画，先移动完成图，再显示标题、星星和底部按钮。
    /// </summary>
    private void PlayEnterAnimation()
    {
        m_enterSequence?.Kill();
        m_enterSequence = DOTween.Sequence().SetLink(gameObject);

        if (m_pictureNode != null)
        {
            m_pictureNode.DOKill();
            m_enterSequence.Append(m_pictureNode.DOAnchorPos(m_pictureTargetPosition, PICTURE_MOVE_DURATION).SetEase(Ease.Linear));
        }

        m_enterSequence.Append(CreateElementShowTween());

        MPAudioManager.Instance.PlaySound(MPSound.MPSoundGameCompleted);
    }

    /// <summary>
    /// 创建标题、星星和底部按钮同时缩放显示的动画。
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

        for (int i = 0; i < m_starNodes.Count; i++)
        {
            if (m_starNodes[i] == null)
                continue;

            m_starNodes[i].DOKill();
            sequence.Join(m_starNodes[i].DOScale(m_starOriginalScales[i], ELEMENT_SHOW_DURATION).SetEase(Ease.OutBack));
        }

        return sequence;
    }

    /// <summary>
    /// 点击重玩按钮，重新打开当前主线关卡。
    /// </summary>
    private void OnReplayClick()
    {
        OpenMainLevel(m_blockInfo, m_index);
    }

    /// <summary>
    /// 点击下一关按钮，如果下一关不存在或尚未解锁，则返回主页。
    /// </summary>
    private void OnNextLevelClick()
    {
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

        DestroyWindow();
        UIManager.Inst.ShowWindow<MPGameView>(data);
    }

    /// <summary>
    /// 返回主页，并触发关卡列表刷新。
    /// </summary>
    private void ReturnHome()
    {
        DestroyWindow();
        m_refreshAction?.Invoke();
    }

    private void OnDestroy()
    {
        m_enterSequence?.Kill();
    }
}

public class MPGameCompletedViewUIMsgData : UIMsgData
{
    /// <summary>
    /// 当前完成的主线关卡配置。
    /// </summary>
    public MPMainBlockInfo blockInfo;

    /// <summary>
    /// 当前完成的主线关卡下标。
    /// </summary>
    public int index;

    /// <summary>
    /// 通关时剩余生命值，用于显示星星数量。
    /// </summary>
    public int lovesCount;

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
    /// 页面返回主页时刷新主界面关卡列表的回调。
    /// </summary>
    public Action refresh;
}
