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

        m_blank = transform.Find("Blank").gameObject;

        m_wrong = transform.Find("Wrong").gameObject;

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

    public void Disable()
    {
        //GetComponent<Image>().raycastTarget = false;
        m_completed = true;
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
