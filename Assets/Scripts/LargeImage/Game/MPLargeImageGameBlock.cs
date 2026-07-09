using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MPLargeImageGameBlock : MonoBehaviour
{
    /// <summary>
    /// 填充标记
    /// </summary>
    private GameObject m_fill;

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
        m_fill = transform.Find("Fill").gameObject;

        m_blank = transform.Find("Blank").gameObject;

        m_wrong = transform.Find("Wrong").gameObject;

        m_blankHit = transform.Find("BlankHit").gameObject;

        m_index = index;
    }

    public void Refresh(bool isFill, bool completed, bool isFillMode = true)
    {
        StopHintAnimation();

        m_isFill = isFill;

        m_blankHit.SetActive(!isFillMode);

        m_completed = completed;
    }

    public void Fill(bool fouce = false)
    {
        if (m_completed && !fouce)
            return;

        m_blank.SetActive(false);
        m_fill.SetActive(true);
    }

    public void Blank(bool fouce = false)
    {
        if (m_completed && !fouce)
            return;

        m_blank.SetActive(true);
        m_fill.SetActive(false);
    }

    public void Empty(bool fouce = false)
    {
        if (m_completed && !fouce)
            return;

        m_blank.SetActive(false);
        m_fill.SetActive(false);
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
