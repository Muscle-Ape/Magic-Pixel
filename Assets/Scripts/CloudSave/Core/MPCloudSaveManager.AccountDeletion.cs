using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;

public partial class MPCloudSaveManager
{
    /// <summary>复用同步锁，等待正在执行的写入结束，并阻止新的延迟上传/账号切换。</summary>
    public async Task<IDisposable> BeginAccountDeletionAsync(CancellationToken token)
    {
        await m_syncLock.WaitAsync(token);
        if (m_accountSwitchInProgress || m_assetComparisonPending)
        {
            m_syncLock.Release();
            throw new InvalidOperationException("An account operation is still in progress.");
        }
        CancelDebouncedFlush();
        m_accountSwitchInProgress = true;
        return new AccountSwitchGuard(this);
    }

    /// <summary>认证删除会使令牌失效，因此先删除关联社区记录、玩家文件及 Player Data。</summary>
    public async Task DeleteRemoteAccountDataAsync(string playerId, CancellationToken token)
    {
        string localOwner = m_metaRepository.LoadActivePlayerId();
        if (!string.IsNullOrEmpty(localOwner) && localOwner != playerId)
            throw new InvalidOperationException("Finish switching accounts before deleting an account.");
        string cursor = string.Empty;
        do
        {
            token.ThrowIfCancellationRequested();
            if (MPLoginManager.Instance.PlayerId != playerId)
                throw new InvalidOperationException("The signed-in account changed.");
            // 必须部署此服务端接口；未部署或请求失败时终止，不假装账号已经删除。
            string next = await CloudCodeService.Instance.CallModuleEndpointAsync<string>(
                MPCustomLevelPublishConstants.MODULE_NAME, "DeleteAccountCommunityData",
                new Dictionary<string, object> { { "after", cursor } });
            if (!string.IsNullOrEmpty(next) && next == cursor)
                throw new InvalidOperationException("Account cleanup did not advance.");
            cursor = next;
        } while (!string.IsNullOrEmpty(cursor));

        var files = await CloudSaveService.Instance.Files.Player.ListAllAsync();
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            await CloudSaveService.Instance.Files.Player.DeleteAsync(file.Key);
        }
        token.ThrowIfCancellationRequested();
        await CloudSaveService.Instance.Data.Player.DeleteAllAsync(new DeleteAllOptions());
        await CloudSaveService.Instance.Data.Player.DeleteAllAsync(
            new DeleteAllOptions(new PublicWriteAccessClassOptions()));
    }

    public void ClearDeletedAccountLocalData(string playerId)
    {
        string localOwner = m_metaRepository.LoadActivePlayerId();
        if (!string.IsNullOrEmpty(localOwner) && localOwner != playerId)
            throw new InvalidOperationException("Local save belongs to another account.");

        MPUser.instance.ClearDeletedAccountData(playerId);
        MPCustomLevelPublishManager.Instance.ClearDeletedAccountCache(playerId);
        ES3.DeleteKey(COMPARISON_BACKUP_KEY_PREFIX + playerId);
        string choiceJson = ES3.Load<string>(GUEST_SAVE_CHOICE_KEY, defaultValue: null);
        if (!string.IsNullOrEmpty(choiceJson))
        {
            var choice = JsonConvert.DeserializeObject<GuestSaveChoice>(choiceJson);
            if (choice?.guestPlayerId == playerId || choice?.targetPlayerId == playerId)
                ES3.DeleteKey(GUEST_SAVE_CHOICE_KEY);
        }
        m_metaRepository.RemoveDeletedAccount(playerId);
        m_meta = null;
        m_playerId = null;
        m_initialized = false;
        m_isUserDataReady = false;
        m_assetComparisonNeedsResolution = false;
    }
}
