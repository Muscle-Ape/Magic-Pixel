using DG.Tweening;
using HQ.UIManager;
using UnityEngine;

public partial class MPGameView
{
    /// <summary>
    /// 切换模式移动的距离
    /// </summary>
    private float m_modeSwitchDistance;

    /// <summary>
    /// 注册界面按钮事件。
    /// </summary>
    private void RegisterUI()
    {
        m_modeSwitchDistance = (m_modeSwitchFrame.transform as RectTransform).rect.width / 4;

        m_modeSwitchFrame.onClick.AddListener(OnModeSwitchClick);
        m_backBtn.onClick.AddListener(OnBackClick);
        if (m_settingBtn != null)
        {
            m_settingBtn.onClick.AddListener(OnSettingClick);
        }

        if (m_hintPropBtn != null)
        {
            m_hintPropBtn.onClick.AddListener(OnHintPropClick);
        }

        if (m_loveRecoverPropBtn != null)
        {
            m_loveRecoverPropBtn.onClick.AddListener(OnLoveRecoverPropClick);
        }

        RefreshPropButtons();

        RefreshUI();

        m_titleText.text = "Level " + (m_index + 1).ToString();
    }

    private void RefreshUI()
    {
        m_coinText.text = MPUser.instance.GetCoins().ToString();
        m_diamondText.text = MPUser.instance.GetDiamond().ToString();
    }

    /// <summary>
    /// 刷新提示道具和生命恢复道具的数量显示。
    /// </summary>
    private void RefreshPropButtons()
    {
        if (m_hintPropBtn == null || m_hintPropCountText == null || m_loveRecoverPropBtn == null || m_loveRecoverPropCountText == null)
            return;

        int hintCount = MPUser.instance.GetHintProps();
        int recoverCount = MPUser.instance.GetLoveRecoverProps();

        m_hintPropCountText.text = hintCount.ToString();
        m_loveRecoverPropCountText.text = recoverCount.ToString();
    }

    /// <summary>
    /// 扣除生命值。
    /// </summary>
    private void SubLoves()
    {
        if (m_isCustomLevel)
            return;

        m_lovesCount = Mathf.Max(0, m_lovesCount - 1);
        GameObject love = m_loves[m_lovesCount];
        love.transform.DOKill();
        love.transform.localScale = Vector3.one;
        love.SetActive(false);

        SaveProgressCache();
        RefreshPropButtons();

        if (m_lovesCount <= 0)
        {
            OpenFailPop();
        }
    }

    /// <summary>
    /// 恢复生命值。
    /// </summary>
    private void AddLoves()
    {
        if (m_isCustomLevel)
            return;

        if (m_lovesCount == m_loves.Count)
            return;

        GameObject love = m_loves[m_lovesCount];
        love.transform.DOKill();
        love.transform.localScale = Vector3.zero;
        love.SetActive(true);
        love.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetLink(love);
        m_lovesCount++;

        SaveProgressCache();
        RefreshPropButtons();
    }

