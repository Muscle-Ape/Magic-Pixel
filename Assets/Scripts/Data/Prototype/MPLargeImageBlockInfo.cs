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
    /// 通关奖励，与主关卡宝箱使用相同的类型和数量结构。
    /// </summary>
    [JsonProperty("box_award")]
    private MPMainLevelBoxAward boxAward;

    /// <summary>
    /// 方块位置信息
    /// </summary>
    [JsonProperty]
    private List<int> block;


    public string ID { get => id; }

    public string Name { get => name; }

    public MPMainLevelBoxAward BoxAward => boxAward;

    public List<int> Block { get => block; }
}
