using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

public class MPGameNumberFrameVertical : MPGameNumberFrameBase
{
    public override void Init(List<int> number, Vector2 fontSize)
    {
        base.Init(number, fontSize);

        m_text.fontSizeMin = fontSize.x;
        m_text.fontSizeMax = fontSize.y;

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

    public override void CheckNumber(List<int> number)
    {
        if (number.Count == 0)
            return;

        StringBuilder sb = new StringBuilder();
        int numIndex = 0;
        
        for (int i = 0; i < m_number.Count; i++)
        {
            if (numIndex < number.Count && m_number[i] == number[numIndex])
            {
                sb.Append($"<color={m_fillColor}>{m_number[i].ToString()}</color>");
                numIndex++;
            }
            else
            {
                sb.Append($"<color={m_defaultColor}>{m_number[i].ToString()}</color>");
            }

            if (i < m_number.Count - 1)
                sb.Append(" ");
        }

        m_text.text = sb.ToString();
    }
}
