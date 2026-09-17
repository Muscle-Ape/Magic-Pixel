using System;
using System.Threading;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>公开关卡举报：单选原因，其他原因必须填写说明；仅服务端确认后提示成功。</summary>
[Component("MPReportPop")]
public sealed class MPReportPop : AWindow
{
    private static readonly string[] Reasons =
    {
        "dislike", "fraud", "political", "bullying", "plagiarism", "sexual", "other"
    };
    [TransformPath("View/Window/Options")] private RectTransform m_options;
    [TransformPath("View/Window/OtherInput")] private TMP_InputField m_otherInput;
    [TransformPath("View/Window/SubmitBtn")] private Button m_submit;
    [TransformPath("View/Window/CloseBtn")] private Button m_close;
    private readonly Button[] m_buttons = new Button[7];
    private readonly Transform[] m_selectedMarks = new Transform[7];
    private readonly UnityAction[] m_actions = new UnityAction[7];
    private CancellationTokenSource m_operation;
    private string m_levelId;
    private string m_playerId;
    private int m_selected = -1;
    private bool m_busy;
    private bool m_closing;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static void Show(string publicLevelId)
    {
        UIManager.Inst.ShowWindow<MPReportPop>(new MPReportPopUIMsgData(publicLevelId), true, UILayer.Top);
    }

    public override void OnCreate()
    {
        for (int i = 0; i < Reasons.Length; i++)
        {
            int index = i;
            Transform option = m_options.Find(Reasons[i]);
            m_buttons[i] = option.GetComponent<Button>();
            m_selectedMarks[i] = option.Find("Selected");
            m_actions[i] = () => Select(index);
            m_buttons[i].onClick.AddListener(m_actions[i]);
        }
        m_otherInput.characterLimit = 300;
        m_otherInput.onValueChanged.AddListener(OnTextChanged);
        m_submit.onClick.AddListener(Submit);
        m_close.onClick.AddListener(Close);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_levelId = uiMsg?.GetMsg<MPReportPopUIMsgData>()?.PublicLevelId;
        m_playerId = MPLoginManager.Instance.PlayerId;
        m_selected = -1;
        m_otherInput.SetTextWithoutNotify(string.Empty);
        Refresh();
    }

    private void Select(int index)
    {
        if (m_busy || m_closing) return;
        m_selected = index;
        Refresh();
    }

    private void OnTextChanged(string _) => Refresh();

    private void Refresh()
    {
        for (int i = 0; i < m_buttons.Length; i++)
        {
            m_buttons[i].interactable = !m_busy && !m_closing;
            m_selectedMarks[i].gameObject.SetActive(m_selected == i);
        }
        bool other = m_selected == Reasons.Length - 1;
        m_otherInput.gameObject.SetActive(other);
        m_otherInput.interactable = !m_busy && !m_closing;
        m_submit.interactable = !m_busy && !m_closing && !string.IsNullOrEmpty(m_levelId) &&
            m_selected >= 0 && (!other || !string.IsNullOrWhiteSpace(m_otherInput.text));
        m_close.interactable = !m_busy && !m_closing;
    }

    private async void Submit()
    {
        if (!m_submit.interactable) return;
        if (MPLoginManager.Instance.PlayerId != m_playerId)
        {
            UnityToast.Instance.ShowToast("Your account changed. Please reopen this report.");
            return;
        }
        m_busy = true;
        Refresh();
        // 新版预制体不再包含 Status；提交期间通过禁用控件防重入，异常使用 Toast 提示。
        m_operation?.Dispose();
        var operation = new CancellationTokenSource();
        m_operation = operation;
        try
        {
            await MPCommunityModeration.SubmitAsync(m_levelId, Reasons[m_selected],
                m_selected == Reasons.Length - 1 ? m_otherInput.text.Trim() : string.Empty, operation.Token);
            if (this == null || IsDestoried || operation.IsCancellationRequested) return;
            UnityToast.Instance.ShowToast("Report submitted. Thank you for your feedback.");
            m_busy = false;
            Close();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPReportPop] 举报提交失败：{exception.GetType().Name}");
            if (this != null && !IsDestoried)
                UnityToast.Instance.ShowToast("Unable to submit. Check your connection and try again.");
        }
        finally
        {
            if (this != null && !IsDestoried && !m_closing)
            {
                m_busy = false;
                Refresh();
            }
        }
    }

    private void Close()
    {
        if (m_busy || m_closing) return;
        m_closing = true;
        Refresh();
        var animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null) animation.Close(null);
        else DestroyWindow();
    }

    public override void OnRelease()
    {
        m_operation?.Cancel();
        m_operation?.Dispose();
        m_operation = null;
        for (int i = 0; i < m_buttons.Length; i++)
            if (m_buttons[i] != null) m_buttons[i].onClick.RemoveListener(m_actions[i]);
        m_otherInput.onValueChanged.RemoveListener(OnTextChanged);
        m_submit.onClick.RemoveListener(Submit);
        m_close.onClick.RemoveListener(Close);
        base.OnRelease();
    }
}

public sealed class MPReportPopUIMsgData : UIMsgData
{
    public string PublicLevelId { get; }
    public MPReportPopUIMsgData(string publicLevelId) { PublicLevelId = publicLevelId; }
}
