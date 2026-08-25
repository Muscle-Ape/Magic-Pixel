using DG.Tweening;
using HQ.UIManager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class MPHomeView
{
    private void RegisterCustomUI()
    {
        UnregisterCustomUI();
        InitializeCustomPalettePointerButton();
        m_customPenBtn.onClick.AddListener(OnCustomPenClick);
        m_customFillBtn.onClick.AddListener(OnCustomFillClick);
        m_customSizeFiveBtn.onClick.AddListener(OnCustomFiveSizeClick);
        m_customSizeTenBtn.onClick.AddListener(OnCustomTenSizeClick);
        m_customPaletteBtn.onClick.AddListener(OnCustomPaletteClick);
        m_customPaletteCompletedBtn.onClick.AddListener(OnCustomPaletteCompletedClick);
        if (m_customPalettePointerBtn != null)
            m_customPalettePointerBtn.onClick.AddListener(OnCustomPalettePointerClick);
        m_customSaveBtn.onClick.AddListener(OnCustomSaveClick);
        m_customPublishBtn.onClick.AddListener(OnCustomUploadClick);
        m_customWarehouseBtn.onClick.AddListener(OnCustomWarehouseClick);
        m_customCommunityBtn.onClick.AddListener(OnCustomCommunityClick);

        MPCustomLevelPublishManager.Instance.PublishStateChanged -= OnCustomPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishStateChanged += OnCustomPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged -= OnCustomPublishOperationChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged += OnCustomPublishOperationChanged;
    }

    private void UnregisterCustomUI()
    {
        if (m_customPenBtn != null)
            m_customPenBtn.onClick.RemoveListener(OnCustomPenClick);
        if (m_customFillBtn != null)
            m_customFillBtn.onClick.RemoveListener(OnCustomFillClick);
        if (m_customSizeFiveBtn != null)
            m_customSizeFiveBtn.onClick.RemoveListener(OnCustomFiveSizeClick);
        if (m_customSizeTenBtn != null)
            m_customSizeTenBtn.onClick.RemoveListener(OnCustomTenSizeClick);
        if (m_customPaletteBtn != null)
            m_customPaletteBtn.onClick.RemoveListener(OnCustomPaletteClick);
        if (m_customPaletteCompletedBtn != null)
            m_customPaletteCompletedBtn.onClick.RemoveListener(OnCustomPaletteCompletedClick);
        if (m_customPalettePointerBtn != null)
            m_customPalettePointerBtn.onClick.RemoveListener(OnCustomPalettePointerClick);
        if (m_customSaveBtn != null)
            m_customSaveBtn.onClick.RemoveListener(OnCustomSaveClick);
        if (m_customPublishBtn != null)
            m_customPublishBtn.onClick.RemoveListener(OnCustomUploadClick);
        if (m_customWarehouseBtn != null)
            m_customWarehouseBtn.onClick.RemoveListener(OnCustomWarehouseClick);
        if (m_customCommunityBtn != null)
            m_customCommunityBtn.onClick.RemoveListener(OnCustomCommunityClick);

        for (int i = 0; i < m_customQuickColorBindings.Count; i++)
        {
            CustomQuickColorBinding binding = m_customQuickColorBindings[i];
            if (binding.Button != null && binding.Callback != null)
                binding.Button.onClick.RemoveListener(binding.Callback);
        }
        m_customQuickColorBindings.Clear();

        MPCustomLevelPublishManager.Instance.PublishStateChanged -= OnCustomPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged -= OnCustomPublishOperationChanged;
    }

    private void InitializeCustomPalette()
    {
        if (m_customPalettePanelCanvasGroup == null)
            return;

        m_customPalette = m_customPalettePanelCanvasGroup.GetComponent<MPPalette>();
        if (m_customPalette == null)
            m_customPalette = m_customPalettePanelCanvasGroup.gameObject.AddComponent<MPPalette>();
        m_customPalette.Initialization(SetCustomColor);

        if (ColorUtility.TryParseHtmlString(DEFAULT_CUSTOM_COLOR, out Color defaultColor))
            m_customPalette.SetPaletteColor(defaultColor);

        InitializeCustomQuickColors();
    }

    /// <summary>
    /// Pointer 是调色板背后的透明点击层，预制体只需要保留 Image，按钮组件由代码补齐。
    /// </summary>
    private void InitializeCustomPalettePointerButton()
    {
        if (m_customPalettePointer == null)
            return;

        m_customPalettePointerBtn = m_customPalettePointer.GetComponent<Button>();
        if (m_customPalettePointerBtn == null)
            m_customPalettePointerBtn = m_customPalettePointer.gameObject.AddComponent<Button>();

        m_customPalettePointerBtn.targetGraphic = m_customPalettePointer;
        m_customPalettePointerBtn.transition = Selectable.Transition.None;
        m_customPalettePointerBtn.interactable = false;
    }

    private void InitializeCustomQuickColors()
    {
        Transform colorsRoot = m_customBtnsCanvasGroup == null
            ? null
            : m_customBtnsCanvasGroup.transform.Find("ColorFrame/Colors");
        if (colorsRoot == null)
        {
            Debug.LogError("MPHomeView Custom 页面缺少 Btns/ColorFrame/Colors 节点。");
            return;
        }

        int count = Mathf.Min(colorsRoot.childCount, CUSTOM_QUICK_COLOR_HEXES.Length);
        for (int i = 0; i < count; i++)
        {
            Transform colorNode = colorsRoot.GetChild(i);
            Button button = colorNode.GetComponent<Button>();
            if (button == null
                || !ColorUtility.TryParseHtmlString(CUSTOM_QUICK_COLOR_HEXES[i], out Color color))
            {
                continue;
            }

            Image swatch = colorNode.Find("Image")?.GetComponent<Image>();
            if (swatch != null)
                swatch.color = color;

            int colorIndex = i;
            UnityEngine.Events.UnityAction callback = () => OnCustomQuickColorClick(colorIndex);
            button.onClick.AddListener(callback);
            m_customQuickColorBindings.Add(new CustomQuickColorBinding
            {
                Button = button,
                Callback = callback
            });
        }

        if (count < CUSTOM_QUICK_COLOR_HEXES.Length)
            Debug.LogWarning($"Custom 快捷颜色节点不足：需要 {CUSTOM_QUICK_COLOR_HEXES.Length}，当前 {count}。");
    }

    private void SetCustomColor(Color color)
    {
        m_customCurrentColor = color;
        if (m_customCurrentColorImage != null)
            m_customCurrentColorImage.color = color;
    }

    private void OnCustomQuickColorClick(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= CUSTOM_QUICK_COLOR_HEXES.Length)
            return;
        if (!ColorUtility.TryParseHtmlString(CUSTOM_QUICK_COLOR_HEXES[colorIndex], out Color color))
            return;

        if (m_customPalette != null)
            m_customPalette.SetPaletteColor(color);
        else
            SetCustomColor(color);

        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    private void OnCustomPaletteClick()
    {
        OpenCustomPalette(true);
    }

    private void OnCustomPaletteCompletedClick()
    {
        CloseCustomPalette(true);
    }

    private void OnCustomPalettePointerClick()
    {
        CloseCustomPalette(true);
    }

    private void OpenCustomPalette(bool animated)
    {
        if (!m_customInitialized
            || m_customPalettePanelCanvasGroup == null
            || m_customBtnsCanvasGroup == null)
        {
            return;
        }

        KillCustomPaletteSequence();
        m_customPaletteOpen = true;
        RectTransform panelRect = m_customPalettePanelCanvasGroup.transform as RectTransform;
        m_customPalettePanelCanvasGroup.gameObject.SetActive(true);
        if (m_customPalettePointer != null)
            m_customPalettePointer.gameObject.SetActive(true);
        if (m_customPalettePointerBtn != null)
            m_customPalettePointerBtn.interactable = true;
        m_customPalettePanelCanvasGroup.interactable = false;
        m_customPalettePanelCanvasGroup.blocksRaycasts = false;
        m_customBtnsCanvasGroup.interactable = false;
        m_customBtnsCanvasGroup.blocksRaycasts = false;

        if (!animated)
        {
            m_customPalettePanelCanvasGroup.alpha = 1f;
            if (panelRect != null)
                panelRect.localScale = Vector3.one;
            m_customBtnsCanvasGroup.alpha = 0f;
            m_customPalettePanelCanvasGroup.interactable = true;
            m_customPalettePanelCanvasGroup.blocksRaycasts = true;
            return;
        }

        m_customPalettePanelCanvasGroup.alpha = 0f;
        if (panelRect != null)
            panelRect.localScale = Vector3.one * 0.86f;

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject)
            .Join(m_customPalettePanelCanvasGroup.DOFade(1f, CUSTOM_PALETTE_ANIMATION_DURATION));
        if (panelRect != null)
        {
            sequence.Join(panelRect.DOScale(1f, CUSTOM_PALETTE_ANIMATION_DURATION)
                .SetEase(Ease.OutBack));
        }
        sequence.Join(m_customBtnsCanvasGroup.DOFade(0f, CUSTOM_PALETTE_ANIMATION_DURATION));
        m_customPaletteSequence = sequence;
        sequence.OnComplete(() =>
        {
            if (m_customPaletteSequence != sequence)
                return;

            m_customPaletteSequence = null;
            m_customPalettePanelCanvasGroup.interactable = true;
            m_customPalettePanelCanvasGroup.blocksRaycasts = true;
        });
        sequence.OnKill(() =>
        {
            if (m_customPaletteSequence == sequence)
                m_customPaletteSequence = null;
        });
    }

    private void CloseCustomPalette(bool animated)
    {
        if (m_customPalettePanelCanvasGroup == null || m_customBtnsCanvasGroup == null)
            return;

        KillCustomPaletteSequence();
        m_customPaletteOpen = false;
        RectTransform panelRect = m_customPalettePanelCanvasGroup.transform as RectTransform;
        m_customPalettePanelCanvasGroup.interactable = false;
        m_customPalettePanelCanvasGroup.blocksRaycasts = false;
        if (m_customPalettePointerBtn != null)
            m_customPalettePointerBtn.interactable = false;
        m_customBtnsCanvasGroup.gameObject.SetActive(true);
        m_customBtnsCanvasGroup.interactable = false;
        m_customBtnsCanvasGroup.blocksRaycasts = false;

        if (!animated || !m_customPalettePanelCanvasGroup.gameObject.activeSelf)
        {
            m_customPalettePanelCanvasGroup.alpha = 0f;
            if (panelRect != null)
                panelRect.localScale = Vector3.one * 0.86f;
            m_customPalettePanelCanvasGroup.gameObject.SetActive(false);
            if (m_customPalettePointer != null)
                m_customPalettePointer.gameObject.SetActive(false);
            m_customBtnsCanvasGroup.alpha = 1f;
            m_customBtnsCanvasGroup.interactable = true;
            m_customBtnsCanvasGroup.blocksRaycasts = true;
            return;
        }

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject)
            .Join(m_customPalettePanelCanvasGroup.DOFade(0f, CUSTOM_PALETTE_ANIMATION_DURATION));
        if (panelRect != null)
        {
            sequence.Join(panelRect.DOScale(0.86f, CUSTOM_PALETTE_ANIMATION_DURATION)
                .SetEase(Ease.InBack));
        }
        sequence.Join(m_customBtnsCanvasGroup.DOFade(1f, CUSTOM_PALETTE_ANIMATION_DURATION));
        m_customPaletteSequence = sequence;
        sequence.OnComplete(() =>
        {
            if (m_customPaletteSequence != sequence)
                return;

            m_customPaletteSequence = null;
            m_customPalettePanelCanvasGroup.gameObject.SetActive(false);
            if (m_customPalettePointer != null)
                m_customPalettePointer.gameObject.SetActive(false);
            m_customBtnsCanvasGroup.interactable = true;
            m_customBtnsCanvasGroup.blocksRaycasts = true;
        });
        sequence.OnKill(() =>
        {
            if (m_customPaletteSequence == sequence)
                m_customPaletteSequence = null;
        });
    }

    private void KillCustomPaletteSequence()
    {
        Sequence previousSequence = m_customPaletteSequence;
        m_customPaletteSequence = null;
        if (previousSequence != null && previousSequence.IsActive())
            previousSequence.Kill();

        if (m_customPalettePanelCanvasGroup != null)
        {
            m_customPalettePanelCanvasGroup.DOKill();
            m_customPalettePanelCanvasGroup.transform.DOKill();
        }
        if (m_customBtnsCanvasGroup != null)
            m_customBtnsCanvasGroup.DOKill();
    }

    private void OnCustomPenClick()
    {
        SetCustomMode(false);
    }

    private void OnCustomFillClick()
    {
        SetCustomMode(true);
    }

    private void SetCustomMode(bool isFillMode)
    {
        m_customIsFillMode = isFillMode;
        RefreshCustomModeState();
    }

    private void RefreshCustomModeState()
    {
        if (m_customBlocks != null)
        {
            for (int i = 0; i < m_customBlocks.Count; i++)
                m_customBlocks[i].SetMode(m_customIsFillMode);
        }

        SetCustomNodeActive(m_customPenOpen, !m_customIsFillMode);
        SetCustomNodeActive(m_customPenText, m_customIsFillMode);
        SetCustomNodeActive(m_customFillOpen, m_customIsFillMode);
        SetCustomNodeActive(m_customFillText, !m_customIsFillMode);
    }

    private void OnCustomFiveSizeClick()
    {
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
        SetCustomSize(false);
    }

    private void OnCustomTenSizeClick()
    {
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
        SetCustomSize(true);
    }

    private void SetCustomSize(bool isTenSize)
    {
        if (m_customIsTenSize == isTenSize)
        {
            RefreshCustomSizeState();
            return;
        }

        BeginNewCustomPublishDraft();
        m_customIsTenSize = isTenSize;
        CreateCustomGrid(isTenSize ? 10 : 5);
        RefreshCustomSizeState();
        RefreshCustomModeState();
    }

    private void RefreshCustomSizeState()
    {
        // 两个按钮始终可点击，只互斥显示其下代表当前尺寸的 Frame。
        SetCustomNodeActive(m_customSizeFiveBtn, true);
        SetCustomNodeActive(m_customSizeTenBtn, true);
        SetCustomNodeActive(m_customSizeFiveFrame, !m_customIsTenSize);
        SetCustomNodeActive(m_customSizeTenFrame, m_customIsTenSize);
    }

    private static void SetCustomNodeActive(Component node, bool active)
    {
        if (node != null && node.gameObject.activeSelf != active)
            node.gameObject.SetActive(active);
    }

    private void OnCustomWarehouseClick()
    {
        UIManager.Inst.ShowWindow<MPCustomLevelView>();
    }

    private void OnCustomCommunityClick()
    {
        UIManager.Inst.ShowWindow<MPCommunityView>();
    }
}
