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

    /// <summary>
    /// 可选宝箱奖励。配置为空或奖励无效时不显示宝箱。
    /// </summary>
    [JsonProperty("box_award")]
    private MPMainLevelBoxAward boxAward;

    public string ID { get => id; }

    public List<int> Block { get => block; }

    public MPMainLevelBoxAward BoxAward { get => boxAward; }
}

/// <summary>
/// 主线关卡宝箱奖励配置。
/// </summary>
public sealed class MPMainLevelBoxAward
{
    [JsonProperty("type")]
    private string type;

    [JsonProperty("count")]
    private int count;

    public string Type { get => type ?? string.Empty; }

    public int Count { get => count; }

    public bool IsValid => !string.IsNullOrWhiteSpace(type) && count > 0;
}
