using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class MPGameNumberFrameVertical : MonoBehaviour
{
    public void Init(List<int> number)
    {
        TMP_Text m_text = transform.Find("Number").GetComponent<TMP_Text>();

        // 数字内容
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < number.Count; i++)
        {
            sb.Append(number[i].ToString());

            if (i < number.Count - 1)
                sb.Append(" ");
        }

        m_text.text = sb.ToString();
    }
}
