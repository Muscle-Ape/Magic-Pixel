using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

public partial class MPCloudSaveManager
{
    private const string GUEST_SAVE_CHOICE_KEY = "MPCloudSave.PendingGuestChoice.v1";

    [Serializable]
    private sealed class GuestSaveChoice
    {
        public string guestPlayerId;
        public string targetPlayerId;
        public MPLoginProvider provider;
        public MPUserCloudSnapshot user;
        public MPCustomLevelCloudSnapshot custom;
    }

    /// <summary>只在游客绑定冲突时创建迁移意图；先保存游客云档，绝不把游客身份改为 Apple 身份。</summary>
    public async Task<bool> PrepareGuestSaveChoiceAsync(MPLoginType provider, CancellationToken token)
    {
        var profile = await MPLoginManager.Instance.LoadLocalProfileAsync(token);
        if (profile?.IsIndependentGuest != true || profile.playerId != MPLoginManager.Instance.PlayerId)
            return false;
        if (!await FlushAsync(token)) return false;
        token.ThrowIfCancellationRequested();
        if (profile.playerId != MPLoginManager.Instance.PlayerId || HasAnyDirtyData()) return false;
        var user = MPUser.instance.CreateCloudSnapshot();
        var custom = MPUser.instance.CreateCustomLevelCloudSnapshot();
        ApplyLoginMetadata(user, profile);
        ApplyLoginMetadata(custom, profile);
        var choice = new GuestSaveChoice
        {
            guestPlayerId = profile.playerId,
            provider = provider == MPLoginType.Apple ? MPLoginProvider.Apple
                : provider == MPLoginType.GooglePlayGames ? MPLoginProvider.GooglePlayGames
                : provider == MPLoginType.Facebook ? MPLoginProvider.Facebook : MPLoginProvider.Unknown,
            user = user, custom = custom
        };
        if (choice.provider == MPLoginProvider.Unknown) return false;
        // 保留快照用于选择中断后的重试；游客自己的云存档和登录凭证均不删除。
        ES3.Save(GUEST_SAVE_CHOICE_KEY, JsonConvert.SerializeObject(choice));
        return true;
    }

    private GuestSaveChoice GetGuestSaveChoice(MPLocalLoginProfile profile)
    {
        string json = ES3.Load<string>(GUEST_SAVE_CHOICE_KEY, defaultValue: null);
        if (string.IsNullOrEmpty(json)) return null;
        var choice = JsonConvert.DeserializeObject<GuestSaveChoice>(json);
        if (choice?.user == null || choice.custom == null || string.IsNullOrEmpty(choice.guestPlayerId) ||
            choice.user.playerId != choice.guestPlayerId || choice.custom.playerId != choice.guestPlayerId)
            throw new InvalidOperationException("Guest save selection backup is invalid.");
        if (m_playerId == choice.guestPlayerId || profile?.lastLoginProvider != choice.provider) return null;
        if (!string.IsNullOrEmpty(choice.targetPlayerId) && choice.targetPlayerId != m_playerId) return null;
        if (string.IsNullOrEmpty(choice.targetPlayerId))
        {
            choice.targetPlayerId = m_playerId;
            ES3.Save(GUEST_SAVE_CHOICE_KEY, JsonConvert.SerializeObject(choice));
        }
        return choice;
    }

    private static void CompleteGuestSaveChoice(GuestSaveChoice choice)
    {
        if (choice == null) return;
        // 仅清除本次选择意图，不删除游客快照、游客会话或 Apple 存档。
        ES3.DeleteKey(GUEST_SAVE_CHOICE_KEY);
    }
}
