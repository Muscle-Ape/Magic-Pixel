using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MPLargeImageGameBlock : MonoBehaviour
{
    /// <summary>
    /// 填充色块和叉号出现时的缩放动画时长。
    /// </summary>
    private const float MARK_SCALE_ANIMATION_DURATION = 0.18f;

    /// <summary>
    /// 填充标记
    /// </summary>
    private GameObject m_fill;

    /// <summary>
    /// 格子外框。
    /// </summary>
    private Image m_frame;

    /// <summary>
    /// 填充节点下用于普通游戏状态显示的颜色块。
    /// </summary>
    private GameObject m_fillColor;

    /// <summary>
    /// 空白标记模式提示
    /// </summary>
    private GameObject m_blankHit;

    /// <summary>
    /// 空白标记
    /// </summary>
    private GameObject m_blank;

    /// <summary>
    /// 错误提示
    /// </summary>
    private GameObject m_wrong;

    /// <summary>
    /// 提示道具闪烁动画。
    /// </summary>
    private Tween m_hintTween;

    /// <summary>
    /// 结算时像素格缩放变色动画。
    /// </summary>
    private Sequence m_settlementSequence;

    /// <summary>
    /// 是否填充
    /// </summary>
    private bool m_isFill;
    public bool isFill => m_isFill;

    /// <summary>
    /// 是否已经完成
    /// </summary>
    private bool m_completed;
    public bool completed => m_completed;

    /// <summary>
    /// 下标位置
    /// </summary>
    private int m_index;
    public int index => m_index;

    /// <summary>
    /// 二位下标位置
    /// </summary>
    private Vector2 m_index2;
    public Vector2 index2 => m_index2;

    /// <summary>
    /// 完成并且是填充
    /// </summary>
    public bool fillCompleted
    {
        get
        {
            return m_isFill && m_completed;
        }
    }

    public void Init(int index)
    {
        Transform frame = transform.Find("Frame");
        if (frame != null)
        {
            m_frame = frame.GetComponent<Image>();
        }

        m_fill = transform.Find("Fill").gameObject;
        Transform fillColor = transform.Find("Fill/Color");
        if (fillColor != null)
        {
            m_fillColor = fillColor.gameObject;
        }

        m_blank = transform.Find("Blank").gameObject;

        m_wrong = transform.Find("Wrong").gameObject;

        m_blankHit = transform.Find("BlankHit").gameObject;

        m_index = index;
    }

    /// <summary>
    /// 替换格子外框图片；未传入图片时保留预制体默认外框。
    /// </summary>
    public void SetFrameSprite(Sprite sprite)
    {
        if (m_frame == null || sprite == null)
            return;

        m_frame.sprite = sprite;
    }

    public void Refresh(bool isFill, bool completed, bool isFillMode = true)
    {
        StopHintAnimation();
        m_settlementSequence?.Kill();

        m_isFill = isFill;

        m_blankHit.SetActive(!isFillMode);

        m_completed = completed;
    }

    public void Fill(bool fouce = false)
    {
        if (m_completed && !fouce)
            return;

        HideMark(m_blank);
        ShowMark(m_fill, !fouce);
    }

    public void Blank(bool fouce = false)
    {
        if (m_completed && !fouce)
            return;

        ShowMark(m_blank, !fouce);
        HideMark(m_fill);
    }

    public void Empty(bool fouce = false)
    {
        if (m_completed && !fouce)
            return;

        HideMark(m_blank);
        HideMark(m_fill);
    }

    public void Wrong()
    {
        if (m_completed)
            return;

        StartCoroutine(WrongAnimation());
    }

    public void SetBlankHit(bool active)
    {
        if (m_completed)
            return;

        m_blankHit.SetActive(active);
    }

    public void Disable()
    {
        //GetComponent<Image>().raycastTarget = false;
        m_completed = true;
    }

    /// <summary>
    /// 播放提示道具命中的格子闪烁动画。
    /// </summary>
    public void PlayHintAnimation()
    {
        GameObject target = m_isFill ? m_fill : m_blank;
        if (target == null)
            return;

        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null)
            return;

        m_hintTween?.Kill();
        target.SetActive(true);

        cg.alpha = 1;

        m_hintTween = cg.DOFade(0.25f, 0.2f)
            .SetEase(Ease.Linear)
            .SetLoops(4, LoopType.Yoyo)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                cg.alpha = 1;
                m_hintTween = null;
            });
    }

    /// <summary>
    /// 停止提示闪烁动画，并恢复格子最终显示状态。
    /// </summary>
    public void StopHintAnimation()
    {
        if (m_hintTween != null)
        {
            m_hintTween.Kill();
            m_hintTween = null;
        }

        SetAlpha(m_fill, 1);
        SetAlpha(m_blank, 1);
    }

    /// <summary>
    /// 播放结算时当前格子变为最终像素颜色的动画。
    /// </summary>
    /// <param name="targetColor">最终像素颜色。</param>
    /// <param name="duration">动画时长。</param>
    public Tween PlaySettlementAnimation(Color targetColor, float duration)
    {
        if (m_fill == null)
            return null;

        m_hintTween?.Kill();
        m_settlementSequence?.Kill();

        m_blank.SetActive(false);
        m_blankHit.SetActive(false);
        m_wrong.SetActive(false);
        m_fill.SetActive(true);
        if (m_fillColor != null)
        {
            m_fillColor.SetActive(false);
        }

        Image img = m_fill.GetComponent<Image>();
        if (img == null)
            return null;

        targetColor.a = 1;
        img.color = Color.white;
        m_fill.transform.localScale = Vector3.zero;

        m_settlementSequence = DOTween.Sequence();
        m_settlementSequence.Join(m_fill.transform.DOScale(Vector3.one, duration).SetEase(Ease.OutBack));
        m_settlementSequence.Join(img.DOColor(targetColor, duration).SetEase(Ease.Linear));
        m_settlementSequence.SetLink(gameObject);
        m_settlementSequence.OnComplete(() => m_settlementSequence = null);

        return m_settlementSequence;
    }

    /// <summary>
    /// 设置目标图片透明度。
    /// </summary>
    /// <param name="target">需要设置透明度的节点。</param>
    /// <param name="alpha">目标透明度。</param>
    private void SetAlpha(GameObject target, float alpha)
    {
        if (target == null)
            return;

        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null)
            return;

        cg.alpha = alpha;
    }

    /// <summary>
    /// 显示填充色块或叉号，并根据需要播放从0到1的缩放动画。
    /// </summary>
    /// <param name="target">需要显示的标记节点。</param>
    /// <param name="playAnimation">是否播放出现动画。</param>
    private void ShowMark(GameObject target, bool playAnimation)
    {
        if (target == null)
            return;

        target.transform.DOKill();
        target.SetActive(true);

        if (!playAnimation)
        {
            target.transform.localScale = Vector3.one;
            return;
        }

        target.transform.localScale = Vector3.zero;
        target.transform.DOScale(Vector3.one, MARK_SCALE_ANIMATION_DURATION)
            .SetEase(Ease.OutBack)
            .SetLink(target);
    }

    /// <summary>
    /// 隐藏填充色块或叉号，并清理未完成的缩放动画。
    /// </summary>
    /// <param name="target">需要隐藏的标记节点。</param>
    private void HideMark(GameObject target)
    {
        if (target == null)
            return;

        target.transform.DOKill();
        target.transform.localScale = Vector3.one;
        target.SetActive(false);
    }

    /// <summary>
    /// 错误动画
    /// </summary>
    /// <returns></returns>
    private IEnumerator WrongAnimation()
    {
        Image img = m_wrong.GetComponent<Image>();

        var color = img.color;
        color.a = 0;
        img.color = color;
        m_wrong.SetActive(true);

        yield return img.DOFade(1, 0.2f).SetEase(Ease.Linear).SetLoops(2, LoopType.Yoyo);
    }
}
