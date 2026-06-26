using System.Collections.Generic;
using UnityEngine;
using YooAsset;

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
    /// 主关卡
    /// </summary>
    public MPMainLevelModel m_mainLevelModel;

    /// <summary>
    /// 大图模式关卡
    /// </summary>
    public MPLargeImageLevelModel m_largeImageModel;

    /// <summary>
    /// 初始化数据
    /// </summary>
    public void Initialize()
    {
        MainLevel();
        LargeImageLevel();
    }

    private void MainLevel()
    {
        // Json 配置
        TextAsset json = YooAssets.LoadAssetSync<TextAsset>("block_info_main_config").AssetObject as TextAsset;

        // 反序列化
        List<MPMainBlockInfo> mainBlockInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<List<MPMainBlockInfo>>(json.text);

        m_mainLevelModel = new MPMainLevelModel();
        m_mainLevelModel.blockInfos = mainBlockInfo;
    }

    private void LargeImageLevel()
    {
        // Json 配置
        TextAsset json = YooAssets.LoadAssetSync<TextAsset>("block_info_largeimage_config").AssetObject as TextAsset;

        // 反序列化
        List<MPLargeImageBlockInfo> largeImageBlockInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<List<MPLargeImageBlockInfo>>(json.text);

        m_largeImageModel = new MPLargeImageLevelModel();
        m_largeImageModel.blockInfos = largeImageBlockInfo;
    }
}
