using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MPStaurationPanel : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    /// <summary>
    /// RectTransform Node
    /// </summary>
    private RectTransform m_rectTransform;

    /// <summary>
    /// 位置标记
    /// </summary>
    private RectTransform m_tag;

    /// <summary>
    /// 创建的精灵图
    /// </summary>
    private Sprite m_saturationSprite;

    /// <summary>
    /// 当前的Hsv
    /// </summary>
    private Vector3 m_currentHueHsv;

    /// <summary>
    /// 显示的图片
    /// </summary>
    private Image m_saturationImg;

    private int m_width;
    private int m_height;

    private Color m_color;

    private Action<Color> m_setColor;

    public void Initialization(Action<Color> setColor)
    {
        m_rectTransform = transform as RectTransform;
        m_tag = transform.Find("Tag") as RectTransform;
        m_saturationImg = transform.GetComponent<Image>();

        m_setColor = setColor;

        m_width = (int)m_rectTransform.rect.width;
        m_height = (int)m_rectTransform.rect.height;
        m_saturationSprite = Sprite.Create(new Texture2D(m_width, m_height), new Rect(0, 0, m_width, m_height), new Vector2(0, 0));
    }

    /// <summary>
    /// 更新饱和度
    /// </summary>
    public void UpdateStauration(Color currentHue)
    {
        m_color = currentHue;

        Color.RGBToHSV(currentHue, out m_currentHueHsv.x, out m_currentHueHsv.y, out m_currentHueHsv.z);

        for (int y = 0; y < m_height; y++)
        {
            for (int x = 0; x < m_width; x++)
            {
                var pixColor = Color.HSVToRGB(m_currentHueHsv.x, (float)x / m_width, (float)y / m_height);
                m_saturationSprite.texture.SetPixel(x, y, pixColor);
            }
        }
        m_saturationSprite.texture.Apply();

        m_saturationImg.sprite = m_saturationSprite;

        SetColor(m_tag.anchoredPosition);
    }

    private void SetColor(Vector2 localPoint)
    {
        localPoint.x = Mathf.Clamp(localPoint.x, -m_rectTransform.rect.width / 2, m_rectTransform.rect.width / 2);
        localPoint.y = Mathf.Clamp(localPoint.y, -m_rectTransform.rect.height / 2, m_rectTransform.rect.height / 2);
        m_tag.anchoredPosition = localPoint;

        // 越界判断
        localPoint.x += m_width / 2;
        localPoint.y += m_height / 2;
        int x = Mathf.Clamp((int)localPoint.x, 0, m_width);
        int y = Mathf.Clamp((int)localPoint.y, 0, m_height);

        // 取色
        Color.RGBToHSV(m_color, out m_currentHueHsv.x, out m_currentHueHsv.y, out m_currentHueHsv.z);
        Color color = Color.HSVToRGB(m_currentHueHsv.x, (float)x / m_width, (float)y / m_height);

        m_setColor?.Invoke(color);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rectTransform, eventData.position, Camera.main, out Vector2 localPoint);

        SetColor(localPoint);
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rectTransform, eventData.position, Camera.main, out Vector2 localPoint);

        SetColor(localPoint);
    }
}
