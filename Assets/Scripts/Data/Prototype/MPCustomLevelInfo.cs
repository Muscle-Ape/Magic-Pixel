using Newtonsoft.Json;
using System;
using System.Collections.Generic;

[Serializable]
public class MPCustomLevelInfo
{
    /// <summary>
    /// 自定义关卡唯一ID。
    /// </summary>
    [JsonProperty]
    private string id;

    /// <summary>
    /// 自定义关卡标题。
    /// </summary>
    [JsonProperty]
    private string title;

    /// <summary>
    /// 自定义关卡网格尺寸。
    /// </summary>
    [JsonProperty]
    private int size;

    /// <summary>
    /// 需要填充的方块索引列表。
    /// </summary>
    [JsonProperty]
    private List<int> block;

    /// <summary>
    /// 自定义关卡颜色信息列表。
    /// </summary>
    [JsonProperty]
    private List<MPCustomLevelColorInfo> colors;

    public string ID => id;
    public string Title => title;
    public int Size => size;
    public List<int> Block => block;
    public List<MPCustomLevelColorInfo> Colors => colors;

    /// <summary>
    /// 默认构造函数，供Json反序列化使用。
    /// </summary>
    public MPCustomLevelInfo()
    {
    }

    /// <summary>
    /// 创建自定义关卡信息实例。
    /// </summary>
    public MPCustomLevelInfo(string id, string title, int size, List<int> block, List<MPCustomLevelColorInfo> colors)
    {
        this.id = id;
        this.title = title;
        this.size = size;
        this.block = block ?? new List<int>();
        this.colors = colors ?? new List<MPCustomLevelColorInfo>();
    }

    /// <summary>
    /// 将自定义关卡转换为主游戏可使用的方块数据。
    /// </summary>
    public MPMainBlockInfo ToMainBlockInfo()
    {
        return new MPMainBlockInfo(id, block);
    }
}

[Serializable]
public class MPCustomLevelColorInfo
{
    /// <summary>
    /// 颜色所在的方块索引。
    /// </summary>
    [JsonProperty]
    private int index;

    /// <summary>
    /// Html格式颜色字符串。
    /// </summary>
    [JsonProperty]
    private string color;

    public int Index => index;
    public string Color => color;

    /// <summary>
    /// 默认构造函数，供Json反序列化使用。
    /// </summary>
    public MPCustomLevelColorInfo()
    {
    }

    /// <summary>
    /// 创建指定方块索引的颜色信息。
    /// </summary>
    public MPCustomLevelColorInfo(int index, string color)
    {
        this.index = index;
        this.color = color;
    }
}

