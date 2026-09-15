using DG.Tweening;
using HQ.UIManager;

[Component("MPHomeView")]
public partial class MPHomeView : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPReleaseFeatures.ApplyHome(transform);
        InitializeTabs();
        RegisterListeners();

        m_selectedTabIndex = HOME_TAB_INDEX;
        m_initialized = true;
        RefreshResponsiveLayout();
        ApplyTabState(false);
        RefreshCurrency();
        InitializeHomePage();
        InitializeCustomEditor();
        InitializeLargerPage();

        MPAudioManager.Instance.PlayBGM(MPMusic.MPBGMMain);
    }

    public override void OnFocus(bool focus)
    {
        if (!m_initialized)
            return;

        if (!focus)
        {
            BlurHomePage();
            return;
        }

        RefreshCurrency();
        RefreshHomePage();
        RefreshCustomEditorFocus();
        RefreshResponsiveLayout();
        ApplyTabState(false);
        // 先恢复页面尺寸与位置，再让虚拟列表计算可见项。
        RefreshLargerPage();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!m_initialized || m_isApplyingLayout)
            return;

        RefreshResponsiveLayout();
        ApplyPagePosition(false);
    }

    public override void OnRelease()
    {
        m_initialized = false;
        ReleaseHomePage();
        ReleaseCustomEditor();
        ReleaseLargerPage();
        UnregisterListeners();
        KillSwitchSequence();
        m_center.DOKill();

        if (m_tabs != null)
        {
            foreach (TabData tab in m_tabs)
            {
                tab.MovingNode.DOKill();
                tab.Label.DOKill();
            }
        }

        m_select.DOKill();
        MPLoad.ReleaseAll(this);
    }
}
