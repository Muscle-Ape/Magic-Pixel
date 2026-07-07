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
        ES3.Save(m_key_customlevel_json, JsonConvert.SerializeObject(m_customlevel_list));
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
            return JsonConvert.DeserializeObject<List<MPCustomLevelInfo>>(json) ?? new List<MPCustomLevelInfo>();
        }
        catch (Exception)
        {
            return new List<MPCustomLevelInfo>();
        }
    }
}








