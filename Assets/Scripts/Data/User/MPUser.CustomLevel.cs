using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class MPUser
{
    /// <summary>
    /// 自定义关卡默认标题前缀。
    /// </summary>
    private const string DEFAULT_CUSTOM_LEVEL_TITLE = "Custom Level";

    /// <summary>
    /// 自定义关卡图片保存目录名称。
    /// </summary>
    private const string CUSTOM_LEVEL_IMAGE_FOLDER = "CustomLevels";

    /// <summary>
    /// 自定义关卡Json存档Key。
    /// </summary>
    private string m_key_customlevel_json = "key_customlevel_json";

    /// <summary>
    /// 自定义关卡通关列表存档Key。
    /// </summary>
    private string m_key_customlevel_passlist = "key_customlevel_passlist";

    /// <summary>
    /// 自定义关卡列表缓存。
    /// </summary>
    private List<MPCustomLevelInfo> m_customlevel_list;

    /// <summary>
    /// 自定义关卡通关列表缓存。
    /// </summary>
    private List<string> m_customlevel_passlist;

    /// <summary>
    /// 自定义关卡图片保存目录路径。
    /// </summary>
    public string CustomLevelImageDirectory => Path.Combine(Application.persistentDataPath, CUSTOM_LEVEL_IMAGE_FOLDER);

    /// <summary>
    /// 初始化自定义关卡存档数据。
    /// </summary>
    private void InitCustomLevel()
    {
        string json = ES3.Load<string>(m_key_customlevel_json, defaultValue: null);
        m_customlevel_list = DeserializeCustomLevels(json);
        m_customlevel_passlist = ES3.Load<List<string>>(m_key_customlevel_passlist, new List<string>());
    }


    /// <summary>
    /// 获取所有自定义关卡。
    /// </summary>
    public List<MPCustomLevelInfo> GetCustomLevels()
    {
        if (m_customlevel_list == null)
        {
            InitCustomLevel();
        }

        return m_customlevel_list;
    }


    /// <summary>
    /// 获取自定义关卡Json字符串。
    /// </summary>
    public string GetCustomLevelsJson()
    {
        return JsonConvert.SerializeObject(GetCustomLevels());
    }


    /// <summary>
    /// 获取新自定义关卡的默认标题。
    /// </summary>
    public string GetDefaultCustomLevelTitle()
    {
        return "Undefined";
    }

    /// <summary>
    /// 创建不会与已有数据和本地图片重复的自定义关卡ID。
    /// </summary>
    public string CreateCustomLevelImageID()
    {
        int index = 1;
        while (CustomLevelImageIDExists(index))
        {
            index++;
        }

        return $"level_custom_{index}";
    }


    /// <summary>
    /// 获取自定义关卡像素图片保存路径。
    /// </summary>
    public string GetCustomLevelImagePath(string id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;

        return Path.Combine(CustomLevelImageDirectory, $"{id}.png");
    }


    /// <summary>
    /// 获取自定义关卡列表图标保存路径。
    /// </summary>
    public string GetCustomLevelIconImagePath(string id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;

        return Path.Combine(CustomLevelImageDirectory, $"icon_{id}.png");
    }


    /// <summary>
    /// 读取自定义关卡完整像素图，调用方使用结束后需要自行销毁返回的Texture2D。
    /// </summary>
    public Texture2D LoadCustomLevelImageTexture(MPCustomLevelInfo levelInfo)
    {
        if (levelInfo == null)
            return null;

        Texture2D texture = LoadCustomLevelImageTextureFromFile(GetCustomLevelImagePath(levelInfo.ID));
        if (texture != null)
        {
            return texture;
        }

        return CreateCustomLevelImageTextureFromConfig(levelInfo);
    }


    /// <summary>
    /// 确保自定义关卡图片目录存在。
    /// </summary>
    public void EnsureCustomLevelImageDirectory()
    {
        if (!Directory.Exists(CustomLevelImageDirectory))
        {
            Directory.CreateDirectory(CustomLevelImageDirectory);
        }
    }


    /// <summary>
    /// 保存自定义关卡信息。
    /// </summary>
    public void SaveCustomLevel(MPCustomLevelInfo levelInfo)
    {
        if (levelInfo == null)
            return;

        levelInfo = NormalizeCustomLevel(levelInfo);
        if (levelInfo == null)
            return;

        List<MPCustomLevelInfo> levels = GetCustomLevels();
        int index = levels.FindIndex(item => item.ID == levelInfo.ID);
        if (index >= 0)
        {
            levels[index] = levelInfo;
        }
        else
        {
            levels.Add(levelInfo);
        }

        SaveCustomLevelsJson();
    }


    /// <summary>
    /// 删除指定ID的自定义关卡数据和本地图片。
    /// </summary>
    public void DeleteCustomLevel(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        List<MPCustomLevelInfo> levels = GetCustomLevels();
        int index = levels.FindIndex(item => item != null && item.ID == id);
        if (index >= 0)
        {
            levels.RemoveAt(index);
            SaveCustomLevelsJson();
        }

        if (m_customlevel_passlist == null)
        {
            m_customlevel_passlist = ES3.Load<List<string>>(m_key_customlevel_passlist, new List<string>());
        }

        if (m_customlevel_passlist.Remove(id))
        {
            ES3.Save(m_key_customlevel_passlist, m_customlevel_passlist);
            NotifyCloudSaveDirty(MPCloudSaveDirtyReason.CustomLevel);
        }

        DeleteCustomLevelFile(GetCustomLevelImagePath(id));
        DeleteCustomLevelFile(GetCustomLevelIconImagePath(id));
    }


    /// <summary>
    /// 标记自定义关卡已通关。
    /// </summary>
    public void CustomLevelPass(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (m_customlevel_passlist == null)
        {
            m_customlevel_passlist = ES3.Load<List<string>>(m_key_customlevel_passlist, new List<string>());
        }

        if (!m_customlevel_passlist.Contains(id))
        {
            m_customlevel_passlist.Add(id);
            ES3.Save(m_key_customlevel_passlist, m_customlevel_passlist);
            NotifyCloudSaveDirty(MPCloudSaveDirtyReason.CustomLevel);
        }
    }


    /// <summary>
    /// 判断自定义关卡是否已通关。
    /// </summary>
    public bool CustomLevelIsPass(string id)
    {
        if (m_customlevel_passlist == null)
        {
            m_customlevel_passlist = ES3.Load<List<string>>(m_key_customlevel_passlist, new List<string>());
        }

        return !string.IsNullOrEmpty(id) && m_customlevel_passlist.Contains(id);
    }


    /// <summary>
    /// 将自定义关卡列表保存为Json字符串。
    /// </summary>
    private void SaveCustomLevelsJson()
    {
        m_customlevel_list = NormalizeCustomLevels(m_customlevel_list);
        ES3.Save(m_key_customlevel_json, JsonConvert.SerializeObject(m_customlevel_list));
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.CustomLevel);
    }

    /// <summary>
    /// 清洗自定义关卡列表，去掉空数据和重复索引，避免本地存档与云端快照不断放大。
    /// </summary>
    private static List<MPCustomLevelInfo> NormalizeCustomLevels(List<MPCustomLevelInfo> levels)
    {
        List<MPCustomLevelInfo> result = new List<MPCustomLevelInfo>();
        if (levels == null)
        {
            return result;
        }

        HashSet<string> levelIds = new HashSet<string>();
        for (int i = 0; i < levels.Count; i++)
        {
            MPCustomLevelInfo level = NormalizeCustomLevel(levels[i]);
            if (level == null || string.IsNullOrEmpty(level.ID) || levelIds.Contains(level.ID))
            {
                continue;
            }

            levelIds.Add(level.ID);
            result.Add(level);
        }

        return result;
    }

    /// <summary>
    /// 清洗单个自定义关卡，保证 block 和 colors 中同一个格子索引只出现一次。
    /// </summary>
    private static MPCustomLevelInfo NormalizeCustomLevel(MPCustomLevelInfo level)
    {
        if (level == null || string.IsNullOrEmpty(level.ID))
        {
            return null;
        }

        int size = Mathf.Max(1, level.Size);
        int cellCount = size * size;
        List<int> blocks = NormalizeCustomBlockIndexes(level.Block, cellCount);
        List<MPCustomLevelColorInfo> colors = NormalizeCustomColors(level.Colors, cellCount);
        string title = string.IsNullOrEmpty(level.Title) ? "Undefined" : level.Title;

        return new MPCustomLevelInfo(level.ID, title, size, blocks, colors);
    }

    /// <summary>
    /// 清洗填充格索引，过滤越界值并去重。
    /// </summary>
    private static List<int> NormalizeCustomBlockIndexes(List<int> source, int cellCount)
    {
        List<int> result = new List<int>();
        if (source == null)
        {
            return result;
        }

        HashSet<int> indexes = new HashSet<int>();
        for (int i = 0; i < source.Count; i++)
        {
            int index = source[i];
            if (index < 0 || index >= cellCount || indexes.Contains(index))
            {
                continue;
            }

            indexes.Add(index);
            result.Add(index);
        }

        result.Sort();
        return result;
    }

    /// <summary>
    /// 清洗颜色格索引，过滤越界值并按索引去重；同索引重复时保留最后一次颜色。
    /// </summary>
    private static List<MPCustomLevelColorInfo> NormalizeCustomColors(List<MPCustomLevelColorInfo> source, int cellCount)
    {
        List<MPCustomLevelColorInfo> result = new List<MPCustomLevelColorInfo>();
        if (source == null)
        {
            return result;
        }

        Dictionary<int, string> colorByIndex = new Dictionary<int, string>();
        for (int i = 0; i < source.Count; i++)
        {
            MPCustomLevelColorInfo colorInfo = source[i];
            if (colorInfo == null || colorInfo.Index < 0 || colorInfo.Index >= cellCount || string.IsNullOrEmpty(colorInfo.Color))
            {
                continue;
            }

            colorByIndex[colorInfo.Index] = colorInfo.Color;
        }

        List<int> indexes = new List<int>(colorByIndex.Keys);
        indexes.Sort();
        for (int i = 0; i < indexes.Count; i++)
        {
            int index = indexes[i];
            result.Add(new MPCustomLevelColorInfo(index, colorByIndex[index]));
        }

        return result;
    }


    /// <summary>
    /// 判断指定序号的自定义关卡ID或图片是否已经存在。
    /// </summary>
    private bool CustomLevelImageIDExists(int index)
    {
        string id = $"level_custom_{index}";
        List<MPCustomLevelInfo> levels = GetCustomLevels();
        bool dataExists = levels.Exists(item => item != null && item.ID == id);

        return dataExists || File.Exists(GetCustomLevelImagePath(id)) || File.Exists(GetCustomLevelIconImagePath(id));
    }

    /// <summary>
    /// 删除指定路径的自定义关卡本地文件。
    /// </summary>
    private void DeleteCustomLevelFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Delete custom level file failed: {path}, {exception}");
        }
    }

    /// <summary>
    /// 从本地缓存文件中读取自定义关卡像素图。
    /// </summary>
    private Texture2D LoadCustomLevelImageTextureFromFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        Texture2D texture = null;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
        catch (Exception exception)
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }

            Debug.LogWarning($"Load custom level image failed: {path}, {exception}");
            return null;
        }
    }

    /// <summary>
    /// 本地缓存图片缺失时，根据自定义关卡颜色配置临时生成完整像素图。
    /// </summary>
    private Texture2D CreateCustomLevelImageTextureFromConfig(MPCustomLevelInfo levelInfo)
    {
        int size = Mathf.Max(1, levelInfo.Size);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        texture.SetPixels(pixels);

        if (levelInfo.Colors != null)
        {
            for (int i = 0; i < levelInfo.Colors.Count; i++)
            {
                MPCustomLevelColorInfo colorInfo = levelInfo.Colors[i];
                if (colorInfo == null || colorInfo.Index < 0 || colorInfo.Index >= pixels.Length)
                    continue;

                if (!ColorUtility.TryParseHtmlString(colorInfo.Color, out Color color))
                    continue;

                int x = colorInfo.Index % size;
                int y = size - 1 - colorInfo.Index / size;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    /// <summary>
    /// 反序列化自定义关卡Json。
    /// </summary>
    private List<MPCustomLevelInfo> DeserializeCustomLevels(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<MPCustomLevelInfo>();
        }

        try
        {
            return NormalizeCustomLevels(JsonConvert.DeserializeObject<List<MPCustomLevelInfo>>(json));
        }
        catch (Exception)
        {
            return new List<MPCustomLevelInfo>();
        }
    }
}








