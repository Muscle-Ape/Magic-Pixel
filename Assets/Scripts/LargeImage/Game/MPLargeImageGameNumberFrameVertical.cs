using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

public class MPLargeImageGameNumberFrameVertical : MPGameNumberFrameVertical
{
    public void Refresh(List<int> number)
    {
        m_number = number;

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

    public void DOCgFade(float value)
    {
        m_cg.alpha = value;
    }
}
