using Newtonsoft.Json;

public enum MPPetCareItemType
{
    Food,
    Toy,
}

public enum MPPetRestoreType
{
    Health,
    Mood,
}

public class MPPetCareItemConfig
{
    [JsonProperty]
    private string id;

    [JsonProperty]
    private string name;

    [JsonProperty]
    private string icon;

    [JsonProperty]
    private string itemType;

    [JsonProperty]
    private string restoreType;

    [JsonProperty]
    private float restorePercent;

    [JsonProperty]
    private bool defaultUnlocked;

    [JsonProperty]
    private int defaultCount;

    [JsonProperty]
    private int unlockLevel;

    [JsonProperty]
    private string unlockText;

    public string ID => id;
    public string Name => string.IsNullOrEmpty(name) ? id : name;
    public string Icon => icon;
    public MPPetCareItemType ItemType => itemType == "toy" ? MPPetCareItemType.Toy : MPPetCareItemType.Food;
    public MPPetRestoreType RestoreType => restoreType == "mood" ? MPPetRestoreType.Mood : MPPetRestoreType.Health;
    public float RestorePercent => restorePercent <= 0f ? 0f : restorePercent;
    public bool DefaultUnlocked => defaultUnlocked;
    public int DefaultCount => defaultCount < 0 ? 0 : defaultCount;
    public int UnlockLevel => unlockLevel;
    public string UnlockText => string.IsNullOrEmpty(unlockText) ? $"Unlock at Lv.{unlockLevel}" : unlockText;
    public string RestoreText => $"+{RestorePercent:0} {GetRestoreName()}";

    private string GetRestoreName()
    {
        switch (RestoreType)
        {
            case MPPetRestoreType.Mood:
                return "Mood";
            case MPPetRestoreType.Health:
            default:
                return "Health";
        }
    }
}
