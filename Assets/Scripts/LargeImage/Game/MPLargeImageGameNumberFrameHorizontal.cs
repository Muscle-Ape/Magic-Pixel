using System.Collections;
using System.Collections.Generic;
using System.Text;

public class MPLargeImageGameNumberFrameHorizontal : MPGameNumberFrameHorizontal
{
    public void Refresh(List<int> number)
    {
        m_number = number;

        // 数字内容
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < number.Count; i++)
        {
            sb.AppendLine(number[i].ToString());
        }

        m_text.text = sb.ToString();
    }
}
