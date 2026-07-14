using HQ.UIManager;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MPMainLevelItem : MonoBehaviour
{
    /// <summary>
    /// 通关后的图片
    /// </summary>
    private Image m_pixel;

    /// <summary>
    /// 关卡下标框
    /// </summary>
    private Image m_levelIndexFrame;

    /// <summary>
    /// 当前关卡从1开始显示的下标文本。
    /// </summary>
    private TMP_Text m_levelIndexText;

    /// <summary>
    /// 通关星星显示节点。
    /// </summary>
    private GameObject m_stars;

    /// <summary>
    /// 通关星星文本数组，数量根据剩余生命显示。
    /// </summary>
    private GameObject[] m_starObj;

    /// <summary>
    /// 解锁未通关状态
    /// </summary>
    private GameObject m_unlock;

    /// <summary>
    /// 未解锁状态
    /// </summary>
    private GameObject m_lock;

    /// <summary>
    /// 关卡点击按钮
    /// </summary>
    private Button m_levelBtn;

    /// <summary>
    /// MainLevel Data
    /// </summary>
    private MPMainBlockInfo m_data;

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
    /// Item未解锁下标框图片资源
    /// </summary>
    private Sprite m_itemIndexFrameLockSpriteAsset;
    /// <summary>
    /// Item已解锁下标框图片资源
    /// </summary>
    private Sprite m_itemIndexFrameUnLockSpriteAsset;
    /// <summary>
    /// Item已通关下标框图片资源
    /// </summary>
    private Sprite m_itemIndexFramePassSpriteAsset;

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize(Action refresh, Sprite itemIndexFrameLockSpriteAsset, Sprite itemIndexFrameUnLockSpriteAsset, Sprite itemIndexFramePassSpriteAsset)
    {
        m_refresh = refresh;
        m_itemIndexFrameLockSpriteAsset = itemIndexFrameLockSpriteAsset;
        m_itemIndexFrameUnLockSpriteAsset = itemIndexFrameUnLockSpriteAsset;
        m_itemIndexFramePassSpriteAsset = itemIndexFramePassSpriteAsset;

        m_pixel = transform.Find("Completed/Pixel").GetComponent<Image>();
        m_levelIndexFrame = transform.Find("IndexFrame").GetComponent<Image>();
        Transform levelIndexTransform = transform.Find("IndexFrame/IndexText");
        if (levelIndexTransform != null)
        {
            m_levelIndexText = levelIndexTransform.GetComponent<TMP_Text>();
        }

        Transform starsTransform = transform.Find("Stars");
        if (starsTransform != null)
        {
            m_stars = starsTransform.gameObject;
            m_starObj = new GameObject[starsTransform.childCount];
            for (int i = 0; i < starsTransform.childCount; i++)
            {
                m_starObj[i] = starsTransform.GetChild(i).GetChild(0).gameObject;
            }
        }

        m_unlock = transform.Find("Unlock").gameObject;
        m_lock = transform.Find("Lock").gameObject;
        m_levelBtn = transform.Find("Btn").GetComponent<Button>();

        m_levelBtn.onClick.AddListener(OnLevelClick);
    }

    /// <summary>
    /// 刷新
    /// </summary>
    public void Refresh(MPMainBlockInfo data, int index)
    {
        m_data = data;
        m_index = index;

        if (m_levelIndexText != null)
        {
            m_levelIndexText.text = (m_index + 1).ToString();
        }

        // 刷新显示状态
        // 1、是否解锁
        m_isUnlock = MPUser.instance.MainLevelIsUnlock(m_data.ID);
        if (!m_isUnlock)
        {
            m_levelIndexFrame.sprite = m_itemIndexFrameLockSpriteAsset;
            m_pixel.gameObject.SetActive(false);
            RefreshStars(false, 0);
            m_unlock.SetActive(false);
            m_lock.SetActive(true);
        }
        else
        {
            // 2、如果解锁了是否已经通关
            bool isPass = MPUser.instance.MainLevelIsPass(m_data.ID);
            if (isPass)
            {
                m_levelIndexFrame.sprite = m_itemIndexFramePassSpriteAsset;
                m_pixel.gameObject.SetActive(true);
                m_unlock.SetActive(false);
                m_lock.SetActive(false);

                m_pixel.sprite = MPLoad.Load<Sprite>("icon_" + m_data.ID);
                RefreshStars(true, MPUser.instance.GetMainLevelStars(m_data.ID));
            }
            else
            {
                m_levelIndexFrame.sprite = m_itemIndexFrameUnLockSpriteAsset;
                m_pixel.gameObject.SetActive(false);
                RefreshStars(false, 0);
                m_unlock.SetActive(true);
                m_lock.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 刷新通关星星显示，未通关时隐藏整个星星节点。
    /// </summary>
    /// <param name="isPass">当前关卡是否已经通关。</param>
    /// <param name="stars">通关时剩余生命对应的星数。</param>
    private void RefreshStars(bool isPass, int stars)
    {
        if (m_stars == null || m_starObj == null)
            return;

        m_stars.SetActive(isPass);
        stars = Mathf.Clamp(stars, 0, m_starObj.Length);
        for (int i = 0; i < m_starObj.Length; i++)
        {
            if (m_starObj[i] != null)
            {
                m_starObj[i].SetActive(i < stars);
            }
        }
    }

    private void OnLevelClick()
    {
        if (m_isUnlock)
        {
            MPGameViewUIMsgData data = new MPGameViewUIMsgData()
            {
                blockInfo = m_data,
                index = m_index,
                refresh = m_refresh,
            };
            UIManager.Inst.ShowWindow<MPGameView>(data);
        }
    }
}
