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
    /// 空白标记
    /// </summary>
    private GameObject m_blank;

    /// <summary>
    /// 错误提示
    /// </summary>
    private GameObject m_wrong;

    /// <summary>
    /// 是否填充
    /// </summary>
    private bool m_isFill;
    public bool isFill => m_isFill;


    public void Init(bool isFill)
    {
        m_fill = transform.Find("Fill").gameObject;

        m_blank = transform.Find("Blank").gameObject;

        m_wrong = transform.Find("Wrong").gameObject;

        m_isFill = isFill;
    }

    public void Fill()
    {
        m_fill.SetActive(true);
    }

    public void Blank()
    {
        m_blank.SetActive(true);
    }

    public void Wrong()
    {
        StartCoroutine(WrongAnimation());
    }

    public void Disable()
    {
        GetComponent<Image>().raycastTarget = false;
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
