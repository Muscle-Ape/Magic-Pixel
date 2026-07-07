using Newtonsoft.Json;
using System.Collections.Generic;

public class MPMainBlockInfo
{
    /// <summary>
    /// 默认构造函数，供Json反序列化使用。
    /// </summary>
    public MPMainBlockInfo()
    {
    }

    /// <summary>
    /// 根据ID和方块数据创建主关卡信息。
    /// </summary>
    public MPMainBlockInfo(string id, List<int> block)
    {
        this.id = id;
        this.block = block ?? new List<int>();
    }

    /// <summary>
    /// ID 唯一标识符
    /// </summary>
    [JsonProperty]
    private string id;

    /// <summary>
    /// 方块数据
    /// </summary>
    [JsonProperty]
    private List<int> block;

    public string ID { get => id; }

    public List<int> Block { get => block; }
}