    /// <summary>
    /// 判断当前关卡是否还有未完成的格子。
    /// </summary>
    /// <returns>存在未完成格子返回true，否则返回false。</returns>
    private bool HasUncompletedBlock()
    {
        if (m_blocks == null)
            return false;

        for (int i = 0; i < m_blocks.Count; i++)
        {
            if (!m_blocks[i].completed)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 自动完成一个可提示的格子，并同步触发行列完成检查。
    /// </summary>
    private void AutoCompleteOneBlock()
    {
        MPGameBlock block = GetHintBlock();
        if (block == null)
            return;

        if (block.isFill)
        {
            block.Fill();
        }
        else
        {
            block.Blank();
        }

        block.Disable();
        block.PlayHintAnimation();
        Check(block);
    }

    /// <summary>
    /// 获取提示道具本次要自动完成的格子，优先选择需要填充的未完成格子。
    /// </summary>
    /// <returns>可自动完成的格子，没有可用格子时返回null。</returns>
    private MPGameBlock GetHintBlock()
    {
        if (m_blocks == null)
            return null;

        for (int i = 0; i < m_blocks.Count; i++)
        {
            if (!m_blocks[i].completed && m_blocks[i].isFill)
            {
                return m_blocks[i];
            }
        }

        for (int i = 0; i < m_blocks.Count; i++)
        {
            if (!m_blocks[i].completed)
            {
                return m_blocks[i];
            }
        }

        return null;
    }

    /// <summary>
    /// 点击提示道具按钮，消耗一个提示道具并自动完成一个格子。
    /// </summary>
    private void OnHintPropClick()
    {
        if (m_isCustomLevel)
        {
            return;
        }

        if (!HasUncompletedBlock())
        {
            RefreshPropButtons();
            return;
        }

        if (!MPUser.instance.UseHintProp())
        {
            RefreshPropButtons();
            return;
        }

        AutoCompleteOneBlock();
        SaveProgressCache();
        RefreshPropButtons();
    }

    /// <summary>
    /// 点击生命恢复道具按钮，消耗一个恢复道具并恢复一颗生命。
    /// </summary>
    private void OnLoveRecoverPropClick()
    {
        if (m_isCustomLevel || m_lovesCount >= m_loves.Count)
        {
            RefreshPropButtons();
            return;
        }

        if (!MPUser.instance.UseLoveRecoverProp())
        {
            RefreshPropButtons();
            return;
        }

        AddLoves();
    }

    /// <summary>
    /// 打开游戏失败弹窗。
    /// </summary>
    private void OpenFailPop()
    {
        if (m_isCustomLevel || m_isFailPopShowing || m_hasCompleted)
            return;

        m_isFailPopShowing = true;
        MPGameFailPopUIMsgData data = new MPGameFailPopUIMsgData()
        {
            exitAction = OnFailExitClick,
            replayAction = OnFailReplayClick,
            restoreLifeAction = OnFailRestoreLifeClick,
        };

        UIManager.Inst.ShowWindow<MPGameFailPop>(data, true, UILayer.Top);
    }

    /// <summary>
    /// 失败弹窗中点击退出当前游戏。
    /// </summary>
    private void OnFailExitClick()
    {
        m_isFailPopShowing = false;
        m_hasCompleted = true;
        ClearProgressCache();
        DestroyWindow();
        m_refreshAction?.Invoke();
    }

    /// <summary>
    /// 失败弹窗中点击重玩当前关卡。
    /// </summary>
    private void OnFailReplayClick()
    {
        m_isFailPopShowing = false;
        m_hasCompleted = true;
        ClearProgressCache();

        MPGameViewUIMsgData data = new MPGameViewUIMsgData()
        {
            blockInfo = m_blockInfo,
            index = m_index,
            refresh = m_refreshAction,
        };

        DestroyWindow();
        UIManager.Inst.ShowWindow<MPGameView>(data);
    }

    /// <summary>
    /// 失败弹窗中点击恢复一点生命值。
    /// </summary>
    /// <returns>成功恢复生命值返回true，弹窗会关闭。</returns>
    private bool OnFailRestoreLifeClick()
    {
        if (m_lovesCount >= m_loves.Count)
        {
            RefreshPropButtons();
            return false;
        }

        if (!MPUser.instance.UseLoveRecoverProp())
        {
            RefreshPropButtons();
            return false;
        }

        AddLoves();
        m_isFailPopShowing = false;
        return true;
    }

    /// <summary>
    /// 切换模式。
    /// </summary>
    private void OnModeSwitchClick()
    {
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);

        m_isFillMode = !m_isFillMode;

        m_modeSwitchTween?.Kill();
        m_modeSwitchTween = (m_modeSwitchBtn.transform as RectTransform).DOAnchorPosX(m_isFillMode ? m_modeSwitchDistance : -m_modeSwitchDistance, 0.1f).SetEase(Ease.Linear);

        m_modeSwitchFill.gameObject.SetActive(m_isFillMode);
        m_modeSwitchBlank.gameObject.SetActive(!m_isFillMode);

        for (int i = 0; i < m_blocks.Count; i++)
        {
            m_blocks[i].SetBlankHit(!m_isFillMode);
        }
    }

    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }

    private void OnBackClick()
    {
        SaveProgressCache();
        DestroyWindow();

        MPAudioManager.Instance.PlayBGM(MPMusic.MPBGMMain);
    }
}
