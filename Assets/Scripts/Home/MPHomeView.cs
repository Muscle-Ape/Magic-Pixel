using DG.Tweening;
using HQ.UIManager;

[Component("MPHomeView")]
public partial class MPHomeView : AWindow
{
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        InitializeTabs();
        RegisterListeners();

        m_selectedTabIndex = HOME_TAB_INDEX;
        m_initialized = true;
        RefreshResponsiveLayout();
        ApplyTabState(false);
        RefreshCurrency();
        InitializeHomePage();
        InitializeCustomEditor();

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
