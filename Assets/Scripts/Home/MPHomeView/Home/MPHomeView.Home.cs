using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class MPHomeView
{
    private const float PET_TIP_DURATION = 0.2f;
    private const float PET_TIP_VISIBLE_TIME = 3f;
    private const float PET_TIP_ITEM_GAP = 12f;
    private const string HOME_REWARD_COMPLETED_TEXT = "Completed!!!";

    [TransformPath("View/Center/Home")]
    private RectTransform m_homePage;

    [TransformPath("View/Center/Home/Widgets/NewGameBtn")]
    private Button m_newGameBtn;

    [TransformPath("View/Center/Home/Widgets/ThreeDBtn")]
    private Button m_threeDBtn;

    [TransformPath("View/Center/Home/Widgets/RewardBtn")]
    private Button m_rewardBtn;

    [TransformPath("View/Center/Home/Widgets/RewardBtn/Countdown")]
    private TMP_Text m_rewardCountdownText;

    [TransformPath("View/Center/Home/Pet")]
    private RectTransform m_petRoot;

    [TransformPath("View/Center/Home/Pet/Pets")]
    private ScrollRect m_petScrollRect;

    [TransformPath("View/Center/Home/Pet/Pets/Viewport/Content")]
    private RectTransform m_petContent;

    [TransformPath("View/Center/Home/Pet/Pet")]
    private Image m_mainPetImage;

    [TransformPath("View/Center/Home/Pet/Name")]
    private TMP_Text m_petNameText;

    [TransformPath("View/Center/Home/Pet/Skill")]
    private TMP_Text m_petOptionText;

    [TransformPath("View/Center/Home/Pet/UnlockedCount")]
    private TMP_Text m_unlockedCountText;

    [TransformPath("View/Center/Home/Pet/Count/Text")]
    private TMP_Text m_petCountText;

    [TransformPath("View/Center/Home/Pet/Count/Mask/Fill")]
    private Image m_petProgressFill;

    [TransformPath("View/Center/Home/Pet/Tip")]
    private RectTransform m_petTip;

    [TransformPath("View/Center/Home/Pet/Tip/Text")]
    private TMP_Text m_petTipText;

    private Tween m_petTipTween;
    private Coroutine m_rewardCountdownCoroutine;
    private EventTrigger m_petScrollEventTrigger;
    private EventTrigger.Entry m_petScrollBeginDragEntry;
    private readonly List<MPPetItem> m_homePetItems = new List<MPPetItem>();
    private List<MPPetConfig> m_petConfigs;

    private void InitializeHomePage()
    {
        InitializeHomePets();
        StartHomeRewardCountdown();
    }

    private void RefreshHomePage()
    {
        RefreshHomePets();
        StartHomeRewardCountdown();
    }

    private void BlurHomePage()
    {
        StopHomeRewardCountdown();
    }

    private void ReleaseHomePage()
    {
        StopHomeRewardCountdown();
        KillPetTipTween();
    }

    private void RegisterHomeListeners()
    {
        m_newGameBtn.onClick.AddListener(OnNewGameClick);
        m_threeDBtn.onClick.AddListener(OnThreeDClick);
        m_rewardBtn.onClick.AddListener(OnHomeRewardClick);
        RegisterPetScrollBeginDrag();
    }

    private void UnregisterHomeListeners()
    {
        if (m_newGameBtn != null)
            m_newGameBtn.onClick.RemoveListener(OnNewGameClick);
        if (m_threeDBtn != null)
            m_threeDBtn.onClick.RemoveListener(OnThreeDClick);
        if (m_rewardBtn != null)
            m_rewardBtn.onClick.RemoveListener(OnHomeRewardClick);
        UnregisterPetScrollBeginDrag();
    }

    private void RegisterPetScrollBeginDrag()
    {
        if (m_petScrollRect == null)
            return;

        UnregisterPetScrollBeginDrag();
        m_petScrollEventTrigger = m_petScrollRect.GetComponent<EventTrigger>();
        if (m_petScrollEventTrigger == null)
            m_petScrollEventTrigger = m_petScrollRect.gameObject.AddComponent<EventTrigger>();
        if (m_petScrollEventTrigger.triggers == null)
            m_petScrollEventTrigger.triggers = new List<EventTrigger.Entry>();

        m_petScrollBeginDragEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.BeginDrag
        };
        m_petScrollBeginDragEntry.callback.AddListener(OnPetListBeginDrag);
        m_petScrollEventTrigger.triggers.Add(m_petScrollBeginDragEntry);
    }

    private void UnregisterPetScrollBeginDrag()
    {
        if (m_petScrollBeginDragEntry != null)
        {
            m_petScrollBeginDragEntry.callback.RemoveListener(OnPetListBeginDrag);
            if (m_petScrollEventTrigger != null && m_petScrollEventTrigger.triggers != null)
                m_petScrollEventTrigger.triggers.Remove(m_petScrollBeginDragEntry);
        }

        m_petScrollBeginDragEntry = null;
        m_petScrollEventTrigger = null;
    }

    private void InitializeHomePets()
    {
        HidePetTip(false);
        m_petConfigs = MPDataManager.Instance.m_petsModel?.petConfigs
            ?? new List<MPPetConfig>();
        MPUser.instance.SyncPetSelection(m_petConfigs);

        if (m_petScrollRect != null)
        {
            m_petScrollRect.horizontal = true;
            m_petScrollRect.vertical = false;
        }

        CreateHomePetItems();
        RefreshHomePets();
    }

    private void CreateHomePetItems()
    {
        if (m_petContent == null || m_petConfigs == null)
            return;

        for (int i = 0; i < m_homePetItems.Count; i++)
        {
            if (m_homePetItems[i] != null)
                Destroy(m_homePetItems[i].gameObject);
        }
        m_homePetItems.Clear();

        GameObject itemPrefab;
        try
        {
            itemPrefab = MPLoad.Load<GameObject>("MPPetItem", this);
        }
        catch (Exception exception)
        {
            Debug.LogError($"主页宠物 Item 加载失败：{exception.Message}");
            return;
        }

        for (int i = 0; i < m_petConfigs.Count; i++)
        {
            GameObject itemObject = Instantiate(itemPrefab, m_petContent, false);
            itemObject.name = $"MPPetItem_{m_petConfigs[i].ID}";

            MPPetItem item = itemObject.GetComponent<MPPetItem>();
            if (item == null)
                item = itemObject.AddComponent<MPPetItem>();

            item.Initialize(OnHomePetItemClick);
            m_homePetItems.Add(item);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_petContent);
    }

    /// <summary>
    /// 刷新解锁状态、选中态和主页宠物详情。
    /// </summary>
    private void RefreshHomePets()
    {
        if (m_petConfigs == null)
            return;

        MPUser.instance.SyncPetSelection(m_petConfigs);
        string selectedPetId = MPUser.instance.GetSelectedPetId();
        for (int i = 0; i < m_homePetItems.Count && i < m_petConfigs.Count; i++)
        {
            MPPetConfig config = m_petConfigs[i];
            bool unlocked = MPUser.instance.PetIsUnlock(config.ID);
            m_homePetItems[i].Refresh(
                config,
                unlocked,
                unlocked && config.ID == selectedPetId);
        }

        RefreshHomePetInfo(selectedPetId);
    }

    private void OnHomePetItemClick(MPPetConfig config)
    {
        if (config == null)
            return;

        MPPetItem item = FindHomePetItem(config.ID);
        if (!MPUser.instance.PetIsUnlock(config.ID))
        {
            ShowPetUnlockTip(item, config);
            return;
        }

        HidePetTip(true);
        if (MPUser.instance.GetSelectedPetId() == config.ID)
            return;

        MPUser.instance.SetSelectedPet(config.ID);
        RefreshHomePets();
    }

    private MPPetItem FindHomePetItem(string petId)
    {
        for (int i = 0; i < m_homePetItems.Count; i++)
        {
            MPPetItem item = m_homePetItems[i];
            if (item != null && item.Config != null && item.Config.ID == petId)
                return item;
        }

        return null;
    }

    private void RefreshHomePetInfo(string selectedPetId)
    {
        MPPetConfig selectedConfig = m_petConfigs.Find(
            config => config != null && config.ID == selectedPetId);
        if (selectedConfig != null)
        {
            if (m_petNameText != null)
                m_petNameText.text = selectedConfig.Name;
            if (m_petOptionText != null)
                m_petOptionText.text = selectedConfig.OptionText;
            SetMainPetSprite(selectedConfig);
        }

        int unlockedCount = 0;
        for (int i = 0; i < m_petConfigs.Count; i++)
        {
            if (m_petConfigs[i] != null && MPUser.instance.PetIsUnlock(m_petConfigs[i].ID))
                unlockedCount++;
        }

        if (m_unlockedCountText != null)
            m_unlockedCountText.text = "Unlocked Count";
        if (m_petCountText != null)
            m_petCountText.text = $"{unlockedCount}/{m_petConfigs.Count}";
        if (m_petProgressFill != null)
        {
            m_petProgressFill.type = Image.Type.Filled;
            m_petProgressFill.fillMethod = Image.FillMethod.Horizontal;
            m_petProgressFill.fillOrigin = 0;
            m_petProgressFill.fillAmount = m_petConfigs.Count == 0
                ? 0f
                : (float)unlockedCount / m_petConfigs.Count;
        }
    }

    private void SetMainPetSprite(MPPetConfig config)
    {
        if (m_mainPetImage == null || config == null)
            return;

        Sprite sprite = TryLoadSprite($"{config.Icon}_main");
        if (sprite == null)
            sprite = TryLoadSprite(config.Icon);
        if (sprite == null)
            return;

        m_mainPetImage.sprite = sprite;
        m_mainPetImage.preserveAspect = true;
        m_mainPetImage.color = Color.white;
    }

    private Sprite TryLoadSprite(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        try
        {
            return MPLoad.Load<Sprite>(location, this);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ShowPetUnlockTip(MPPetItem item, MPPetConfig config)
    {
        if (item == null || item.RectTransform == null || m_petTip == null || m_petRoot == null)
            return;

        KillPetTipTween();
        if (m_petScrollRect != null)
        {
            m_petScrollRect.StopMovement();
            m_petScrollRect.velocity = Vector2.zero;
        }

        if (m_petTipText != null)
            m_petTipText.text = config.UnlockText;

        m_petTip.gameObject.SetActive(true);
        m_petTip.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_petTip);

        RectTransform itemRect = item.RectTransform;
        Vector3 itemBottomWorld = itemRect.TransformPoint(new Vector3(
            itemRect.rect.center.x,
            itemRect.rect.yMin,
            0f));
        Vector3 itemBottomLocal = m_petRoot.InverseTransformPoint(itemBottomWorld);

        float halfTipWidth = m_petTip.rect.width * 0.5f;
        float minX = m_petRoot.rect.xMin + halfTipWidth + 12f;
        float maxX = m_petRoot.rect.xMax - halfTipWidth - 12f;
        float tipX = minX <= maxX
            ? Mathf.Clamp(itemBottomLocal.x, minX, maxX)
            : 0f;
        m_petTip.anchoredPosition = new Vector2(
            tipX,
            itemBottomLocal.y - PET_TIP_ITEM_GAP);

        m_petTip.localScale = Vector3.zero;
        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject)
            .Append(m_petTip.DOScale(1f, PET_TIP_DURATION).SetEase(Ease.OutBack))
            .AppendInterval(PET_TIP_VISIBLE_TIME)
            .Append(m_petTip.DOScale(0f, PET_TIP_DURATION).SetEase(Ease.InBack));
        m_petTipTween = sequence;
        sequence.OnComplete(() =>
        {
            if (m_petTipTween == sequence)
                m_petTipTween = null;

            if (m_petTip != null)
            {
                m_petTip.localScale = Vector3.zero;
                m_petTip.gameObject.SetActive(false);
            }
        });
        sequence.OnKill(() =>
        {
            if (m_petTipTween == sequence)
                m_petTipTween = null;
        });
    }

    private void HidePetTip(bool animated)
    {
        if (m_petTip == null)
            return;

        KillPetTipTween();
        if (!m_petTip.gameObject.activeSelf
            || !animated
            || m_petTip.localScale.sqrMagnitude <= 0.0001f)
        {
            m_petTip.localScale = Vector3.zero;
            m_petTip.gameObject.SetActive(false);
            return;
        }

        Tween tween = m_petTip.DOScale(0f, PET_TIP_DURATION)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .SetLink(gameObject);
        m_petTipTween = tween;
        tween.OnComplete(() =>
        {
            if (m_petTipTween == tween)
                m_petTipTween = null;

            if (m_petTip != null)
            {
                m_petTip.localScale = Vector3.zero;
                m_petTip.gameObject.SetActive(false);
            }
        });
        tween.OnKill(() =>
        {
            if (m_petTipTween == tween)
                m_petTipTween = null;
        });
    }

    /// <summary>
    /// 每次播放 Tip 动画前停止上一次展示、延时或关闭动画。
    /// </summary>
    private void KillPetTipTween()
    {
        Tween previousTween = m_petTipTween;
        m_petTipTween = null;
        if (previousTween != null && previousTween.IsActive())
            previousTween.Kill();

        if (m_petTip != null)
            m_petTip.DOKill();
    }

    private void OnPetListBeginDrag(BaseEventData _)
    {
        HidePetTip(false);
    }

    private void StartHomeRewardCountdown()
    {
        StopHomeRewardCountdown();
        RefreshHomeRewardButton();
        m_rewardCountdownCoroutine = StartCoroutine(HomeRewardCountdownRoutine());
    }

    private void StopHomeRewardCountdown()
    {
        if (m_rewardCountdownCoroutine == null)
            return;

        StopCoroutine(m_rewardCountdownCoroutine);
        m_rewardCountdownCoroutine = null;
    }

    private IEnumerator HomeRewardCountdownRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1f);
        while (true)
        {
            yield return wait;
            RefreshHomeRewardButton();
        }
    }

    private void RefreshHomeRewardButton()
    {
        TimeSpan remaining = MPUser.instance.GetHomeRewardRemainingTime();
        bool completed = remaining <= TimeSpan.Zero;
        // 倒计时期间仍允许点击，未到期分支后续可直接接入 Toast 提示。
        if (m_rewardBtn != null)
            m_rewardBtn.interactable = true;
        if (m_rewardCountdownText == null)
            return;

        if (completed)
        {
            m_rewardCountdownText.text = HOME_REWARD_COMPLETED_TEXT;
            return;
        }

        long totalSeconds = Math.Max(1L, (long)Math.Ceiling(remaining.TotalSeconds));
        long hours = totalSeconds / 3600L;
        long minutes = totalSeconds % 3600L / 60L;
        long seconds = totalSeconds % 60L;
        m_rewardCountdownText.text = $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    private void OnHomeRewardClick()
    {
        if (!MPUser.instance.TryClaimHomeReward())
        {
            RefreshHomeRewardButton();
            return;
        }

        RefreshCurrency();
        RefreshHomeRewardButton();
    }

    private void OnNewGameClick()
    {
        UIManager.Inst.ShowWindow<MPMainLevelView>();
    }

    private void OnThreeDClick()
    {
        MPThreeDIntegration.Open();
    }
}
