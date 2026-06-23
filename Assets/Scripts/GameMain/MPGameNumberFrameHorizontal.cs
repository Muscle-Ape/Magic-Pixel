using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class MPGameNumberFrameHorizontal : MonoBehaviour
{
    public void Init(List<int> number, float fontSize)
    {
        TMP_Text m_text = transform.Find("Number").GetComponent<TMP_Text>();

        m_text.fontSize = fontSize;

        // 数字内容
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < number.Count; i++)
        {
            sb.AppendLine(number[i].ToString());
        }

        m_text.text = sb.ToString();
    }
}
