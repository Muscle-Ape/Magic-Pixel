using HQ.UIManager;
using SuperScrollView;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPPetsView")]
public class MPPetsView : AWindow
{
    /// <summary>
    /// 详情区最多展示的奖励数量，需要和 prefab 中 Award1-Award3 对齐。
    /// </summary>
    private const int MAX_REWARD_COUNT = 3;

    /// <summary>
    /// 详情区状态刷新间隔。列表倒计时由 Item 自己刷新。
    /// </summary>
    private const float REFRESH_INTERVAL = 1f;

    private enum PetsTabType
    {
        Pets,
        Food,
        Toys,
    }

    /// <summary>
    /// 返回按钮。
    /// </summary>
    [TransformPath("View/Up/BackBtn")]
    private Button m_backBtn;

    /// <summary>
    /// 设置按钮。
    /// </summary>
    [TransformPath("View/Up/SettingBtn")]
    private Button m_settingBtn;

    /// <summary>
    /// 宠物页签按钮。
    /// </summary>
    [TransformPath("View/TabPanel/Tab/PetsTab")]
    private Button m_petTabBtn;

    /// <summary>
    /// 食物页签按钮，当前暂无数据。
    /// </summary>
    [TransformPath("View/TabPanel/Tab/FoodTab")]
    private Button m_foodTabBtn;

    /// <summary>
    /// 玩具页签按钮，当前暂无数据。
    /// </summary>
    [TransformPath("View/TabPanel/Tab/ToysTab")]
    private Button m_toysTabBtn;

    /// <summary>
    /// 宠物页签选中标识。
    /// </summary>
    [TransformPath("View/TabPanel/Tab/PetsTab/Select")]
    private RectTransform m_petTabSelect;

    /// <summary>
    /// 食物页签选中标识。
    /// </summary>
    [TransformPath("View/TabPanel/Tab/FoodTab/Select")]
    private RectTransform m_foodTabSelect;

    /// <summary>
    /// 玩具页签选中标识。
    /// </summary>
    [TransformPath("View/TabPanel/Tab/ToysTab/Select")]
    private RectTransform m_toysTabSelect;

    /// <summary>
    /// 宠物/食物/玩具复用列表。
    /// </summary>
    [TransformPath("View/TabPanel/ContentGrid")]
    private LoopGridView m_contentGrid;

    /// <summary>
    /// 详情区宠物图标。
    /// </summary>
    [TransformPath("View/PetInfo/Pet")]
    private Image m_pet;

    /// <summary>
    /// 健康度进度条。
    /// </summary>
    [TransformPath("View/PetInfo/HealthCard/ProgressBg/ProgressFill")]
    private Image m_healthProgressFill;

    /// <summary>
    /// 健康度数值。
    /// </summary>
    [TransformPath("View/PetInfo/HealthCard/Value")]
    private TMP_Text m_healthProgressValue;

    /// <summary>
    /// 心情度进度条。
    /// </summary>
    [TransformPath("View/PetInfo/MoodCard/ProgressBg/ProgressFill")]
    private Image m_moodProgressFill;

    /// <summary>
    /// 心情度数值。
    /// </summary>
    [TransformPath("View/PetInfo/MoodCard/Value")]
    private TMP_Text m_moodProgressValue;

    /// <summary>
    /// 详情区奖励领取倒计时。
    /// </summary>
    [TransformPath("View/PetInfo/ProducingCard/TimeBg/TimetText")]
    private TMP_Text m_awardTimer;

    /// <summary>
    /// 详情区奖励卡片按钮，点击尝试领取当前选中宠物奖励。
    /// </summary>
    [TransformPath("View/PetInfo/ProducingCard")]
    private Button m_producingBtn;

    /// <summary>
    /// 宠物静态配置列表。
    /// </summary>
    private List<MPPetConfig> m_petConfigs;

    /// <summary>
    /// 当前页签类型。
    /// </summary>
    private PetsTabType m_currentTab;

    /// <summary>
    /// 当前详情区展示的宠物配置。
    /// </summary>
    private MPPetConfig m_selectedPetConfig;

    /// <summary>
    /// 详情区奖励节点缓存，最多三个。
    /// </summary>
    private Transform[] m_petInfoRewardNodes;

    /// <summary>
    /// 详情区刷新计时器，列表 Item 倒计时由各自脚本自行刷新。
    /// </summary>
    private float m_refreshTimer;

