using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MPGameNumberFrameBase : MonoBehaviour
{
    /// <summary>
    /// 数字数据
    /// </summary>
    protected List<int> m_number;

    /// <summary>
    /// 文本组件
    /// </summary>
    protected TMP_Text m_text;

    protected CanvasGroup m_cg;

    /// <summary>
    /// 是否完成
    /// </summary>
    protected bool m_completed;
    public bool completed => m_completed;

    /// <summary>
    /// 默认字体颜色
    /// </summary>
    protected string m_defaultColor = "#334961";

    /// <summary>
    /// 填充后的字体颜色
    /// </summary>
    protected string m_fillColor = "#A2A2A2";

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="number"></param>
    /// <param name="fontSize"></param>
    public virtual void Init(List<int> number, float fontSize)
    {
        m_number = number;
        m_text = transform.Find("Number").GetComponent<TMP_Text>();
        m_cg = transform.GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 检查Number显示
    /// </summary>
    /// <param name="number"></param>
    /// <returns>是否全部完成</returns>
    public virtual void CheckNumber(List<int> number) { }

    /// <summary>
    /// 标记已完成
    /// </summary>
    public void Completed()
    {
        m_completed = true;

        m_cg.DOFade(0.5f, 0.3f);
    }
}
