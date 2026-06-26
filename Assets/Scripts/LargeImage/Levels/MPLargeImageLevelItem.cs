using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MPLargeImageLevelItem : MonoBehaviour
{
    /// <summary>
    /// 通关后的图片
    /// </summary>
    private Image m_pixel;

    /// <summary>
    /// 解锁未通关状态
    /// </summary>
    private GameObject m_unlock;

    /// <summary>
    /// 未解锁状态
    /// </summary>
    private GameObject m_lock;

    /// <summary>
    /// 背景色
    /// </summary>
    private GameObject m_color;

    /// <summary>
    /// 关卡点击按钮
    /// </summary>
    private Button m_levelBtn;

    /// <summary>
    /// 关卡名称文本
    /// </summary>
    private TMP_Text m_nameText;

    /// <summary>
    /// MainLevel Data
    /// </summary>
    private MPLargeImageBlockInfo m_data;

    /// <summary>
    /// 当前关卡下标
    /// </summary>
    private int m_index;

    /// <summary>
    /// 标记是否解锁
    /// </summary>
    private bool m_isUnlock;

    /// <summary>
    /// 刷新页面回调
    /// </summary>
    private Action m_refresh;

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize(Action refresh)
    {
        m_refresh = refresh;

        m_pixel = transform.Find("Status/Completed/Pixel").GetComponent<Image>();
        m_unlock = transform.Find("Status/Unlock").gameObject;
        m_lock = transform.Find("Status/Lock").gameObject;
        m_color = transform.Find("Status/Color").gameObject;
        m_levelBtn = transform.Find("Btn").GetComponent<Button>();
        m_nameText = transform.Find("Name").GetComponent<TMP_Text>();

        m_levelBtn.onClick.AddListener(OnLevelClick);
    }

    /// <summary>
    /// 刷新
    /// </summary>
    public void Refresh(MPLargeImageBlockInfo data, int index)
    {
        m_data = data;
        m_index = index;

        // 更新关卡名称
        m_nameText.text = m_data.Name;

        // 刷新显示状态
        // 1、是否解锁
        m_isUnlock = MPUser.instance.MainLevelIsUnlock(m_data.ID);
        if (!m_isUnlock)
        {
            m_pixel.gameObject.SetActive(false);
            m_color.SetActive(true);
            m_unlock.SetActive(false);
            m_lock.SetActive(true);
        }
        else
        {
            // 2、如果解锁了是否已经通关
            bool isPass = MPUser.instance.MainLevelIsPass(m_data.ID);
            if (isPass)
            {
                m_pixel.gameObject.SetActive(true);
                m_color.SetActive(false);
                m_unlock.SetActive(false);
                m_lock.SetActive(false);

                m_pixel.sprite = MPLoad.Load<Sprite>("icon_" + m_data.ID);
            }
            else
            {
                m_pixel.gameObject.SetActive(false);
                m_color.SetActive(true);
                m_unlock.SetActive(true);
                m_lock.SetActive(false);
            }
        }
    }

    private void OnLevelClick()
    {
        if (m_isUnlock)
        {

        }
    }
}
