using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MPGameBlock : MonoBehaviour
{
    /// <summary>
    /// 填充标记
    /// </summary>
    private GameObject m_fill;

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

    public void Init(bool isFill, int index)
    {
        m_fill = transform.Find("Fill").gameObject;
        Transform fillColor = transform.Find("Fill/Color");
        if (fillColor != null)
        {
            m_fillColor = fillColor.gameObject;
        }

        m_blank = transform.Find("Blank").gameObject;

        m_wrong = transform.Find("Wrong").gameObject;

        m_blankHit = transform.Find("BlankHit").gameObject;

        m_isFill = isFill;
        m_index = index;
    }

    public void Fill()
    {
        if (m_completed)
            return;

        m_fill.SetActive(true);
    }

    public void Blank()
    {
        if (m_completed)
            return;

        m_blank.SetActive(true);
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
