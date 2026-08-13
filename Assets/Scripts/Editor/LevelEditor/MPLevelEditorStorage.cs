using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 关卡编辑器支持的运行时关卡类型。
/// </summary>
internal enum MPLevelEditorMode
{
    Main,
    LargeImage,
}

/// <summary>
/// 关卡编辑器在内存中使用的完整关卡数据。
/// 底色和 Blocks 都使用从左上角开始的行优先下标。
/// </summary>
internal sealed class MPLevelEditorData
{
    public MPLevelEditorMode Mode;
    public bool IsExisting;
    public string ID;
    public string Name;
    public int AwardCoin;
    public int Size;
    public Color[] Colors;
    public bool[] ColorAssigned;
    public bool[] Blocks;
    public string SourceAssetPath;
}

/// <summary>
/// 关卡资源保存结果。
/// </summary>
internal sealed class MPLevelEditorSaveResult
{
    public string ConfigAssetPath;
    public string PixelAssetPath;
    public string ThumbAssetPath;
}

/// <summary>
/// 关卡删除结果。
/// </summary>
internal sealed class MPLevelEditorDeleteResult
{
    public string ConfigAssetPath;
    public string PixelAssetPath;
    public string ThumbAssetPath;
    public bool ThumbDeleted;
}

/// <summary>
/// 负责关卡 JSON、BlockPixel 和 Thumb 的读取、保存与删除。
/// </summary>
internal static class MPLevelEditorStorage
{
    public const string MainConfigAssetPath = "Assets/YooRes/Config/block_info_main_config.json";
    public const string LargeImageConfigAssetPath = "Assets/YooRes/Config/block_info_largeimage_config.json";
    public const string BlockPixelAssetDirectory = "Assets/YooRes/Sprites/Res/BlockPixel";
    public const string ThumbAssetDirectory = "Assets/YooRes/Sprites/Res/Thumb";
    public const int MinMainGridSize = 2;
    public const int MinLargeImageGridSize = 10;
    public const int MaxGridSize = 64;

    private const string MainIdPrefix = "level_main_";
    private const string LargeImageIdPrefix = "level_largeimage_";
    private const int LargeImageThumbSize = 600;

    /// <summary>
    /// 根据模式返回对应的 JSON 配置路径。
    /// </summary>
    public static string GetConfigAssetPath(MPLevelEditorMode mode)
    {
        return mode == MPLevelEditorMode.Main ? MainConfigAssetPath : LargeImageConfigAssetPath;
    }

    /// <summary>
    /// 根据模式返回运行时要求的 ID 前缀。
    /// </summary>
    public static string GetIdPrefix(MPLevelEditorMode mode)
    {
        return mode == MPLevelEditorMode.Main ? MainIdPrefix : LargeImageIdPrefix;
    }

    /// <summary>
    /// 根据现有配置计算下一个默认 ID。
    /// </summary>
    public static string GetNextId(MPLevelEditorMode mode)
    {
        string prefix = GetIdPrefix(mode);
        IEnumerable<string> ids = mode == MPLevelEditorMode.Main
            ? LoadMainRecords().Select(item => item.id)
            : LoadLargeImageRecords().Select(item => item.id);

        int maxIndex = 0;
        foreach (string id in ids)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string suffix = id.Substring(prefix.Length);
            if (int.TryParse(suffix, out int index))
            {
                maxIndex = Mathf.Max(maxIndex, index);
            }
        }

        string result;
        do
        {
            maxIndex++;
            result = prefix + maxIndex;
        }
        while (IdExists(result));

