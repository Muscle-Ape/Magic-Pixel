using HQ.UIManager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPPetClaimPop")]
public sealed class MPPetClaimPop : AWindow
{
    /// <summary>
    /// 宠物图片
    /// </summary>
    [TransformPath("View/Window/Pet")] private Image m_pet;
    /// <summary>
    /// 宠物名称
    /// </summary>
    [TransformPath("View/Window/Name")] private TMP_Text m_name;
    /// <summary>
    /// 宠物技能描述
    /// </summary>
    [TransformPath("View/Window/Infos/SkillDesc/Desc/Text")] private TMP_Text m_skillDesc;
    /// <summary>
    /// 宠物解锁来源
    /// </summary>
    [TransformPath("View/Window/Infos/UnlockSource/Desc/Text")] private TMP_Text m_unlockSource;
    /// <summary>
    /// 宠物技能次数
    /// </summary>
    [TransformPath("View/Window/Infos/SkillUses/Desc/Text")] private TMP_Text m_skillUses;
    /// <summary>
    /// 关闭按钮
    /// </summary>
    [TransformPath("View/Window/LaterBtn")] private Button m_laterBtn;
    /// <summary>
    /// 应用宠物按钮
    /// </summary>
    [TransformPath("View/Window/UseNowBtn")] private Button m_useNowBtn;
    /// <summary>
    /// 页面传入数据
    /// </summary>
    private MPPetClaimPopUIMsgData m_data;
    private bool m_claiming;
    private bool m_closing;

    protected override bool ShouldAdaptToNotchScreen() => false;

    /// <summary>里程碑奖励已经入账：Later 只关闭，Use Now 只切换宠物，不再次发奖。</summary>
    public static bool ShowMilestoneNotification(string petId, AWindow source, Action onSelected = null)
    {
        if (source == null || source.IsDestoried || !source.IsFocus) return false;
        MPUser user = MPUser.instance;
        MPPetConfig pet = user.GetPendingMilestonePet(petId);
        if (pet == null) return false;
        string owner = user.GetRewardProgressOwner();
        MPPetClaimPop pop = Show(pet,
            () => user.GetRewardProgressOwner() == owner && user.PetIsUnlock(petId),
            () =>
            {
                if (user.GetRewardProgressOwner() != owner) return;
                user.SetSelectedPet(petId);
                onSelected?.Invoke();
            }, pet.UnlockText, source);
        if (pop == null || pop.IsDestoried) return false;
        // 创建成功才标记已展示；重进主页或重复通关不会再次弹出。
        user.MarkPetUnlockNotificationSeen(petId);
        return true;
    }

    /// <summary>标准宠物奖励领取入口。只在点击 Collect 确认时提交领取记录。</summary>
    public static MPPetClaimPop Show(MPPetConfig pet, Action onClaimed = null,
        string sourceName = null, AWindow sourceWindow = null)
    {
        if (pet == null) throw new ArgumentNullException(nameof(pet));
        string owner = MPUser.instance.GetRewardProgressOwner();
        return Show(pet, () => owner == MPUser.instance.GetRewardProgressOwner()
            && MPUser.instance.TryClaimPet(pet.ID), onClaimed, sourceName, sourceWindow);
    }

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
            pet = pet,
            tryClaim = tryClaim,
            onClaimed = onClaimed,
            sourceName = sourceName,
            sourceWindow = sourceWindow
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
        m_skillDesc.text = pet.ClaimSkillText;
        m_unlockSource.text = pet.UnlockText;
        m_skillUses.text = "x" + pet.SkillUseCount.ToString();
        MPRewardPopupIcons.Load(m_pet, pet.Icon + "_main", this, "popup_pet_placeholder");
        m_pet.SetNativeSize();
        m_useNowBtn.onClick.RemoveListener(OnCollect);
        m_laterBtn.onClick.RemoveListener(OnLater);
        m_useNowBtn.onClick.AddListener(OnCollect);
        m_laterBtn.onClick.AddListener(OnLater);
    }

    private void OnCollect()
    {
        if (m_closing || m_claiming || m_data == null) return;
        if (!SourceIsAlive(m_data.sourceWindow))
        {
            m_useNowBtn.interactable = false;
            return;
        }
        m_claiming = true;
        m_useNowBtn.interactable = m_laterBtn.interactable = false;
        MPPetClaimPopUIMsgData request = m_data;
        try
        {
            bool claimed = request.tryClaim();
            if (this == null || IsDestoried) return;
            if (!claimed)
            {
                return;
            }
            // 只有确认并提交成功才关闭；打开或取消弹窗不会发奖、标记已领或自动选中宠物。
            AWindow source = request.sourceWindow;
            Action onClaimed = request.onClaimed;
            Close(() => { if (SourceIsAlive(source)) onClaimed?.Invoke(); });
        }
        catch (Exception)
        {

        }
        finally
        {
            m_claiming = false;
            if (this != null && !IsDestoried && !m_closing)
                m_useNowBtn.interactable = m_laterBtn.interactable = true;
        }
    }

    private void OnLater()
    {
        if (!m_closing && !m_claiming) Close(null);
    }

    private static bool SourceIsAlive(AWindow source) => ReferenceEquals(source, null) || (source != null && !source.IsDestoried);

    private void Close(Action onClosed)
    {
        m_closing = true;
        m_useNowBtn.interactable = m_laterBtn.interactable = false;
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null) animation.Close(onClosed);
        else { DestroyWindow(); onClosed?.Invoke(); }
    }

    public override void OnRelease()
    {
        if (m_useNowBtn != null) m_useNowBtn.onClick.RemoveListener(OnCollect);
        if (m_laterBtn != null) m_laterBtn.onClick.RemoveListener(OnLater);
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
