using System;
using System.Threading;
using System.Threading.Tasks;

public partial class MPLoginManager
{
    public bool IsDeletingAccount { get; private set; }
    private string m_deletedAccountPendingCleanup;
    public string DeletedAccountPendingCleanup => m_deletedAccountPendingCleanup;

    private void ThrowIfDeletingAccount()
    {
        if (IsDeletingAccount || m_deletedAccountPendingCleanup != null)
            throw new InvalidOperationException("Please finish account deletion first.");
    }

    /// <summary>仅在用户确认后调用。远端删除成功前不清理本地存档，不自动创建替代游客。</summary>
    public async Task DeleteCurrentAccountAsync(string expectedPlayerId, CancellationToken token = default)
    {
        if (IsDeletingAccount || IsLoginFlowRunning || string.IsNullOrEmpty(expectedPlayerId))
            throw new InvalidOperationException("Invalid account deletion request.");
        if (m_deletedAccountPendingCleanup != expectedPlayerId &&
            (!IsLoggedIn || PlayerId != expectedPlayerId))
            throw new InvalidOperationException("The signed-in account changed. Please reopen Settings.");
        if (MPCustomLevelPublishManager.Instance.HasPendingAccountWrites)
            throw new InvalidOperationException("Please wait for pending community operations and retry.");

        IsDeletingAccount = true;
        try
        {
            using (await MPCloudSaveManager.Instance.BeginAccountDeletionAsync(token))
            {
                if (m_deletedAccountPendingCleanup != expectedPlayerId)
                {
                    // 先使用服务端凭证完成 Apple 授权撤销；缺少凭证、取消或服务异常都不进入数据删除。
                    await RevokeAppleAuthorizationForDeletionAsync(expectedPlayerId, token);
                    token.ThrowIfCancellationRequested();
                    if (PlayerId != expectedPlayerId)
                        throw new InvalidOperationException("The signed-in account changed.");
                    await MPCloudSaveManager.Instance.DeleteRemoteAccountDataAsync(expectedPlayerId, token);
                    token.ThrowIfCancellationRequested();
                    if (PlayerId != expectedPlayerId)
                        throw new InvalidOperationException("The signed-in account changed.");
                    await m_inner.DeleteAccountAsync(token);
                    // 后续本地清理即使 UI 已关闭也必须继续；失败可重试，不能再删除另一个账号。
                    m_deletedAccountPendingCleanup = expectedPlayerId;
                }

                MPLocalLoginProfile guest = await m_localLoginRepository.LoadGuestProfileAsync();
                if (guest?.playerId == expectedPlayerId && !string.IsNullOrEmpty(guest.unityProfile))
                {
                    // 同一 PlayerId 也可能保存在 guest Profile（游客曾绑定第三方）。
                    if (!m_inner.SwitchProfile(guest.unityProfile) || !m_inner.ClearSessionToken())
                        throw new InvalidOperationException("Could not clear deleted account credentials.");
                }
                MPCloudSaveManager.Instance.ClearDeletedAccountLocalData(expectedPlayerId);
                await m_localLoginRepository.RemoveDeletedAccountAsync(expectedPlayerId);
                m_deletedAccountPendingCleanup = null;
            }
        }
        finally
        {
            IsDeletingAccount = false;
        }
    }
}
