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

    private Action<Color> m_setColor;

    public void Initialization(Action<Color> setColor)
    {
        m_rectTransform = transform as RectTransform;
        m_tag = transform.Find("Tag") as RectTransform;
        m_saturationImg = transform.GetComponent<Image>();

        m_setColor = setColor;

        int sWidth = (int)m_rectTransform.rect.width, sHeight = (int)m_rectTransform.rect.height;
        m_saturationSprite = Sprite.Create(new Texture2D(sWidth, sHeight), new Rect(0, 0, sWidth, sHeight), new Vector2(0, 0));
    }

    /// <summary>
    /// 更新饱和度
    /// </summary>
    public void UpdateStauration(Color currentHue)
    {
        int sWidth = (int)m_rectTransform.rect.width, sHeight = (int)m_rectTransform.rect.height;

        Color.RGBToHSV(currentHue, out m_currentHueHsv.x, out m_currentHueHsv.y, out m_currentHueHsv.z);

        for (int y = 0; y < sHeight; y++)
        {
            for (int x = 0; x < sWidth; x++)
            {
                var pixColor = Color.HSVToRGB(m_currentHueHsv.x, (float)x / sWidth, (float)y / sHeight);
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
        localPoint.x += m_rectTransform.rect.width / 2;
        localPoint.y += m_rectTransform.rect.height / 2;
        int x = Mathf.Clamp((int)localPoint.x, 1, (int)(m_rectTransform.rect.width) - 1);
        int y = Mathf.Clamp((int)localPoint.y, 1, (int)(m_rectTransform.rect.height) - 1);

        // 取色
        Color color = m_saturationSprite.texture.GetPixel(x, y);

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
