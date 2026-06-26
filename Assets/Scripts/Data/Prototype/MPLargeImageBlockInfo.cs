using Newtonsoft.Json;
using System.Collections.Generic;

public class MPLargeImageBlockInfo
{
    /// <summary>
    /// ID 唯一标识符
    /// 和图片名称绑定
    /// </summary>
    [JsonProperty]
    private string id;

    /// <summary>
    /// 关卡名称
    /// </summary>
    [JsonProperty]
    private string name;

    /// <summary>
    /// 方块位置信息
    /// </summary>
    [JsonProperty]
    private List<int> block;


    public string ID { get => id; }

    public string Name { get => name; }

    public List<int> Block { get => block; }
}
