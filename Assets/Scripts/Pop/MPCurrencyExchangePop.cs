using System;
using System.Globalization;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Coin 按 1:10 转换为 Fluorite 的货币转换弹窗。</summary>
[Component("MPCurrencyExchangePop")]
public sealed class MPCurrencyExchangePop : AWindow
{
    public const int FLUORITE_PER_COIN = 10;
    private const float HOLD_THRESHOLD = 0.45f;
    private const float HOLD_REPEAT_INTERVAL = 0.08f;

    [TransformPath("View/Window/Sub")] private Button m_subButton;
    [TransformPath("View/Window/Sub/Disable")] private Image m_subDisable;
    [TransformPath("View/Window/Add")] private Button m_addButton;
    [TransformPath("View/Window/Add/Disable")] private Image m_addDisable;
    [TransformPath("View/Window/CoinCount")] private TMP_InputField m_coinInput;
    [TransformPath("View/Window/Fluorite/Count")] private TMP_Text m_fluoriteCountText;
    [TransformPath("View/Window/ConfirmBtn")] private Button m_confirmButton;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeButton;

    private MPCurrencyExchangePopUIMsgData m_data;
    private MPPressAndHoldButton m_subPress;
    private MPPressAndHoldButton m_addPress;
    private int m_coinAmount;
    private int m_maxCoinAmount;
    private bool m_refreshingInput;
    private bool m_busy;
    private bool m_closing;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static MPCurrencyExchangePop Show(Action<int, int> onExchanged = null)
    {
        return UIManager.Inst.ShowWindow<MPCurrencyExchangePop>(
            new MPCurrencyExchangePopUIMsgData(onExchanged), true, UILayer.Top);
    }

    public override void OnCreate()
    {
        // Sub/Add 的单击与长按都由指针组件统一处理，避免长按松手后
        // Button.onClick 再额外增减一次。MPButton 仍保留自带的按压缩放和点击音效。
        m_subPress = EnsurePressAndHold(m_subButton);
        m_addPress = EnsurePressAndHold(m_addButton);
        m_subPress.Initialize(() => TryAdjustCoin(-1), HOLD_THRESHOLD, HOLD_REPEAT_INTERVAL);
        m_addPress.Initialize(() => TryAdjustCoin(1), HOLD_THRESHOLD, HOLD_REPEAT_INTERVAL);

        m_confirmButton.onClick.AddListener(OnConfirmClick);
        m_closeButton.onClick.AddListener(OnCloseClick);
        m_coinInput.onValueChanged.AddListener(OnCoinInputChanged);
        m_coinInput.onEndEdit.AddListener(OnCoinInputEndEdit);

        // IntegerNumber 在某些软键盘上仍可能输入符号，再用字符校验只放行 0-9。
        m_coinInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        m_coinInput.characterValidation = TMP_InputField.CharacterValidation.Integer;
        m_coinInput.keyboardType = TouchScreenKeyboardType.NumberPad;
        m_coinInput.characterLimit = 10;
        m_coinInput.onValidateInput = ValidateDigit;
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg?.GetMsg<MPCurrencyExchangePopUIMsgData>();
        m_busy = false;
        m_closing = false;
        RefreshOwnedAssets();
        SetCoinAmount(0, true);
    }

    private static char ValidateDigit(string text, int charIndex, char addedChar)
    {
        return addedChar >= '0' && addedChar <= '9' ? addedChar : '\0';
    }

    private static MPPressAndHoldButton EnsurePressAndHold(Button button)
    {
        MPPressAndHoldButton press = button.GetComponent<MPPressAndHoldButton>();
        return press != null ? press : button.gameObject.AddComponent<MPPressAndHoldButton>();
    }

    /// <summary>边界状态下按钮仍然可点击，仅不执行增减。</summary>
    private bool TryAdjustCoin(int delta)
    {
        if (m_busy || m_closing)
            return false;

        int target = Mathf.Clamp(m_coinAmount + delta, 0, m_maxCoinAmount);
        if (target == m_coinAmount)
            return false;

        SetCoinAmount(target, true);
        return true;
    }

