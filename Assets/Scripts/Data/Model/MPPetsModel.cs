using System.Collections.Generic;

public class MPPetsModel
{
    /// <summary>
    /// 宠物静态配置列表，由 pets_config.json 反序列化得到。
    /// </summary>
    public List<MPPetConfig> petConfigs = new List<MPPetConfig>();
}
