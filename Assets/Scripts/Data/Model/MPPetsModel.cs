using System.Collections.Generic;

public class MPPetsModel
{
    /// <summary>
    /// 宠物静态配置列表，由 pets_config.json 反序列化得到。
    /// </summary>
    public List<MPPetConfig> petConfigs = new List<MPPetConfig>();

    /// <summary>
    /// 食物配置列表，用于恢复当前选中宠物的健康度。
    /// </summary>
    public List<MPPetCareItemConfig> foodConfigs = new List<MPPetCareItemConfig>();

    /// <summary>
    /// 玩具配置列表，用于恢复当前选中宠物的心情度。
    /// </summary>
    public List<MPPetCareItemConfig> toyConfigs = new List<MPPetCareItemConfig>();
}
