using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class MPDataManager
{
    #region Singleton
    private static MPDataManager m_instance;
    private MPDataManager() { }
    public static MPDataManager Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new MPDataManager();
            }

            return m_instance;
        }
    }
    #endregion

    /// <summary>
    /// 主关卡数据。
    /// </summary>
    public MPMainLevelModel m_mainLevelModel;

    /// <summary>
    /// 大图模式关卡数据。
    /// </summary>
    public MPLargeImageLevelModel m_largeImageModel;

    /// <summary>
    /// 宠物系统配置数据。
    /// </summary>
    public MPPetsModel m_petsModel;

    /// <summary>
    /// 初始化所有静态配置数据。
    /// </summary>
    public void Initialize()
    {
        MainLevel();
        LargeImageLevel();
        Pets();
    }

    private void MainLevel()
    {
        List<MPMainBlockInfo> mainBlockInfo;
        using (MPAssetLoadLease<TextAsset> lease = MPLoad.LoadLease<TextAsset>("block_info_main_config"))
        {
            mainBlockInfo = JsonConvert.DeserializeObject<List<MPMainBlockInfo>>(lease.Asset.text);
        }

        m_mainLevelModel = new MPMainLevelModel();
        m_mainLevelModel.blockInfos = mainBlockInfo;
    }

    private void LargeImageLevel()
    {
        List<MPLargeImageBlockInfo> largeImageBlockInfo;
        using (MPAssetLoadLease<TextAsset> lease = MPLoad.LoadLease<TextAsset>("block_info_largeimage_config"))
        {
            largeImageBlockInfo = JsonConvert.DeserializeObject<List<MPLargeImageBlockInfo>>(lease.Asset.text);
        }

        m_largeImageModel = new MPLargeImageLevelModel();
        m_largeImageModel.blockInfos = largeImageBlockInfo;
    }

    /// <summary>
    /// 加载宠物、食物和玩具静态配置。
    /// </summary>
    private void Pets()
    {
        m_petsModel = new MPPetsModel();
        m_petsModel.petConfigs = LoadConfigList<MPPetConfig>("pets_config");
        m_petsModel.foodConfigs = LoadConfigList<MPPetCareItemConfig>("pet_foods_config");
        m_petsModel.toyConfigs = LoadConfigList<MPPetCareItemConfig>("pet_toys_config");
    }

    private List<T> LoadConfigList<T>(string location)
    {
        using (MPAssetLoadLease<TextAsset> lease = MPLoad.LoadLease<TextAsset>(location))
        {
            TextAsset json = lease.Asset;
            if (json == null || string.IsNullOrEmpty(json.text))
            {
                return new List<T>();
            }

            return JsonConvert.DeserializeObject<List<T>>(json.text) ?? new List<T>();
        }
    }
}