    private void OnCoinInputChanged(string value)
    {
        if (m_refreshingInput || m_busy || m_closing)
            return;

        if (string.IsNullOrEmpty(value))
        {
            SetCoinAmount(0, false);
            return;
        }

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed))
        {
            SetCoinAmount(0, true);
            return;
        }

        int clamped = (int)Math.Min(Math.Max(parsed, 0L), m_maxCoinAmount);
        // 超出持有数量时立即把输入框改为最大值。
        SetCoinAmount(clamped, parsed != clamped);
    }

    private void OnCoinInputEndEdit(string _)
    {
        if (!m_busy && !m_closing)
            SetCoinAmount(m_coinAmount, true);
    }

    private void SetCoinAmount(int amount, bool updateInput)
    {
        m_coinAmount = Mathf.Clamp(amount, 0, m_maxCoinAmount);
        if (updateInput && m_coinInput.text != m_coinAmount.ToString(CultureInfo.InvariantCulture))
        {
            m_refreshingInput = true;
            m_coinInput.SetTextWithoutNotify(m_coinAmount.ToString(CultureInfo.InvariantCulture));
            m_coinInput.ForceLabelUpdate();
            m_refreshingInput = false;
        }

        long fluorite = (long)m_coinAmount * FLUORITE_PER_COIN;
        m_fluoriteCountText.text = fluorite.ToString(CultureInfo.InvariantCulture);
        RefreshInteractable();
    }

    private void RefreshOwnedAssets()
    {
        m_maxCoinAmount = Mathf.Max(0, MPUser.instance.GetCoins());
    }

    private void RefreshInteractable()
    {
        bool available = !m_busy && !m_closing;
        bool canSub = m_coinAmount > 0;
        bool canAdd = m_coinAmount < m_maxCoinAmount;

        // 不用 interactable 表达数值边界，保留按钮点击，由 Disable 节点负责视觉状态。
        m_subButton.interactable = available;
        m_addButton.interactable = available;
        m_subDisable.gameObject.SetActive(!canSub);
        m_addDisable.gameObject.SetActive(!canAdd);
        m_coinInput.interactable = available;
        m_confirmButton.interactable = available && m_coinAmount > 0;
        m_closeButton.interactable = available;
    }

    private void OnConfirmClick()
    {
        if (m_busy || m_closing || m_coinAmount <= 0)
            return;

        m_busy = true;
        RefreshInteractable();
        int spentCoins = m_coinAmount;
        if (!MPUser.instance.TryExchangeCoinsForFluorite(
                spentCoins, FLUORITE_PER_COIN, out int receivedFluorite))
        {
            m_busy = false;
            RefreshOwnedAssets();
            SetCoinAmount(Mathf.Min(spentCoins, m_maxCoinAmount), true);
            UnityToast.Instance?.ShowToast("Unable to exchange. Please try again.");
            return;
        }

        m_busy = false;
        RefreshOwnedAssets();
        SetCoinAmount(0, true);
        Action<int, int> onExchanged = m_data?.OnExchanged;
        onExchanged?.Invoke(spentCoins, receivedFluorite);
        if (this == null || IsDestoried)
            return;
        UnityToast.Instance?.ShowToast($"Received {receivedFluorite} Fluorite.");
        Close();
    }

    private void OnCloseClick()
    {
        Close();
    }

    private void Close()
    {
        if (m_busy || m_closing)
            return;
        m_closing = true;
        RefreshInteractable();
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null)
            animation.Close(null);
        else
            DestroyWindow();
    }

    public override void OnRelease()
    {
        m_subPress?.Release();
        m_addPress?.Release();
        m_subPress = null;
        m_addPress = null;
        m_confirmButton.onClick.RemoveListener(OnConfirmClick);
        m_closeButton.onClick.RemoveListener(OnCloseClick);
        m_coinInput.onValueChanged.RemoveListener(OnCoinInputChanged);
        m_coinInput.onEndEdit.RemoveListener(OnCoinInputEndEdit);
        m_coinInput.onValidateInput = null;
        m_data = null;
        base.OnRelease();
    }
}

public sealed class MPCurrencyExchangePopUIMsgData : UIMsgData
{
    public Action<int, int> OnExchanged { get; }

    public MPCurrencyExchangePopUIMsgData(Action<int, int> onExchanged)
    {
        OnExchanged = onExchanged;
    }
}
