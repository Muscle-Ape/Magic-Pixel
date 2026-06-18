using Newtonsoft.Json;
using System.Collections.Generic;

public class MPMainBlockInfo
{
    /// <summary>
    /// ID 唯一标识符
    /// 和图片名称绑定
    /// </summary>
    [JsonProperty]
    private string id;

    /// <summary>
    /// 方块位置信息
    /// </summary>
    [JsonProperty]
    private List<int> block;

    public string ID { get => id; }

    public List<int> Block { get => block; }
}
