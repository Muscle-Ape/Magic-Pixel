using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MPHuePanel : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    /// <summary>
    /// 色相条运行时生成的精灵。
    /// </summary>
    private Sprite m_hueSprite;

    /// <summary>
    /// 色相条RectTransform组件。
    /// </summary>
    private RectTransform m_rectTransform;

    /// <summary>
    /// 当前选中的色相颜色。
    /// </summary>
    private Color m_color;

    /// <summary>
    /// 色相位置标记。
    /// </summary>
    private RectTransform m_tag;

    /// <summary>
    /// 饱和度面板。
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
    /// 生成横向显示的色相渐变图。
    /// </summary>
    private void UpdateHue()
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(m_rectTransform.rect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(m_rectTransform.rect.height));
        Texture2D hueTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int x = 0; x < width; x++)
        {
            float hue = width <= 1 ? 0 : x / (float)(width - 1);
            Color pixColor = Color.HSVToRGB(hue, 1, 1);
            for (int y = 0; y < height; y++)
            {
                hueTexture.SetPixel(x, y, pixColor);
            }
        }

        hueTexture.Apply();

        m_hueSprite = Sprite.Create(hueTexture, new Rect(0, 0, width, height), new Vector2(0, 0));
        transform.GetComponent<Image>().sprite = m_hueSprite;

        m_color = hueTexture.GetPixel(0, 0);
    }

    /// <summary>
    /// 根据HSV值设置色相条标记位置。
    /// </summary>
    /// <param name="hue">色相值，范围0到1。</param>
    /// <param name="s">饱和度值，范围0到1。</param>
    /// <param name="v">明度值，范围0到1。</param>
    public void SetHueByHSV(float hue, float s, float v)
    {
        float width = Mathf.Max(1, m_rectTransform.rect.width);
        Vector2 tagPos = new Vector2(hue * width - width / 2f, 0);
        m_tag.anchoredPosition = tagPos;

        m_color = Color.HSVToRGB(hue, 1, 1);

        m_staurationPanel.UpdateStauration(m_color, s, v);
    }

    /// <summary>
    /// 根据本地坐标设置当前色相颜色。
    /// </summary>
    private void SetColor(Vector2 localPoint)
    {
        float width = Mathf.Max(1, m_rectTransform.rect.width);
        localPoint.x = Mathf.Clamp(localPoint.x, -width / 2, width / 2);
        localPoint.y = 0;
        m_tag.anchoredPosition = localPoint;

        float normalizedX = (localPoint.x + width / 2) / width;
        int x = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (m_hueSprite.texture.width - 1)), 0, m_hueSprite.texture.width - 1);

        m_color = m_hueSprite.texture.GetPixel(x, 0);
        m_staurationPanel.UpdateStauration(m_color);
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
