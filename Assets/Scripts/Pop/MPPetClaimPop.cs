using HQ.UIManager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPPetClaimPop")]
public sealed class MPPetClaimPop : AWindow
{
    [TransformPath("View/Window/Icon")] private Image m_icon;
    [TransformPath("View/Window/Name")] private TMP_Text m_name;
    [TransformPath("View/Window/Tag")] private TMP_Text m_tag;
    [TransformPath("View/Window/Skill")] private TMP_Text m_skill;
    [TransformPath("View/Window/Source")] private TMP_Text m_source;
    [TransformPath("View/Window/CollectBtn")] private Button m_collectBtn;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeBtn;
    private MPPetClaimPopUIMsgData m_data;
    private bool m_claiming;
    private bool m_closing;

    protected override bool ShouldAdaptToNotchScreen() => false;

    /// <summary>
    /// 由实际宠物奖励的领取入口主动调用，不用于主线条件达成后的自动解锁通知。
    /// tryClaim 由奖励来源校验资格并幂等提交存档，成功后返回 true；弹窗不擅自切换宠物。
    /// </summary>
    public static MPPetClaimPop Show(MPPetConfig pet, Func<bool> tryClaim, Action onClaimed = null,
        string sourceName = null, AWindow sourceWindow = null)
    {
        if (pet == null) throw new ArgumentNullException(nameof(pet));
        if (tryClaim == null) throw new ArgumentNullException(nameof(tryClaim));
        return UIManager.Inst.ShowWindow<MPPetClaimPop>(new MPPetClaimPopUIMsgData
        {
            pet = pet, tryClaim = tryClaim, onClaimed = onClaimed,
            sourceName = sourceName, sourceWindow = sourceWindow
        }, true, UILayer.Top);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg as MPPetClaimPopUIMsgData;
        if (m_data?.pet == null || m_data.tryClaim == null)
        {
            DestroyWindow();
            return;
        }
        MPPetConfig pet = m_data.pet;
        m_name.text = pet.Name;
        m_tag.text = pet.Tag;
        m_skill.text = pet.ClaimSkillText;
        m_source.text = m_data.sourceName ?? "Pet reward";
        MPRewardPopupIcons.Load(m_icon, pet.Icon, this, "popup_pet_placeholder");
        m_collectBtn.onClick.RemoveListener(OnCollect);
        m_closeBtn.onClick.RemoveListener(OnClose);
        m_collectBtn.onClick.AddListener(OnCollect);
        m_closeBtn.onClick.AddListener(OnClose);
    }

    private void OnCollect()
    {
        if (m_closing || m_claiming || m_data == null) return;
        if (!SourceIsAlive(m_data.sourceWindow))
        {
            m_source.text = "This reward is no longer available. Please return and try again.";
            m_collectBtn.interactable = false;
            return;
        }
        m_claiming = true;
        m_collectBtn.interactable = m_closeBtn.interactable = false;
        MPPetClaimPopUIMsgData request = m_data;
        try
        {
            bool claimed = request.tryClaim();
            if (this == null || IsDestoried) return;
            if (!claimed)
            {
                m_source.text = "Could not claim this pet reward. Please try again.";
                return;
            }
            // 只有确认并提交成功才关闭；打开或取消弹窗不会发奖、标记已领或自动选中宠物。
            AWindow source = request.sourceWindow;
            Action onClaimed = request.onClaimed;
            Close(() => { if (SourceIsAlive(source)) onClaimed?.Invoke(); });
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPPetClaimPop] 领取失败：{exception.GetType().Name}");
            if (this != null && !IsDestoried)
                m_source.text = "Could not claim this pet reward. Please try again.";
        }
        finally
        {
            m_claiming = false;
            if (this != null && !IsDestoried && !m_closing)
                m_collectBtn.interactable = m_closeBtn.interactable = true;
        }
    }

    private void OnClose()
    {
        if (!m_closing && !m_claiming) Close(null);
    }

    private static bool SourceIsAlive(AWindow source) => ReferenceEquals(source, null) || (source != null && !source.IsDestoried);

    private void Close(Action onClosed)
    {
        m_closing = true;
        m_collectBtn.interactable = m_closeBtn.interactable = false;
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null) animation.Close(onClosed);
        else { DestroyWindow(); onClosed?.Invoke(); }
    }

    public override void OnRelease()
    {
        if (m_collectBtn != null) m_collectBtn.onClick.RemoveListener(OnCollect);
        if (m_closeBtn != null) m_closeBtn.onClick.RemoveListener(OnClose);
        m_data = null;
        MPLoad.ReleaseAll(this);
    }
}

public sealed class MPPetClaimPopUIMsgData : UIMsgData
{
    public MPPetConfig pet;
    public string sourceName;
    public AWindow sourceWindow;
    public Func<bool> tryClaim;
    public Action onClaimed;
}
