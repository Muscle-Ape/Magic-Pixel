using System.Collections;
using System.Collections.Generic;
using System.Text;

public class MPGameNumberFrameHorizontal : MPGameNumberFrameBase
{
    public override void Init(List<int> number, float fontSize)
    {
        base.Init(number, fontSize);

        m_text.fontSize = fontSize;

        // 数字内容
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < number.Count; i++)
        {
            sb.AppendLine(number[i].ToString());
        }

        m_text.text = sb.ToString();
    }


    public override void CheckNumber(List<int> number)
    {
        //StringBuilder sb = new StringBuilder();

        //for (int i = 0; i < m_number.Count; i++)
        //{
        //    if (number.Contains(m_number[i]))
        //    {
        //        sb.AppendLine($"</color={m_fillColor}>{m_number[i].ToString()}</color>");
        //    }
        //    else
        //    {
        //        sb.AppendLine($"</color={m_defaultColor}>{m_number[i].ToString()}</color>");
        //    }
        //}

        //m_text.text = sb.ToString();
    }
}
