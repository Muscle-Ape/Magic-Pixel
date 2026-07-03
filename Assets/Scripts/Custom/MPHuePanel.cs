using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MPHuePanel : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    /// <summary>
    /// 图片
    /// </summary>
    private Sprite m_hueSprite;

    /// <summary>
    /// RectTransfom 组件
    /// </summary>
    private RectTransform m_rectTransform;

    /// <summary>
    /// 当前取的颜色
    /// </summary>
    private Color m_color;

    /// <summary>
    /// 颜色位置标记
    /// </summary>
    private RectTransform m_tag;

    /// <summary>
    /// 饱和度面板
    /// </summary>
    private MPStaurationPanel m_staurationPanel;

    public void Initialization(MPStaurationPanel staurationPanel)
    {
        m_rectTransform = transform as RectTransform;
        m_tag = transform.Find("Tag") as RectTransform;

        UpdateHue();

        m_staurationPanel = staurationPanel;
        m_staurationPanel.UpdateStauration(m_color);
    }

    /// <summary>
    /// 更新色泽度 
    /// </summary>
    private void UpdateHue()
    {
        float w = m_rectTransform.rect.width, h = m_rectTransform.rect.height;

        m_hueSprite = Sprite.Create(new Texture2D((int)w, (int)h), new Rect(0, 0, w, h), new Vector2(0, 0));

        for (int x = 0; x <= h; x++)
        {
            Color pixColor = Color.HSVToRGB(x / h, 1, 1);
            for (int y = 0; y < w; y++)
            {
                m_hueSprite.texture.SetPixel(y, x, pixColor);
            }
        }
        m_hueSprite.texture.Apply();

        transform.GetComponent<Image>().sprite = m_hueSprite;

        m_color = m_hueSprite.texture.GetPixel(0, 0);
    }

    private void SetColor(Vector2 localPoint)
    {
        localPoint.x = 0;
        localPoint.y = Mathf.Clamp(localPoint.y, -m_rectTransform.rect.height / 2, m_rectTransform.rect.height / 2);
        m_tag.anchoredPosition = localPoint;

        // 越界判断
        localPoint.y += m_rectTransform.rect.height / 2;
        int y = (int)localPoint.y;
        if (y < 0 || y > m_rectTransform.rect.height)
            return;

        // 取色
        m_color = m_hueSprite.texture.GetPixel(0, y);

        m_staurationPanel.UpdateStauration(m_color);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 坐标转换
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rectTransform, eventData.position, Camera.main, out Vector2 localPoint);

        SetColor(localPoint);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 坐标转换
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rectTransform, eventData.position, Camera.main, out Vector2 localPoint);

        SetColor(localPoint);
    }
}
