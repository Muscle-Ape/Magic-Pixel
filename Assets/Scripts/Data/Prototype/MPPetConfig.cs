using Newtonsoft.Json;
using System;

/// <summary>
/// 宠物静态配置，对应 YooRes/Config/pets_config.json。
/// 宠物只负责主页展示、解锁条件和局内技能，不再包含养成及奖励数据。
/// </summary>
public class MPPetConfig
{
    [JsonProperty]
    private string id;

    [JsonProperty]
    private string name;

    [JsonProperty]
    private string icon;

    /// <summary>
    /// 局内技能类型，目前支持 hint 和 recover_life。
    /// </summary>
    [JsonProperty]
    private string option;

    /// <summary>
    /// 主页展示的技能说明。
    /// </summary>
    [JsonProperty]
    private string optionText;

    /// <summary>
    /// 每次进入关卡后可免费使用该技能的次数。
    /// </summary>
    [JsonProperty]
    private int skillUseCount;

    /// <summary>
    /// 解锁规则，格式示例：default、mainlevel 25。
    /// </summary>
    [JsonProperty]
    private string unlock;

    [JsonProperty]
    private string unlockText;

    [JsonProperty]
    private string tag;

    [JsonProperty]
    private string claimSkillText;

    public string ID => id;
    public string Name => string.IsNullOrWhiteSpace(name) ? id : name;
    public string Icon => icon;
    public string Option => option == null ? string.Empty : option.Trim().ToLowerInvariant();
    public string OptionText => string.IsNullOrWhiteSpace(optionText) ? Option : optionText;
    public int SkillUseCount => Math.Max(0, skillUseCount);
    public string Unlock => unlock ?? string.Empty;
    public string Tag => string.IsNullOrWhiteSpace(tag) ? "Companion" : tag;
    public string ClaimSkillText => !string.IsNullOrWhiteSpace(claimSkillText) ? claimSkillText
        : Option == MPPetSkillOption.Hint ? "Helps you complete an unfinished block."
        : Option == MPPetSkillOption.RecoverLife ? "Restores a lost life during a puzzle."
        : "A new companion for your puzzles.";
    public bool DefaultUnlocked => string.Equals(Unlock.Trim(), "default", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Unlock.Trim(), "free", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Unlock.Trim(), "unlocked", StringComparison.OrdinalIgnoreCase);

    public string UnlockText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(unlockText))
                return unlockText;

            if (TryGetUnlockRequirement(out string type, out int value)
                && string.Equals(type, "mainlevel", StringComparison.OrdinalIgnoreCase))
            {
                return $"Complete Main Level {value} to unlock";
            }

            return DefaultUnlocked ? "Unlocked" : "Locked";
        }
    }

    public bool TryGetUnlockRequirement(out string type, out int value)
    {
        type = string.Empty;
        value = 0;

        string rule = Unlock.Trim();
        if (string.IsNullOrEmpty(rule))
            return false;

        string[] parts = rule.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        type = parts[0].ToLowerInvariant();
        if (parts.Length == 1)
            return true;

        return int.TryParse(parts[1], out value);
    }
}

/// <summary>
/// pets_config 中 option 字段当前支持的局内技能值。
/// </summary>
public static class MPPetSkillOption
{
    public const string Hint = "hint";
    public const string RecoverLife = "recover_life";
}
