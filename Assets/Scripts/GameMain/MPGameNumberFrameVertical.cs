using System.Collections;
using System.Collections.Generic;
using System.Text;

public class MPGameNumberFrameVertical : MPGameNumberFrameBase
{
    public override void Init(List<int> number, float fontSize)
    {
        base.Init(number, fontSize);

        m_text.fontSize = fontSize;

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
        //StringBuilder sb = new StringBuilder();
        //int fillCount = 0;

        //for (int i = 0; i < m_number.Count; i++)
        //{
        //    if (number.Contains(m_number[i]))
        //    {
        //        sb.Append($"</color={m_fillColor}>{m_number[i].ToString()}</color>");
        //        fillCount++;
        //    }
        //    else
        //    {
        //        sb.Append($"</color={m_defaultColor}>{m_number[i].ToString()}</color>");
        //    }

        //    if (i < m_number.Count - 1)
        //        sb.Append(" ");
        //}

        //m_text.text = sb.ToString();
    }
}