    /// <summary>
    /// LoopGridView 只能先 InitGridView，再 SetListItemCount，避免初始化前刷新导致空引用。
    /// </summary>
    private bool m_gridInitialized;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_petConfigs = MPDataManager.Instance.m_petsModel?.petConfigs ?? new List<MPPetConfig>();
        MPUser.instance.SyncPetRuntimeConfigs(m_petConfigs);

        CachePetInfoRewardNodes();
        RegisterUI();
        SelectDefaultPet();
        SwitchTab(PetsTabType.Pets);
        RefreshPetInfo();
    }

    public override void OnRelease()
    {
        if (m_backBtn != null)
            m_backBtn.onClick.RemoveListener(OnBackClick);
        if (m_settingBtn != null)
            m_settingBtn.onClick.RemoveListener(OnSettingClick);
        if (m_petTabBtn != null)
            m_petTabBtn.onClick.RemoveListener(OnPetTabClick);
        if (m_foodTabBtn != null)
            m_foodTabBtn.onClick.RemoveListener(OnFoodTabClick);
        if (m_toysTabBtn != null)
            m_toysTabBtn.onClick.RemoveListener(OnToysTabClick);
        if (m_producingBtn != null)
            m_producingBtn.onClick.RemoveListener(OnProducingClick);
    }

    private void Update()
    {
        m_refreshTimer += Time.deltaTime;
        if (m_refreshTimer < REFRESH_INTERVAL)
            return;

        m_refreshTimer = 0f;
        // 状态衰减每秒检查一次，但存档写入由 MPUser 内部控制最小间隔。
        MPUser.instance.ApplyPetStatusDecay(m_petConfigs);
        RefreshPetInfo();
    }

    /// <summary>
    /// 注册页面按钮事件，先移除再添加，避免重复绑定。
    /// </summary>
    private void RegisterUI()
    {
        if (m_backBtn != null)
        {
            m_backBtn.onClick.RemoveListener(OnBackClick);
            m_backBtn.onClick.AddListener(OnBackClick);
        }

        if (m_settingBtn != null)
        {
            m_settingBtn.onClick.RemoveListener(OnSettingClick);
            m_settingBtn.onClick.AddListener(OnSettingClick);
        }

        if (m_petTabBtn != null)
        {
            m_petTabBtn.onClick.RemoveListener(OnPetTabClick);
            m_petTabBtn.onClick.AddListener(OnPetTabClick);
        }

        if (m_foodTabBtn != null)
        {
            m_foodTabBtn.onClick.RemoveListener(OnFoodTabClick);
            m_foodTabBtn.onClick.AddListener(OnFoodTabClick);
        }

        if (m_toysTabBtn != null)
        {
            m_toysTabBtn.onClick.RemoveListener(OnToysTabClick);
            m_toysTabBtn.onClick.AddListener(OnToysTabClick);
        }

        if (m_producingBtn != null)
        {
            m_producingBtn.onClick.RemoveListener(OnProducingClick);
            m_producingBtn.onClick.AddListener(OnProducingClick);
        }
    }

    /// <summary>
    /// 切换页签并刷新列表数量。
    /// </summary>
    private void SwitchTab(PetsTabType tab)
    {
        m_currentTab = tab;
        RefreshTabState();

        int count = GetCurrentTabCount();
        if (!m_gridInitialized)
        {
            // 首次打开必须用 InitGridView，否则 LoopGridView 内部数据未准备好会报空。
            m_contentGrid.InitGridView(count, GetItemByRowColumn);
            m_gridInitialized = true;
        }
        else
        {
            m_contentGrid.SetListItemCount(count);
            m_contentGrid.RefreshAllShownItem();
        }
    }

    /// <summary>
    /// 获取当前页签应该显示的 Item 数量。
    /// </summary>
    private int GetCurrentTabCount()
    {
        switch (m_currentTab)
        {
            case PetsTabType.Pets:
                return m_petConfigs.Count;
            case PetsTabType.Food:
            case PetsTabType.Toys:
                // 食物和玩具功能暂未接入，先返回 0 保持页签可切换。
                return 0;
            default:
                return 0;
        }
    }

    /// <summary>
    /// LoopGridView 的 Item 获取回调。
    /// </summary>
    private LoopGridViewItem GetItemByRowColumn(LoopGridView view, int index, int row, int column)
    {
        if (m_currentTab != PetsTabType.Pets || index < 0 || index >= m_petConfigs.Count)
            return null;

        MPPetConfig config = m_petConfigs[index];
        LoopGridViewItem item = m_contentGrid.NewListViewItem("MPPetItem");
        MPPetItem petItem = item.GetComponent<MPPetItem>();
        if (petItem == null)
        {
            petItem = item.gameObject.AddComponent<MPPetItem>();
        }

        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            // Item 点击统一回到 View 处理，已解锁负责选中，未解锁预留弹窗入口。
            petItem.Initialize(OnPetItemClick);
        }

        MPPetRuntimeData runtimeData = MPUser.instance.GetPetRuntimeData(config.ID);
        petItem.Refresh(config, runtimeData, m_selectedPetConfig != null && m_selectedPetConfig.ID == config.ID);
        return item;
    }

    /// <summary>
    /// 宠物 Item 点击回调。已解锁宠物执行选中，未解锁宠物进入解锁提示入口。
    /// </summary>
    private void OnPetItemClick(MPPetConfig config)
    {
        if (config == null)
            return;

        if (!MPUser.instance.PetIsUnlock(config.ID))
        {
            OnLockedPetItemClick(config);
            return;
        }

        MPUser.instance.SetSelectedPet(config.ID);
        m_selectedPetConfig = config;
        RefreshPetInfo();
        m_contentGrid.RefreshAllShownItem();
    }

    /// <summary>
    /// 未解锁宠物点击入口，后续在这里接入解锁确认弹窗。
    /// </summary>
    private void OnLockedPetItemClick(MPPetConfig config)
    {
        // TODO: 后续接入解锁确认弹窗。
    }

    /// <summary>
    /// 进入页面时恢复上次选中的宠物，没有可用记录时默认选中第一只已解锁宠物。
    /// </summary>
    private void SelectDefaultPet()
    {
        string selectedId = MPUser.instance.GetSelectedPetId();
        m_selectedPetConfig = FindPetConfig(selectedId);

        if (m_selectedPetConfig != null && MPUser.instance.PetIsUnlock(m_selectedPetConfig.ID))
            return;

        m_selectedPetConfig = m_petConfigs.Find(item => item != null && MPUser.instance.PetIsUnlock(item.ID));
        if (m_selectedPetConfig != null)
        {
            MPUser.instance.SetSelectedPet(m_selectedPetConfig.ID);
        }
    }

    /// <summary>
    /// 刷新详情区宠物图标、健康度、心情度和奖励信息。
    /// </summary>
    private void RefreshPetInfo()
    {
        if (m_selectedPetConfig == null)
            return;

        MPPetRuntimeData data = MPUser.instance.GetPetRuntimeData(m_selectedPetConfig.ID);
        if (data == null)
            return;

        SetImageSprite(m_pet, m_selectedPetConfig.Icon);
        SetProgress(m_healthProgressFill, data.health / 100f);
        SetProgress(m_moodProgressFill, data.mood / 100f);

        if (m_healthProgressValue != null)
        {
            m_healthProgressValue.text = $"{Mathf.RoundToInt(data.health)}/100";
        }
        if (m_moodProgressValue != null)
        {
            m_moodProgressValue.text = $"{Mathf.RoundToInt(data.mood)}/100";
        }
        if (m_awardTimer != null)
        {
            int remainingSeconds = MPUser.instance.GetPetRewardRemainingSeconds(m_selectedPetConfig);
            m_awardTimer.text = remainingSeconds <= 0 ? "Ready" : MPPetItem.FormatTime(remainingSeconds);
        }

        RefreshPetInfoRewards(m_selectedPetConfig);
    }

    /// <summary>
    /// 根据配置奖励数量刷新详情区奖励节点。
    /// </summary>
    private void RefreshPetInfoRewards(MPPetConfig config)
    {
        if (m_petInfoRewardNodes == null)
            return;

        int rewardCount = Mathf.Min(config.Rewards.Count, MAX_REWARD_COUNT);
        for (int i = 0; i < m_petInfoRewardNodes.Length; i++)
        {
            Transform rewardNode = m_petInfoRewardNodes[i];
            if (rewardNode == null)
                continue;

            bool active = i < rewardCount;
            rewardNode.gameObject.SetActive(active);
            if (!active)
                continue;

            SetReward(rewardNode, config.Rewards[i]);
        }
    }

    /// <summary>
    /// 设置单个详情区奖励节点。
    /// </summary>
    private void SetReward(Transform rewardNode, MPPetRewardConfig reward)
    {
        if (reward == null)
            return;

        Transform icon = rewardNode.Find("Icon");
        if (icon != null)
        {
            Image image = icon.GetComponent<Image>();
            if (image != null)
            {
                SetImageSprite(image, reward.Icon);
            }

            SetText(icon, GetRewardShortName(reward.Type));
        }

        Transform count = rewardNode.Find("Count");
        if (count != null)
        {
            SetText(count, reward.Count.ToString());
        }
    }

    /// <summary>
    /// 缓存详情区奖励节点，兼容两种可能的 prefab 层级。
    /// </summary>
    private void CachePetInfoRewardNodes()
    {
        m_petInfoRewardNodes = new Transform[MAX_REWARD_COUNT];
        for (int i = 0; i < MAX_REWARD_COUNT; i++)
        {
            string nodeName = $"Award{i + 1}";
            m_petInfoRewardNodes[i] = FindViewTransform($"View/PetInfo/ProducingCard/Awards/{nodeName}");
            if (m_petInfoRewardNodes[i] == null)
            {
                m_petInfoRewardNodes[i] = FindViewTransform($"View/PetInfo/ProducingCard/{nodeName}");
            }
        }
    }

    /// <summary>
    /// 刷新页签选中状态。
    /// </summary>
    private void RefreshTabState()
    {
        SetActive(m_petTabSelect, m_currentTab == PetsTabType.Pets);
        SetActive(m_foodTabSelect, m_currentTab == PetsTabType.Food);
        SetActive(m_toysTabSelect, m_currentTab == PetsTabType.Toys);
    }

    private void OnBackClick()
    {
        DestroyWindow();
    }

    private void OnSettingClick()
    {
    }

    private void OnPetTabClick()
    {
        SwitchTab(PetsTabType.Pets);
    }

    private void OnFoodTabClick()
    {
        SwitchTab(PetsTabType.Food);
    }

    private void OnToysTabClick()
    {
        SwitchTab(PetsTabType.Toys);
    }

    /// <summary>
    /// 点击详情区奖励卡片，奖励已完成时领取并重新开始倒计时。
    /// </summary>
    private void OnProducingClick()
    {
        if (m_selectedPetConfig == null)
            return;

        if (MPUser.instance.ClaimPetReward(m_selectedPetConfig))
        {
            RefreshPetInfo();
        }
    }

    private MPPetConfig FindPetConfig(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return m_petConfigs.Find(item => item != null && item.ID == id);
    }

    private Transform FindViewTransform(string path)
    {
        return string.IsNullOrEmpty(path) ? null : transform.Find(path);
    }

    private void SetActive(Component component, bool active)
    {
        if (component != null && component.gameObject.activeSelf != active)
        {
            component.gameObject.SetActive(active);
        }
    }

    private void SetProgress(Image image, float progress)
    {
        if (image == null)
            return;

        progress = Mathf.Clamp01(progress);
        // 进度条使用 Image.fillAmount，不修改 localScale，避免影响 prefab 原始布局。
        image.fillAmount = progress;
    }

    /// <summary>
    /// 通过项目资源加载封装加载图片，失败时保留 prefab 原有占位图。
    /// </summary>
    private void SetImageSprite(Image image, string location)
    {
        if (image == null || string.IsNullOrEmpty(location))
            return;

        try
        {
            Sprite sprite = MPLoad.Load<Sprite>(location);
            if (sprite != null)
            {
                image.sprite = sprite;
            }
        }
        catch (Exception)
        {
        }
    }

    private void SetText(Transform target, string value)
    {
        TMP_Text tmpText = target.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = value;
            return;
        }

        Text text = target.GetComponent<Text>();
        if (text != null)
        {
            text.text = value;
        }
    }

    /// <summary>
    /// 美术资源未接入时使用的奖励类型简写。
    /// </summary>
    private string GetRewardShortName(string rewardType)
    {
        if (string.IsNullOrEmpty(rewardType))
            return string.Empty;

        switch (rewardType)
        {
            case "coin":
                return "C";
            case "light":
                return "L";
            case "paw":
                return "P";
            case "leaf":
                return "Leaf";
            default:
                return rewardType;
        }

    }
}
