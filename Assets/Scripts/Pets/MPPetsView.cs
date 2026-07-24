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
    /// 食物页签按钮。
    /// </summary>
    [TransformPath("View/TabPanel/Tab/FoodTab")]
    private Button m_foodTabBtn;

    /// <summary>
    /// 玩具页签按钮。
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
    /// 金币数量
    /// </summary>
    [TransformPath("View/Up/Coin/Count")]
    private TMP_Text m_coinText;

    /// <summary>
    /// 钻石数量
    /// </summary>
    [TransformPath("View/Up/Diamond/Count")]
    private TMP_Text m_diamondText;

    /// <summary>
    /// 宠物静态配置列表。
    /// </summary>
    private List<MPPetConfig> m_petConfigs;

    /// <summary>
    /// 食物配置列表，用于恢复健康度。
    /// </summary>
    private List<MPPetCareItemConfig> m_foodConfigs;

    /// <summary>
    /// 玩具配置列表，用于恢复心情度。
    /// </summary>
    private List<MPPetCareItemConfig> m_toyConfigs;

    /// <summary>
    /// 宠物页签实际展示列表：已解锁在前，未解锁在后，同状态内保留配置顺序。
    /// </summary>
    private List<MPPetConfig> m_petDisplayConfigs;

    /// <summary>
    /// 食物页签实际展示列表：已解锁在前，未解锁在后，同状态内保留配置顺序。
    /// </summary>
    private List<MPPetCareItemConfig> m_foodDisplayConfigs;

    /// <summary>
    /// 玩具页签实际展示列表：已解锁在前，未解锁在后，同状态内保留配置顺序。
    /// </summary>
    private List<MPPetCareItemConfig> m_toyDisplayConfigs;

    /// <summary>
    /// 当前页签类型。
    /// </summary>
    private PetsTabType m_currentTab;

    /// <summary>
    /// 当前详情区展示的宠物配置。
    /// </summary>
    private MPPetConfig m_selectedPetConfig;

    /// <summary>
    /// 当前选中的食物或玩具 ID。点击 Use 按钮后才会真正使用。
    /// </summary>
    private string m_selectedCareItemId;

    /// <summary>
    /// 当前选中的食物或玩具所属页签。
    /// </summary>
    private PetsTabType m_selectedCareItemTab;

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
        MPPetsModel petsModel = MPDataManager.Instance.m_petsModel;
        m_petConfigs = petsModel?.petConfigs ?? new List<MPPetConfig>();
        m_foodConfigs = petsModel?.foodConfigs ?? new List<MPPetCareItemConfig>();
        m_toyConfigs = petsModel?.toyConfigs ?? new List<MPPetCareItemConfig>();

        MPUser.instance.SyncPetRuntimeConfigs(m_petConfigs);
        MPUser.instance.SyncPetCareRuntimeConfigs(m_foodConfigs);
        MPUser.instance.SyncPetCareRuntimeConfigs(m_toyConfigs);

        CachePetInfoRewardNodes();
        RegisterUI();
        SelectDefaultPet();
        MPUser.instance.ApplyPetStatusDecay(m_petConfigs);
        RebuildAllDisplayConfigs();
        SwitchTab(PetsTabType.Pets);
        RefreshPetInfo();
    }

    public override void OnFocus(bool focus)
    {
        if (focus)
        {
            RefreshUI();
        }
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

    private void RefreshUI()
    {
        if (m_coinText != null)
        {
            m_coinText.text = MPUser.instance.GetCoins().ToString();
        }

        if (m_diamondText != null)
        {
            m_diamondText.text = MPUser.instance.GetDiamond().ToString();
        }
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
        // 按钮点击音效
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);

        if (m_currentTab != tab)
        {
            ClearCareItemSelection();
        }

        m_currentTab = tab;
        RefreshTabState();
        RebuildCurrentTabDisplayConfigs();

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

        MoveCurrentTabToDefaultItem(count);
    }

    /// <summary>
    /// 切换到宠物页时，默认定位到详情区当前展示的宠物；其他页签默认回到第一个 Item。
    /// </summary>
    private void MoveCurrentTabToDefaultItem(int count)
    {
        if (m_contentGrid == null || count <= 0)
            return;

        int targetIndex = 0;
        if (m_currentTab == PetsTabType.Pets && m_selectedPetConfig != null)
        {
            int selectedIndex = FindPetConfigIndex(m_selectedPetConfig.ID);
            if (selectedIndex >= 0)
            {
                targetIndex = selectedIndex;
            }
        }

        m_contentGrid.MovePanelToItemByIndex(targetIndex);
    }

    /// <summary>
    /// 食物/玩具解锁后会重排展示列表，这里把列表定位到刚选中的 Item。
    /// </summary>
    private void MoveCurrentTabToSelectedCareItem(string itemId)
    {
        if (m_contentGrid == null || string.IsNullOrEmpty(itemId))
            return;

        List<MPPetCareItemConfig> configs = GetCareConfigs(m_currentTab);
        int index = FindCareConfigIndex(configs, itemId);
        if (index >= 0)
        {
            m_contentGrid.MovePanelToItemByIndex(index);
        }
    }

    /// <summary>
    /// 重建全部页签的展示列表。展示顺序只影响 UI，不修改原始配置列表。
    /// </summary>
    private void RebuildAllDisplayConfigs()
    {
        m_petDisplayConfigs = BuildPetDisplayConfigs(m_petConfigs);
        m_foodDisplayConfigs = BuildCareDisplayConfigs(m_foodConfigs);
        m_toyDisplayConfigs = BuildCareDisplayConfigs(m_toyConfigs);
    }

    /// <summary>
    /// 重建当前页签的展示列表。解锁状态变化后调用，用于把新解锁 Item 移到未解锁 Item 前面。
    /// </summary>
    private void RebuildCurrentTabDisplayConfigs()
    {
        switch (m_currentTab)
        {
            case PetsTabType.Pets:
                m_petDisplayConfigs = BuildPetDisplayConfigs(m_petConfigs);
                break;
            case PetsTabType.Food:
                m_foodDisplayConfigs = BuildCareDisplayConfigs(m_foodConfigs);
                break;
            case PetsTabType.Toys:
                m_toyDisplayConfigs = BuildCareDisplayConfigs(m_toyConfigs);
                break;
        }
    }

    private List<MPPetConfig> BuildPetDisplayConfigs(List<MPPetConfig> configs)
    {
        List<MPPetConfig> result = new List<MPPetConfig>();
        AddPetDisplayConfigs(result, configs, true);
        AddPetDisplayConfigs(result, configs, false);
        return result;
    }

    private void AddPetDisplayConfigs(List<MPPetConfig> result, List<MPPetConfig> configs, bool unlockedOnly)
    {
        if (result == null || configs == null)
            return;

        for (int i = 0; i < configs.Count; i++)
        {
            MPPetConfig config = configs[i];
            bool unlocked = config != null && MPUser.instance.PetIsUnlock(config.ID);
            if (unlocked == unlockedOnly)
            {
                result.Add(config);
            }
        }
    }

    private List<MPPetCareItemConfig> BuildCareDisplayConfigs(List<MPPetCareItemConfig> configs)
    {
        List<MPPetCareItemConfig> result = new List<MPPetCareItemConfig>();
        AddCareDisplayConfigs(result, configs, true);
        AddCareDisplayConfigs(result, configs, false);
        return result;
    }

    private void AddCareDisplayConfigs(List<MPPetCareItemConfig> result, List<MPPetCareItemConfig> configs, bool unlockedOnly)
    {
        if (result == null || configs == null)
            return;

        for (int i = 0; i < configs.Count; i++)
        {
            MPPetCareItemConfig config = configs[i];
            bool unlocked = config != null && MPUser.instance.PetCareItemIsUnlock(config.ID);
            if (unlocked == unlockedOnly)
            {
                result.Add(config);
            }
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
                return m_petDisplayConfigs == null ? 0 : m_petDisplayConfigs.Count;
            case PetsTabType.Food:
                return m_foodDisplayConfigs == null ? 0 : m_foodDisplayConfigs.Count;
            case PetsTabType.Toys:
                return m_toyDisplayConfigs == null ? 0 : m_toyDisplayConfigs.Count;
            default:
                return 0;
        }
    }

    /// <summary>
    /// LoopGridView 的 Item 获取回调。
    /// </summary>
    private LoopGridViewItem GetItemByRowColumn(LoopGridView view, int index, int row, int column)
    {
        switch (m_currentTab)
        {
            case PetsTabType.Pets:
                return GetPetItem(index);
            case PetsTabType.Food:
                return GetCareItem(index, m_foodDisplayConfigs, "MPFoodItem");
            case PetsTabType.Toys:
                return GetCareItem(index, m_toyDisplayConfigs, "MPToyItem");
            default:
                return null;
        }
    }

    private LoopGridViewItem GetPetItem(int index)
    {
        if (m_petDisplayConfigs == null || index < 0 || index >= m_petDisplayConfigs.Count)
            return null;

        MPPetConfig config = m_petDisplayConfigs[index];
        if (config == null)
            return null;

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

    private LoopGridViewItem GetCareItem(int index, List<MPPetCareItemConfig> configs, string prefabName)
    {
        if (configs == null || index < 0 || index >= configs.Count)
            return null;

        MPPetCareItemConfig config = configs[index];
        if (config == null)
            return null;

        LoopGridViewItem item = m_contentGrid.NewListViewItem(prefabName);
        MPPetCareItem careItem = item.GetComponent<MPPetCareItem>();
        if (careItem == null)
        {
            careItem = item.gameObject.AddComponent<MPPetCareItem>();
        }

        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            careItem.Initialize(OnCareItemClick, OnCareItemUseClick);
        }

        MPPetCareRuntimeData runtimeData = MPUser.instance.GetPetCareRuntimeData(config.ID);
        bool selected = m_selectedCareItemTab == m_currentTab && m_selectedCareItemId == config.ID;
        // 使用按钮的可点击状态由数据层统一判断，包含解锁、数量、宠物有效性以及目标值是否已满。
        bool canUse = m_selectedPetConfig != null && MPUser.instance.PetCareItemCanUse(m_selectedPetConfig.ID, config);
        careItem.Refresh(config, runtimeData, selected, canUse);
        return item;
    }

    /// <summary>
    /// 只刷新当前可见的宠物 Item。普通选择不需要重建整个 Content，未显示的格子等滚动出来时会自动刷新。
    /// </summary>
    private void RefreshShownPetItem(string petId)
    {
        if (m_currentTab != PetsTabType.Pets || string.IsNullOrEmpty(petId) || m_contentGrid == null)
            return;

        int index = FindPetConfigIndex(petId);
        if (index < 0)
            return;

        LoopGridViewItem item = m_contentGrid.GetShownItemByItemIndex(index);
        if (item == null)
            return;

        MPPetItem petItem = item.GetComponent<MPPetItem>();
        if (petItem == null)
            return;

        MPPetConfig config = m_petDisplayConfigs[index];
        MPPetRuntimeData runtimeData = MPUser.instance.GetPetRuntimeData(config.ID);
        bool selected = m_selectedPetConfig != null && m_selectedPetConfig.ID == config.ID;
        petItem.Refresh(config, runtimeData, selected);
    }

    /// <summary>
    /// 只刷新当前可见的食物/玩具 Item。选择和使用道具时只改受影响的格子，真正解锁时才整体刷新。
    /// </summary>
    private void RefreshShownCareItem(PetsTabType tab, string itemId)
    {
        if (tab != m_currentTab || string.IsNullOrEmpty(itemId) || m_contentGrid == null)
            return;

        List<MPPetCareItemConfig> configs = GetCareConfigs(tab);
        int index = FindCareConfigIndex(configs, itemId);
        if (index < 0)
            return;

        LoopGridViewItem item = m_contentGrid.GetShownItemByItemIndex(index);
        if (item == null)
            return;

        MPPetCareItem careItem = item.GetComponent<MPPetCareItem>();
        if (careItem == null)
            return;

        MPPetCareItemConfig config = configs[index];
        MPPetCareRuntimeData runtimeData = MPUser.instance.GetPetCareRuntimeData(config.ID);
        bool selected = m_selectedCareItemTab == m_currentTab && m_selectedCareItemId == config.ID;
        bool canUse = m_selectedPetConfig != null && MPUser.instance.PetCareItemCanUse(m_selectedPetConfig.ID, config);
        careItem.Refresh(config, runtimeData, selected, canUse);
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
            if (MPUser.instance.PetIsUnlock(config.ID))
            {
                MPUser.instance.SetSelectedPet(config.ID);
                m_selectedPetConfig = config;
                RefreshPetInfo();
                RebuildCurrentTabDisplayConfigs();
                m_contentGrid.RefreshAllShownItem();
                MoveCurrentTabToDefaultItem(GetCurrentTabCount());
            }
            return;
        }

        string previousPetId = m_selectedPetConfig != null ? m_selectedPetConfig.ID : null;
        if (previousPetId == config.ID)
            return;

        MPUser.instance.SetSelectedPet(config.ID);
        m_selectedPetConfig = config;
        RefreshPetInfo();
        RefreshShownPetItem(previousPetId);
        RefreshShownPetItem(config.ID);
    }

    private void OnCareItemClick(MPPetCareItemConfig config)
    {
        if (config == null)
            return;

        if (!MPUser.instance.PetCareItemIsUnlock(config.ID))
        {
            OnLockedCareItemClick(config);
            if (MPUser.instance.PetCareItemIsUnlock(config.ID))
            {
                SelectCareItem(config);
                RebuildCurrentTabDisplayConfigs();
                m_contentGrid.RefreshAllShownItem();
                MoveCurrentTabToSelectedCareItem(config.ID);
            }
            return;
        }

        if (m_selectedPetConfig == null)
            return;

        string previousItemId = m_selectedCareItemId;
        PetsTabType previousItemTab = m_selectedCareItemTab;
        if (previousItemTab == m_currentTab && previousItemId == config.ID)
            return;

        SelectCareItem(config);
        RefreshShownCareItem(previousItemTab, previousItemId);
        RefreshShownCareItem(m_currentTab, config.ID);
    }

    private void OnCareItemUseClick(MPPetCareItemConfig config)
    {
        if (config == null)
            return;

        if (!MPUser.instance.PetCareItemIsUnlock(config.ID))
            return;

        if (m_selectedPetConfig == null)
            return;

        if (m_selectedCareItemTab != m_currentTab || m_selectedCareItemId != config.ID)
            return;

        // 先把当前宠物状态按时间补扣，再执行恢复，避免恢复值被旧时间差抵消。
        MPUser.instance.ApplyPetStatusDecay(m_petConfigs);
        if (MPUser.instance.UsePetCareItem(m_selectedPetConfig.ID, config))
        {
            RefreshPetInfo();
            RefreshShownCareItem(m_currentTab, config.ID);
        }
    }

    private void ClearCareItemSelection()
    {
        m_selectedCareItemId = null;
        m_selectedCareItemTab = PetsTabType.Pets;
    }

    private void SelectCareItem(MPPetCareItemConfig config)
    {
        if (config == null || !MPUser.instance.PetCareItemIsUnlock(config.ID))
            return;

        m_selectedCareItemId = config.ID;
        m_selectedCareItemTab = m_currentTab;
    }

    /// <summary>
    /// 未解锁宠物点击入口，后续在这里接入解锁确认弹窗。
    /// </summary>
    private void OnLockedPetItemClick(MPPetConfig config)
    {
        // TODO: 后续接入宠物解锁确认弹窗。
    }

    /// <summary>
    /// 未解锁食物/玩具点击入口，后续在这里接入解锁确认弹窗。
    /// </summary>
    private void OnLockedCareItemClick(MPPetCareItemConfig config)
    {
        // TODO: 后续接入食物/玩具解锁确认弹窗。
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
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
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

        List<MPPetRewardConfig> claimedRewards = new List<MPPetRewardConfig>(m_selectedPetConfig.Rewards);
        List<string> changedCareItemIds = new List<string>();
        if (MPUser.instance.ClaimPetReward(m_selectedPetConfig, changedCareItemIds))
        {
            RefreshUI();
            RefreshPetInfo();
            RefreshShownPetItem(m_selectedPetConfig.ID);
            RefreshChangedCareItems(changedCareItemIds);
            ShowPetRewardsPop(claimedRewards);
        }
    }

    /// <summary>
    /// 奖励已经在 MPUser 中发放完成，这里只打开领取展示弹窗。
    /// </summary>
    private void ShowPetRewardsPop(List<MPPetRewardConfig> rewards)
    {
        MPPetRewardsPopUIMsgData data = new MPPetRewardsPopUIMsgData()
        {
            rewards = rewards,
        };
        UIManager.Inst.ShowWindow<MPPetRewardsPop>(data, true, UILayer.Top);
    }

    /// <summary>
    /// 领取奖励可能会增加食物或玩具数量，只刷新当前可见且数量发生变化的格子。
    /// </summary>
    private void RefreshChangedCareItems(List<string> itemIds)
    {
        if (itemIds == null || itemIds.Count == 0)
            return;

        for (int i = 0; i < itemIds.Count; i++)
        {
            string itemId = itemIds[i];
            RefreshShownCareItem(PetsTabType.Food, itemId);
            RefreshShownCareItem(PetsTabType.Toys, itemId);
        }
    }

    private MPPetConfig FindPetConfig(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return m_petConfigs.Find(item => item != null && item.ID == id);
    }

    private int FindPetConfigIndex(string id)
    {
        if (string.IsNullOrEmpty(id) || m_petDisplayConfigs == null)
            return -1;

        for (int i = 0; i < m_petDisplayConfigs.Count; i++)
        {
            MPPetConfig config = m_petDisplayConfigs[i];
            if (config != null && config.ID == id)
                return i;
        }

        return -1;
    }

    private List<MPPetCareItemConfig> GetCareConfigs(PetsTabType tab)
    {
        switch (tab)
        {
            case PetsTabType.Food:
                return m_foodDisplayConfigs;
            case PetsTabType.Toys:
                return m_toyDisplayConfigs;
            default:
                return null;
        }
    }

    private int FindCareConfigIndex(List<MPPetCareItemConfig> configs, string id)
    {
        if (string.IsNullOrEmpty(id) || configs == null)
            return -1;

        for (int i = 0; i < configs.Count; i++)
        {
            MPPetCareItemConfig config = configs[i];
            if (config != null && config.ID == id)
                return i;
        }

        return -1;
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

        switch (rewardType.ToLowerInvariant())
        {
            case "coin":
                return "C";
            case "diamond":
            case "diamonds":
            case "gem":
            case "gems":
                return "D";
            case "light":
            case "hint":
            case "hint_prop":
                return "L";
            case "paw":
            case "love":
            case "life":
            case "love_recover":
            case "life_recover":
                return "P";
            case "leaf":
            case "food":
                return "Leaf";
            case "toy":
                return "Toy";
            default:
                return rewardType;
        }
    }
}