        return result;
    }

    /// <summary>
    /// 检查 ID 是否已存在于任意一种关卡配置中。
    /// </summary>
    public static bool IdExists(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        bool existsInConfig = LoadMainRecords().Any(item => string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase))
            || LoadLargeImageRecords().Any(item => string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase));
        if (existsInConfig)
        {
            return true;
        }

        string pixelAssetPath = $"{BlockPixelAssetDirectory}/{id}.png";
        string thumbAssetPath = $"{ThumbAssetDirectory}/icon_{id}.png";
        return File.Exists(ToAbsolutePath(pixelAssetPath)) || File.Exists(ToAbsolutePath(thumbAssetPath));
    }

    /// <summary>
    /// 从拖入的 BlockPixel 图片读取像素颜色，并按同名 ID 从 JSON 中读取 Blocks 和扩展字段。
    /// </summary>
    public static bool TryLoadExisting(Texture2D texture, out MPLevelEditorData data, out string error)
    {
        data = null;
        error = string.Empty;

        if (texture == null)
        {
            error = "请拖入一张关卡 BlockPixel 图片。";
            return false;
        }

        string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            error = "只支持项目 Assets 目录中的 PNG 图片。";
            return false;
        }

        if (!assetPath.StartsWith(BlockPixelAssetDirectory + "/", StringComparison.OrdinalIgnoreCase))
        {
            error = $"请拖入 {BlockPixelAssetDirectory} 目录中的原始关卡图片，而不是 Thumb。";
            return false;
        }

        string id = Path.GetFileNameWithoutExtension(assetPath);
        if (!TryGetModeFromId(id, out MPLevelEditorMode mode))
        {
            error = $"无法从图片名“{id}”判断关卡模式，图片名必须以 {MainIdPrefix} 或 {LargeImageIdPrefix} 开头。";
            return false;
        }

        if (!TryReadPngColors(assetPath, out int size, out Color[] colors, out error))
        {
            return false;
        }

        List<int> blockIndexes;
        string levelName = string.Empty;
        int awardCoin = 0;
        if (mode == MPLevelEditorMode.Main)
        {
            List<MPMainLevelEditorJsonRecord> records = LoadMainRecords()
                .Where(item => string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (records.Count != 1)
            {
                error = $"主关卡配置中 ID {id} 的记录数量为 {records.Count}，必须且只能存在一条。";
                return false;
            }

            MPMainLevelEditorJsonRecord record = records[0];
            blockIndexes = record.block ?? new List<int>();
        }
        else
        {
            List<MPLargeImageLevelEditorJsonRecord> records = LoadLargeImageRecords()
                .Where(item => string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (records.Count != 1)
            {
                error = $"大图关卡配置中 ID {id} 的记录数量为 {records.Count}，必须且只能存在一条。";
                return false;
            }

            MPLargeImageLevelEditorJsonRecord record = records[0];
            levelName = record.name ?? string.Empty;
            awardCoin = record.awardCoin;
            blockIndexes = record.block ?? new List<int>();
        }

        int cellCount = size * size;
        bool[] blocks = new bool[cellCount];
        for (int i = 0; i < blockIndexes.Count; i++)
        {
            int index = blockIndexes[i];
            if (index < 0 || index >= cellCount)
            {
                error = $"关卡 {id} 的 block 下标 {index} 超出图片尺寸 {size}×{size}。";
                return false;
            }

            blocks[index] = true;
        }

        data = new MPLevelEditorData
        {
            Mode = mode,
            IsExisting = true,
            ID = id,
            Name = levelName,
            AwardCoin = awardCoin,
            Size = size,
            Colors = colors,
            ColorAssigned = Enumerable.Repeat(true, cellCount).ToArray(),
            Blocks = blocks,
            SourceAssetPath = assetPath,
        };
        return true;
    }

    /// <summary>
    /// 保存关卡图片和 JSON 配置；只有大图关卡会生成缩略图。
    /// 新关卡追加到配置末尾，已有区域只原位更新对应 ID。
    /// </summary>
    public static MPLevelEditorSaveResult Save(MPLevelEditorData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        ValidateDataForSave(data);
        bool shouldCreateThumb = data.Mode == MPLevelEditorMode.LargeImage;
        EnsureAssetDirectories(shouldCreateThumb);
        List<int> blockIndexes = GetBlockIndexes(data.Blocks);
        MPLevelEditorConfigUpdate configUpdate = data.Mode == MPLevelEditorMode.Main
            ? CreateMainConfigUpdate(data, blockIndexes)
            : CreateLargeImageConfigUpdate(data, blockIndexes);
        CreateLevelImageBytes(data, out byte[] pixelBytes, out byte[] thumbBytes);

        string configPath = configUpdate.AssetPath;
        string pixelAssetPath = $"{BlockPixelAssetDirectory}/{data.ID}.png";
        string thumbAssetPath = shouldCreateThumb
            ? $"{ThumbAssetDirectory}/icon_{data.ID}.png"
            : string.Empty;
        WriteFilesWithRollback(
            configUpdate,
            pixelAssetPath,
            pixelBytes,
            thumbAssetPath,
            thumbBytes);

        AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceUpdate);
        ImportAndConfigureTexture(pixelAssetPath, true);
        if (shouldCreateThumb)
        {
            ImportAndConfigureTexture(thumbAssetPath, false);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return new MPLevelEditorSaveResult
        {
            ConfigAssetPath = configPath,
            PixelAssetPath = pixelAssetPath,
            ThumbAssetPath = thumbAssetPath,
        };
    }

    /// <summary>
    /// 删除已有的关卡配置和图片资源。
    /// BlockPixel 必须存在；只有大图关卡会清理同 ID 的 Thumb。
    /// </summary>
    public static MPLevelEditorDeleteResult Delete(MPLevelEditorMode mode, string id)
    {
        ValidateId(mode, id);

        MPLevelEditorConfigUpdate configUpdate = CreateConfigDeleteUpdate(mode, id);
        string pixelAssetPath = $"{BlockPixelAssetDirectory}/{id}.png";
        string thumbAssetPath = mode == MPLevelEditorMode.LargeImage
            ? $"{ThumbAssetDirectory}/icon_{id}.png"
            : string.Empty;
        bool thumbDeleted = DeleteFilesWithRollback(configUpdate, pixelAssetPath, thumbAssetPath);

        return new MPLevelEditorDeleteResult
        {
            ConfigAssetPath = configUpdate.AssetPath,
            PixelAssetPath = pixelAssetPath,
            ThumbAssetPath = thumbAssetPath,
            ThumbDeleted = thumbDeleted,
        };
    }

    private static MPLevelEditorConfigUpdate CreateMainConfigUpdate(MPLevelEditorData data, List<int> blockIndexes)
    {
        List<MPMainLevelEditorJsonRecord> records = LoadMainRecords();
        List<MPMainLevelEditorJsonRecord> matchedRecords = records
            .Where(item => string.Equals(item.id, data.ID, StringComparison.OrdinalIgnoreCase))
            .ToList();
        MPMainLevelEditorJsonRecord updatedRecord;
        if (data.IsExisting)
        {
            if (matchedRecords.Count != 1)
            {
                throw new InvalidOperationException($"主关卡配置中 ID {data.ID} 的记录数量为 {matchedRecords.Count}，必须且只能存在一条。");
            }

            updatedRecord = matchedRecords[0];
            updatedRecord.block = blockIndexes;
        }
        else
        {
            if (IdExists(data.ID))
            {
                throw new InvalidOperationException($"关卡 ID 已存在：{data.ID}。");
            }

            updatedRecord = new MPMainLevelEditorJsonRecord
            {
                id = data.ID,
                block = blockIndexes,
            };
        }

        return CreateConfigUpdate(MainConfigAssetPath, data.ID, updatedRecord, data.IsExisting);
    }

    private static MPLevelEditorConfigUpdate CreateLargeImageConfigUpdate(MPLevelEditorData data, List<int> blockIndexes)
    {
        List<MPLargeImageLevelEditorJsonRecord> records = LoadLargeImageRecords();
        List<MPLargeImageLevelEditorJsonRecord> matchedRecords = records
            .Where(item => string.Equals(item.id, data.ID, StringComparison.OrdinalIgnoreCase))
            .ToList();
        MPLargeImageLevelEditorJsonRecord updatedRecord;
        if (data.IsExisting)
        {
            if (matchedRecords.Count != 1)
            {
                throw new InvalidOperationException($"大图关卡配置中 ID {data.ID} 的记录数量为 {matchedRecords.Count}，必须且只能存在一条。");
            }

            updatedRecord = matchedRecords[0];
            updatedRecord.name = data.Name;
            updatedRecord.awardCoin = data.AwardCoin;
            updatedRecord.block = blockIndexes;
        }
        else
        {
            if (IdExists(data.ID))
            {
                throw new InvalidOperationException($"关卡 ID 已存在：{data.ID}。");
            }

            updatedRecord = new MPLargeImageLevelEditorJsonRecord
            {
                id = data.ID,
                name = data.Name,
                awardCoin = data.AwardCoin,
                block = blockIndexes,
            };
        }

        return CreateConfigUpdate(LargeImageConfigAssetPath, data.ID, updatedRecord, data.IsExisting);
    }

    private static MPLevelEditorConfigUpdate CreateConfigDeleteUpdate(MPLevelEditorMode mode, string id)
    {
        string assetPath = GetConfigAssetPath(mode);
        int matchedCount = mode == MPLevelEditorMode.Main
            ? LoadMainRecords().Count(item => string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase))
            : LoadLargeImageRecords().Count(item => string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase));
        if (matchedCount != 1)
        {
            throw new InvalidOperationException(
                $"{(mode == MPLevelEditorMode.Main ? "主关卡" : "大图关卡")}配置中 ID {id} 的记录数量为 {matchedCount}，必须且只能存在一条。");
        }

        string absolutePath = ToAbsolutePath(assetPath);
        byte[] originalBytes = File.ReadAllBytes(absolutePath);
        bool emitBom = HasUtf8Bom(originalBytes);
        string originalJson = File.ReadAllText(absolutePath, Encoding.UTF8);
        string updatedJson = RemoveExistingRecord(originalJson, id);

        if (mode == MPLevelEditorMode.Main)
        {
            List<MPMainLevelEditorJsonRecord> records = JsonConvert.DeserializeObject<List<MPMainLevelEditorJsonRecord>>(updatedJson);
            if (records == null || records.Any(item => string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("删除后的主关卡配置无法通过 JSON 校验。");
            }
        }
        else
        {
            List<MPLargeImageLevelEditorJsonRecord> records = JsonConvert.DeserializeObject<List<MPLargeImageLevelEditorJsonRecord>>(updatedJson);
            if (records == null || records.Any(item => string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("删除后的大图关卡配置无法通过 JSON 校验。");
            }
        }

        return new MPLevelEditorConfigUpdate
        {
            AssetPath = assetPath,
            Content = updatedJson,
            Encoding = new UTF8Encoding(emitBom),
        };
    }

    private static List<int> GetBlockIndexes(bool[] blocks)
    {
        var result = new List<int>();
        if (blocks == null)
        {
            return result;
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i])
            {
                result.Add(i);
            }
        }

        return result;
    }

    private static void ValidateDataForSave(MPLevelEditorData data)
    {
        ValidateId(data.Mode, data.ID);

        int minSize = data.Mode == MPLevelEditorMode.Main ? MinMainGridSize : MinLargeImageGridSize;
        if (data.Size < minSize || data.Size > MaxGridSize)
        {
            throw new InvalidDataException($"关卡尺寸必须在 {minSize}～{MaxGridSize} 之间。");
        }

        int cellCount = data.Size * data.Size;
        if (data.Colors == null || data.Colors.Length != cellCount
            || data.ColorAssigned == null || data.ColorAssigned.Length != cellCount
            || data.Blocks == null || data.Blocks.Length != cellCount)
        {
            throw new InvalidDataException("关卡网格数据长度与尺寸不一致。");
        }

        if (data.ColorAssigned.Any(value => !value))
        {
            throw new InvalidDataException("关卡仍有未填充底色的格子。");
        }

        if (data.Mode == MPLevelEditorMode.LargeImage && string.IsNullOrWhiteSpace(data.Name))
        {
            throw new InvalidDataException("大图关卡名称不能为空。");
        }
    }

    private static void ValidateId(MPLevelEditorMode mode, string id)
    {
        string expectedPrefix = GetIdPrefix(mode);
        string idPattern = "^" + Regex.Escape(expectedPrefix) + "[A-Za-z0-9_-]+$";
        if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, idPattern))
        {
            throw new InvalidDataException($"关卡 ID 不合法，必须以 {expectedPrefix} 开头。");
        }
    }

    private static void CreateLevelImageBytes(MPLevelEditorData data, out byte[] pixelBytes, out byte[] thumbBytes)
    {
        int size = data.Size;
        Color32[] sourcePixels = new Color32[size * size];
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                int cellIndex = row * size + column;
                int pixelY = size - 1 - row;
                Color color = data.Colors[cellIndex];
                color.a = 1f;
                sourcePixels[pixelY * size + column] = color;
            }
        }

        Texture2D pixelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D thumbTexture = null;
        try
        {
            pixelTexture.filterMode = FilterMode.Point;
            pixelTexture.wrapMode = TextureWrapMode.Clamp;
            pixelTexture.SetPixels32(sourcePixels);
            pixelTexture.Apply(false, false);

            pixelBytes = pixelTexture.EncodeToPNG();
            if (data.Mode == MPLevelEditorMode.LargeImage)
            {
                thumbTexture = CreateThumbTexture(sourcePixels, size, LargeImageThumbSize);
                thumbBytes = thumbTexture.EncodeToPNG();
            }
            else
            {
                thumbBytes = null;
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pixelTexture);
            if (thumbTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(thumbTexture);
            }
        }
    }

    private static Texture2D CreateThumbTexture(Color32[] sourcePixels, int sourceSize, int targetSize)
    {
        var targetPixels = new Color32[targetSize * targetSize];
        for (int y = 0; y < targetSize; y++)
        {
            int sourceY = y * sourceSize / targetSize;
            for (int x = 0; x < targetSize; x++)
            {
                int sourceX = x * sourceSize / targetSize;
                targetPixels[y * targetSize + x] = sourcePixels[sourceY * sourceSize + sourceX];
            }
        }

        var texture = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(targetPixels);
        texture.Apply(false, false);
        return texture;
    }

    private static bool TryReadPngColors(string assetPath, out int size, out Color[] colors, out string error)
    {
        size = 0;
        colors = null;
        error = string.Empty;

        string absolutePath = ToAbsolutePath(assetPath);
        if (!File.Exists(absolutePath))
        {
            error = $"图片文件不存在：{assetPath}";
            return false;
        }

        var readableTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!readableTexture.LoadImage(File.ReadAllBytes(absolutePath), false))
            {
                error = $"无法读取 PNG 图片：{assetPath}";
                return false;
            }

            if (readableTexture.width <= 0 || readableTexture.width != readableTexture.height)
            {
                error = $"关卡图片必须是正方形，当前尺寸：{readableTexture.width}×{readableTexture.height}。";
                return false;
            }

            size = readableTexture.width;
            colors = new Color[size * size];
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    int pixelY = size - 1 - row;
                    Color color = readableTexture.GetPixel(column, pixelY);
                    color.a = 1f;
                    colors[row * size + column] = color;
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            error = $"读取关卡图片失败：{exception.Message}";
            return false;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(readableTexture);
        }
    }

    private static bool TryGetModeFromId(string id, out MPLevelEditorMode mode)
    {
        if (!string.IsNullOrEmpty(id) && id.StartsWith(MainIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            mode = MPLevelEditorMode.Main;
            return true;
        }

        if (!string.IsNullOrEmpty(id) && id.StartsWith(LargeImageIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            mode = MPLevelEditorMode.LargeImage;
            return true;
        }

        mode = MPLevelEditorMode.Main;
        return false;
    }

    private static List<MPMainLevelEditorJsonRecord> LoadMainRecords()
    {
        return ReadJson<List<MPMainLevelEditorJsonRecord>>(MainConfigAssetPath)
            ?? new List<MPMainLevelEditorJsonRecord>();
    }

    private static List<MPLargeImageLevelEditorJsonRecord> LoadLargeImageRecords()
    {
        return ReadJson<List<MPLargeImageLevelEditorJsonRecord>>(LargeImageConfigAssetPath)
            ?? new List<MPLargeImageLevelEditorJsonRecord>();
    }

    private static T ReadJson<T>(string assetPath)
    {
        string absolutePath = ToAbsolutePath(assetPath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("关卡配置文件不存在。", assetPath);
        }

        string json = File.ReadAllText(absolutePath, Encoding.UTF8);
        return JsonConvert.DeserializeObject<T>(json);
    }

    private static MPLevelEditorConfigUpdate CreateConfigUpdate<T>(
        string assetPath,
        string id,
        T updatedRecord,
        bool isExisting)
    {
        string absolutePath = ToAbsolutePath(assetPath);
        byte[] originalBytes = File.ReadAllBytes(absolutePath);
        bool emitBom = HasUtf8Bom(originalBytes);
        string originalJson = File.ReadAllText(absolutePath, Encoding.UTF8);
        string newLine = originalJson.Contains("\r\n") ? "\r\n" : "\n";
        string serializedRecord = JsonConvert.SerializeObject(updatedRecord, Formatting.Indented)
            .Replace("\r\n", "\n")
            .Replace("\n", newLine);

        string updatedJson = isExisting
            ? ReplaceExistingRecord(originalJson, id, serializedRecord, newLine)
            : AppendRecord(originalJson, serializedRecord, newLine);

        List<T> validationRecords = JsonConvert.DeserializeObject<List<T>>(updatedJson);
        if (validationRecords == null)
        {
            throw new InvalidDataException("更新后的关卡配置无法通过 JSON 校验。");
        }

        return new MPLevelEditorConfigUpdate
        {
            AssetPath = assetPath,
            Content = updatedJson,
            Encoding = new UTF8Encoding(emitBom),
        };
    }

    private static string ReplaceExistingRecord(string json, string id, string serializedRecord, string newLine)
    {
        Match idMatch = Regex.Match(
            json,
            $"\\\"id\\\"\\s*:\\s*\\\"{Regex.Escape(id)}\\\"",
            RegexOptions.IgnoreCase);
        if (!idMatch.Success)
        {
            throw new InvalidOperationException($"配置文本中没有找到需要修改的 ID：{id}。");
        }

        int objectStart = FindObjectStart(json, idMatch.Index);
        int objectEnd = FindObjectEnd(json, objectStart);
        string baseIndent = GetLineIndent(json, objectStart);
        string indentedRecord = IndentSerializedRecord(serializedRecord, baseIndent, newLine, false);
        return json.Substring(0, objectStart)
            + indentedRecord
            + json.Substring(objectEnd + 1);
    }

    private static string RemoveExistingRecord(string json, string id)
    {
        MatchCollection idMatches = Regex.Matches(
            json,
            $"\\\"id\\\"\\s*:\\s*\\\"{Regex.Escape(id)}\\\"",
            RegexOptions.IgnoreCase);
        if (idMatches.Count != 1)
        {
            throw new InvalidOperationException(
                $"配置文本中 ID {id} 的记录数量为 {idMatches.Count}，必须且只能存在一条。");
        }

        int objectStart = FindObjectStart(json, idMatches[0].Index);
        int objectEnd = FindObjectEnd(json, objectStart);
        int previousIndex = FindPreviousNonWhitespace(json, objectStart - 1);
        int nextIndex = FindNextNonWhitespace(json, objectEnd + 1);
        int removalStart = objectStart;
        int removalEnd = objectEnd + 1;

        if (nextIndex < json.Length && json[nextIndex] == ',')
        {
            removalStart = GetIndentedLineStart(json, objectStart);
            removalEnd = ConsumeFollowingLineBreak(json, nextIndex + 1);
        }
        else if (previousIndex >= 0 && json[previousIndex] == ',')
        {
            removalStart = previousIndex;
        }
        else
        {
            removalStart = GetIndentedLineStart(json, objectStart);
            removalEnd = ConsumeFollowingLineBreak(json, objectEnd + 1);
        }

        return json.Remove(removalStart, removalEnd - removalStart);
    }

    private static string AppendRecord(string json, string serializedRecord, string newLine)
    {
        int arrayEnd = json.LastIndexOf(']');
        if (arrayEnd < 0)
        {
            throw new InvalidDataException("关卡配置不是合法的 JSON 数组。");
        }

        int contentEnd = arrayEnd - 1;
        while (contentEnd >= 0 && char.IsWhiteSpace(json[contentEnd]))
        {
            contentEnd--;
        }

        bool isEmptyArray = contentEnd >= 0 && json[contentEnd] == '[';
        string baseIndent = GetFirstObjectIndent(json, arrayEnd);
        string indentedRecord = IndentSerializedRecord(serializedRecord, baseIndent, newLine, true);
        string prefix = json.Substring(0, contentEnd + 1);
        string suffix = json.Substring(arrayEnd);
        string separator = isEmptyArray ? newLine : "," + newLine;
        return prefix + separator + indentedRecord + newLine + suffix;
    }

    private static int FindObjectStart(string json, int searchFrom)
    {
        for (int i = searchFrom; i >= 0; i--)
        {
            if (json[i] == '{')
            {
                return i;
            }
        }

        throw new InvalidDataException("无法定位关卡 JSON 对象起点。");
    }

    private static int FindObjectEnd(string json, int objectStart)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = objectStart; i < json.Length; i++)
        {
            char character = json[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
            }
            else if (character == '{')
            {
                depth++;
            }
            else if (character == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        throw new InvalidDataException("无法定位关卡 JSON 对象终点。");
    }

    private static int FindPreviousNonWhitespace(string text, int index)
    {
        while (index >= 0 && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        return index;
    }

    private static int FindNextNonWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int GetIndentedLineStart(string text, int index)
    {
        int lineStart = text.LastIndexOf('\n', Mathf.Max(0, index - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        for (int i = lineStart; i < index; i++)
        {
            if (text[i] != ' ' && text[i] != '\t' && text[i] != '\r')
            {
                return index;
            }
        }

        return lineStart;
    }

    private static int ConsumeFollowingLineBreak(string text, int index)
    {
        while (index < text.Length && (text[index] == ' ' || text[index] == '\t'))
        {
            index++;
        }

        if (index < text.Length && text[index] == '\r')
        {
            index++;
        }

        if (index < text.Length && text[index] == '\n')
        {
            index++;
        }

        return index;
    }

    private static string GetLineIndent(string text, int index)
    {
        int lineStart = text.LastIndexOf('\n', Mathf.Max(0, index - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        int cursor = lineStart;
        while (cursor < text.Length && (text[cursor] == ' ' || text[cursor] == '\t'))
        {
            cursor++;
        }

        return text.Substring(lineStart, cursor - lineStart);
    }

    private static string GetFirstObjectIndent(string json, int arrayEnd)
    {
        int firstObject = json.IndexOf('{');
        return firstObject >= 0 && firstObject < arrayEnd ? GetLineIndent(json, firstObject) : "    ";
    }

    private static string IndentSerializedRecord(
        string serializedRecord,
        string baseIndent,
        string newLine,
        bool includeFirstLineIndent)
    {
        string[] lines = serializedRecord.Split(new[] { newLine }, StringSplitOptions.None);
        var builder = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(newLine);
            }

            if (includeFirstLineIndent || i > 0)
            {
                builder.Append(baseIndent);
            }

            builder.Append(lines[i]);
        }

        return builder.ToString();
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes != null
            && bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF;
    }

    private static void WriteFilesWithRollback(
        MPLevelEditorConfigUpdate configUpdate,
        string pixelAssetPath,
        byte[] pixelBytes,
        string thumbAssetPath,
        byte[] thumbBytes)
    {
        string configAbsolutePath = ToAbsolutePath(configUpdate.AssetPath);
        string pixelAbsolutePath = ToAbsolutePath(pixelAssetPath);
        bool hasThumb = !string.IsNullOrEmpty(thumbAssetPath) && thumbBytes != null;
        string thumbAbsolutePath = hasThumb ? ToAbsolutePath(thumbAssetPath) : string.Empty;
        var configBackup = MPLevelEditorFileBackup.Capture(configAbsolutePath);
        var pixelBackup = MPLevelEditorFileBackup.Capture(pixelAbsolutePath);
        MPLevelEditorFileBackup thumbBackup = hasThumb
            ? MPLevelEditorFileBackup.Capture(thumbAbsolutePath)
            : null;

        try
        {
            File.WriteAllBytes(pixelAbsolutePath, pixelBytes);
            if (hasThumb)
            {
                File.WriteAllBytes(thumbAbsolutePath, thumbBytes);
            }

            File.WriteAllText(configAbsolutePath, configUpdate.Content, configUpdate.Encoding);
        }
        catch
        {
            configBackup.Restore();
            pixelBackup.Restore();
            thumbBackup?.Restore();
            throw;
        }
    }

    private static bool DeleteFilesWithRollback(
        MPLevelEditorConfigUpdate configUpdate,
        string pixelAssetPath,
        string thumbAssetPath)
    {
        string configAbsolutePath = ToAbsolutePath(configUpdate.AssetPath);
        string pixelAbsolutePath = ToAbsolutePath(pixelAssetPath);
        string pixelMetaPath = pixelAbsolutePath + ".meta";
        bool shouldDeleteThumb = !string.IsNullOrEmpty(thumbAssetPath);
        string thumbAbsolutePath = shouldDeleteThumb ? ToAbsolutePath(thumbAssetPath) : string.Empty;
        string thumbMetaPath = shouldDeleteThumb ? thumbAbsolutePath + ".meta" : string.Empty;
        if (!File.Exists(pixelAbsolutePath))
        {
            throw new FileNotFoundException("关卡 BlockPixel 图片不存在，已取消删除。", pixelAssetPath);
        }

        bool thumbExists = shouldDeleteThumb && File.Exists(thumbAbsolutePath);
        var backups = new List<MPLevelEditorFileBackup>
        {
            MPLevelEditorFileBackup.Capture(configAbsolutePath),
            MPLevelEditorFileBackup.Capture(pixelAbsolutePath),
            MPLevelEditorFileBackup.Capture(pixelMetaPath),
        };
        if (shouldDeleteThumb)
        {
            backups.Add(MPLevelEditorFileBackup.Capture(thumbAbsolutePath));
            backups.Add(MPLevelEditorFileBackup.Capture(thumbMetaPath));
        }

        try
        {
            File.WriteAllText(configAbsolutePath, configUpdate.Content, configUpdate.Encoding);
            File.Delete(pixelAbsolutePath);
            File.Delete(pixelMetaPath);
            if (shouldDeleteThumb)
            {
                File.Delete(thumbAbsolutePath);
                File.Delete(thumbMetaPath);
            }

            AssetDatabase.ImportAsset(
                configUpdate.AssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
        }
        catch
        {
            foreach (MPLevelEditorFileBackup backup in backups)
            {
                backup.Restore();
            }

            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception refreshException)
            {
                Debug.LogException(refreshException);
            }

            throw;
        }

        return thumbExists;
    }

    private static void EnsureAssetDirectories(bool includeThumb)
    {
        Directory.CreateDirectory(ToAbsolutePath(BlockPixelAssetDirectory));
        if (includeThumb)
        {
            Directory.CreateDirectory(ToAbsolutePath(ThumbAssetDirectory));
        }
    }

    private static void ImportAndConfigureTexture(string assetPath, bool isBlockPixel)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"无法获取图片导入器：{assetPath}");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.isReadable = isBlockPixel;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, NormalizeAssetPath(assetPath)));
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    [Serializable]
    private sealed class MPMainLevelEditorJsonRecord
    {
        [JsonProperty(Order = 0)]
        public string id;

        [JsonProperty(Order = 1)]
        public List<int> block = new List<int>();
    }

    [Serializable]
    private sealed class MPLargeImageLevelEditorJsonRecord
    {
        [JsonProperty(Order = 0)]
        public string id;

        [JsonProperty(Order = 1)]
        public string name;

        [JsonProperty("award_coin", Order = 2)]
        public int awardCoin;

        [JsonProperty(Order = 3)]
        public List<int> block = new List<int>();
    }

    private sealed class MPLevelEditorConfigUpdate
    {
        public string AssetPath;
        public string Content;
        public Encoding Encoding;
    }

    private sealed class MPLevelEditorFileBackup
    {
        private string m_path;
        private bool m_existed;
        private byte[] m_bytes;

        public static MPLevelEditorFileBackup Capture(string path)
        {
            bool exists = File.Exists(path);
            return new MPLevelEditorFileBackup
            {
                m_path = path,
                m_existed = exists,
                m_bytes = exists ? File.ReadAllBytes(path) : null,
            };
        }

        public void Restore()
        {
            if (m_existed)
            {
                File.WriteAllBytes(m_path, m_bytes);
            }
            else if (File.Exists(m_path))
            {
                File.Delete(m_path);
            }
        }
    }
}
