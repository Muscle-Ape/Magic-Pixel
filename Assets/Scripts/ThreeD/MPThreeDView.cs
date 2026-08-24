using System;
using System.Collections;
using System.Collections.Generic;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 3D 拼装模式的 UGUI 窗口。
/// 视图只负责编排输入、状态反馈和存档节流，实际 3D 逻辑集中在
/// MPThreeDWorldController 中。
/// </summary>
[Component("MPThreeDView")]
public sealed class MPThreeDView : AWindow
{
    private const float AutoSaveDelay = 0.8f;
    private const float ClearConfirmDuration = 3f;
    private const float PartCardHeight = 144f;

    private static readonly Color ReadyColor =
        new Color32(236, 243, 255, 255);
    private static readonly Color ValidColor =
        new Color32(92, 222, 151, 255);
    private static readonly Color WarningColor =
        new Color32(255, 201, 92, 255);
    private static readonly Color InvalidColor =
        new Color32(255, 105, 105, 255);
    private static readonly Color CardColor =
        new Color32(58, 72, 97, 255);
    private static readonly Color CardTextColor =
        new Color32(242, 247, 255, 255);

    [TransformPath("SceneViewport")]
    private RawImage m_sceneViewport;

    [TransformPath("View/Header/BackBtn")]
    private Button m_backBtn;

    [TransformPath("View/Header/TitleText")]
    private TMP_Text m_titleText;

    [TransformPath("View/Header/GridBtn")]
    private Button m_gridBtn;

    [TransformPath("View/Header/ConnectionBtn")]
    private Button m_connectionBtn;

    [TransformPath("View/Header/SaveBtn")]
    private Button m_saveBtn;

    [TransformPath("View/StatusPanel/StatusText")]
    private TMP_Text m_statusText;

    [TransformPath("View/StatusPanel/HintText")]
    private TMP_Text m_hintText;

    [TransformPath("View/PartPanel/ScrollView/Viewport/Content")]
    private RectTransform m_partContent;

    [TransformPath("View/ToolPanel/CheckBtn")]
    private Button m_checkBtn;

    [TransformPath("View/ToolPanel/ClearBtn")]
    private Button m_clearBtn;

    [TransformPath("View/BottomBar/RotationSnapBtn")]
    private Button m_rotationSnapBtn;

    [TransformPath("View/BottomBar/DuplicateBtn")]
    private Button m_duplicateBtn;

    [TransformPath("View/BottomBar/DeleteBtn")]
    private Button m_deleteBtn;

    private readonly List<Button> m_partButtons = new List<Button>();
    private readonly List<GameObject> m_partCards = new List<GameObject>();

    private MPThreeDWorldController m_worldController;
    private MPThreeDViewportInput m_viewportInput;
    private Coroutine m_autoSaveCoroutine;
    private Coroutine m_clearConfirmCoroutine;
    private bool m_runtimeInitialized;
    private bool m_isLoadingState;
    private bool m_savePending;
    private bool m_released;
    private bool m_hasFocus = true;
    private float m_clearConfirmDeadline = -1f;

    public override void OnCreate()
    {
        m_released = false;
        RegisterUI();
        BuildPartCards();
        SetTitle("3D BUILDER");
        SetStatus("READY", ReadyColor);
        SetHint("SELECT A PART TO START");
        RefreshControls();
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        if (m_runtimeInitialized || m_released)
        {
            return;
        }

        InitializeRuntime();
    }

    public override void OnFocus(bool focus)
    {
        m_hasFocus = focus;
        // UIManager 首次打开窗口时会先 OnFocus，再 LoadUIMsgData。
        if (!m_runtimeInitialized || m_released || m_viewportInput == null)
        {
            return;
        }

        m_viewportInput.enabled = focus;
    }

    public override void OnRelease()
    {
        ReleaseRuntime();
    }

    private void OnDestroy()
    {
        ReleaseRuntime();
    }

    private void InitializeRuntime()
    {
        try
        {
            m_worldController = GetComponent<MPThreeDWorldController>();
            if (m_worldController == null)
            {
                m_worldController = gameObject.AddComponent<MPThreeDWorldController>();
            }

            SubscribeControllerEvents();

            float renderScale = Mathf.Min(
                1f,
                2048f / Mathf.Max(Screen.width, Screen.height));
            int renderWidth = Mathf.Max(
                256,
                Mathf.RoundToInt(Screen.width * renderScale));
            int renderHeight = Mathf.Max(
                256,
                Mathf.RoundToInt(Screen.height * renderScale));
            m_worldController.Initialize(renderWidth, renderHeight);
            m_sceneViewport.texture = m_worldController.RenderTexture;

            m_viewportInput =
                m_sceneViewport.GetComponent<MPThreeDViewportInput>();
            if (m_viewportInput == null)
            {
                m_viewportInput =
                    m_sceneViewport.gameObject.AddComponent<MPThreeDViewportInput>();
            }

            m_viewportInput.Initialize(
                m_worldController,
                m_sceneViewport.rectTransform);
            m_viewportInput.enabled = m_hasFocus;

            m_runtimeInitialized = true;
            LoadSavedState();
            RefreshControls();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[ThreeD] Runtime initialization failed: {exception}");
            SetStatus("INITIALIZATION FAILED", InvalidColor);
            SetHint("RETURN AND TRY AGAIN");
            m_runtimeInitialized = false;
            UnsubscribeControllerEvents();
            if (m_viewportInput != null)
            {
                m_viewportInput.Shutdown();
                m_viewportInput.enabled = false;
                m_viewportInput = null;
            }
            ShutdownController();
            RefreshControls();
        }
    }

    private void LoadSavedState()
    {
        MPThreeDAssemblySaveDto state = null;
        try
        {
            state = MPThreeDModuleServices.Storage?.Load();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ThreeD] Load failed, using an empty build: {exception}");
        }

        m_isLoadingState = true;
        try
        {
            m_worldController.LoadState(
                state ?? MPThreeDAssemblySaveDto.CreateEmpty());
        }
        finally
        {
            m_isLoadingState = false;
        }
    }

    private void RegisterUI()
    {
        RegisterButton(m_backBtn, OnBackClick);
        RegisterButton(m_gridBtn, OnGridClick);
        RegisterButton(m_connectionBtn, OnConnectionClick);
        RegisterButton(m_saveBtn, OnSaveClick);
        RegisterButton(m_checkBtn, OnCheckClick);
        RegisterButton(m_clearBtn, OnClearClick);
        RegisterButton(m_rotationSnapBtn, OnRotationSnapClick);
        RegisterButton(m_duplicateBtn, OnDuplicateClick);
        RegisterButton(m_deleteBtn, OnDeleteClick);
    }

    private void UnregisterUI()
    {
        UnregisterButton(m_backBtn, OnBackClick);
        UnregisterButton(m_gridBtn, OnGridClick);
        UnregisterButton(m_connectionBtn, OnConnectionClick);
        UnregisterButton(m_saveBtn, OnSaveClick);
        UnregisterButton(m_checkBtn, OnCheckClick);
        UnregisterButton(m_clearBtn, OnClearClick);
        UnregisterButton(m_rotationSnapBtn, OnRotationSnapClick);
        UnregisterButton(m_duplicateBtn, OnDuplicateClick);
        UnregisterButton(m_deleteBtn, OnDeleteClick);
    }

    private void SubscribeControllerEvents()
    {
        if (m_worldController == null)
        {
            return;
        }

        m_worldController.ValidationChanged -= OnValidationChanged;
        m_worldController.StateCommitted -= OnStateCommitted;
        m_worldController.MessageChanged -= OnMessageChanged;
        m_worldController.ValidationChanged += OnValidationChanged;
        m_worldController.StateCommitted += OnStateCommitted;
        m_worldController.MessageChanged += OnMessageChanged;
    }

    private void UnsubscribeControllerEvents()
    {
        if (m_worldController == null)
        {
            return;
        }

        m_worldController.ValidationChanged -= OnValidationChanged;
        m_worldController.StateCommitted -= OnStateCommitted;
        m_worldController.MessageChanged -= OnMessageChanged;
    }

    private void OnValidationChanged(MPThreeDValidationResult result)
    {
        if (m_released || result == null)
        {
            return;
        }

        switch (result.State)
        {
            case MPThreeDPlacementState.SnappedValid:
                SetStatus("SNAPPED", ValidColor);
                break;
            case MPThreeDPlacementState.FreeValid:
                SetStatus("VALID", ValidColor);
                break;
            case MPThreeDPlacementState.NoConnection:
                SetStatus("NO CONNECTION", WarningColor);
                break;
            case MPThreeDPlacementState.Collision:
                SetStatus("COLLISION", InvalidColor);
                break;
            case MPThreeDPlacementState.OutOfBounds:
                SetStatus("OUT OF BOUNDS", InvalidColor);
                break;
            case MPThreeDPlacementState.Invalid:
                SetStatus("INVALID", InvalidColor);
                break;
            case MPThreeDPlacementState.Loading:
                SetStatus("LOADING", WarningColor);
                break;
            default:
                SetStatus("READY", ReadyColor);
                break;
        }

        if (!string.IsNullOrEmpty(result.Message))
        {
            SetHint(ToEnglishMessage(result.Message));
        }

        RefreshControls();
    }

    private void OnStateCommitted()
    {
        if (m_released)
        {
            return;
        }

        RefreshControls();
        if (!m_isLoadingState)
        {
            ScheduleAutoSave();
        }
    }

    private void OnMessageChanged(string message)
    {
        if (!m_released && !string.IsNullOrEmpty(message))
        {
            SetHint(ToEnglishMessage(message));
        }
    }

    private void BuildPartCards()
    {
        ClearPartCards();
        if (m_partContent == null)
        {
            return;
        }

        IReadOnlyList<MPThreeDPartDefinition> definitions =
            MPThreeDPartCatalog.BuildableParts;
        for (int i = 0; i < definitions.Count; i++)
        {
            CreatePartCard(definitions[i]);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_partContent);
    }

    private void CreatePartCard(MPThreeDPartDefinition definition)
    {
        GameObject card = new GameObject(
            $"PartCard_{definition.Id}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        card.layer = m_partContent.gameObject.layer;
        card.transform.SetParent(m_partContent, false);

        RectTransform cardRect = (RectTransform)card.transform;
        cardRect.sizeDelta = new Vector2(0f, PartCardHeight);

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = CardColor;

        Button cardButton = card.GetComponent<Button>();
        cardButton.targetGraphic = cardImage;
        cardButton.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };

        ColorBlock colors = cardButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
        colors.fadeDuration = 0.08f;
        cardButton.colors = colors;

        LayoutElement layoutElement = card.GetComponent<LayoutElement>();
        layoutElement.minHeight = PartCardHeight;
        layoutElement.preferredHeight = PartCardHeight;

        Image swatch = CreateRuntimeImage(
            "Swatch", card.transform, definition.Color);
        SetRuntimeRect(
            swatch.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(14f, 0f),
            new Vector2(62f, 92f));
        swatch.raycastTarget = false;

        TMP_Text nameText = CreateRuntimeText(
            "NameText",
            card.transform,
            definition.DisplayName,
            27f,
            FontStyles.Bold);
        SetRuntimeRect(
            nameText.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(42f, 18f),
            new Vector2(-98f, -48f));

        Vector3 size = definition.Size;
        string sizeLabel = $"{size.x:0.#} x {size.y:0.#} x {size.z:0.#}";
        TMP_Text sizeText = CreateRuntimeText(
            "SizeText",
            card.transform,
            sizeLabel,
            19f,
            FontStyles.Normal);
        sizeText.color = new Color32(184, 198, 219, 255);
        SetRuntimeRect(
            sizeText.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(42f, -30f),
            new Vector2(-98f, -60f));

        string partId = definition.Id;
        cardButton.onClick.AddListener(() => OnPartSelected(partId));
        m_partButtons.Add(cardButton);
        m_partCards.Add(card);
    }

    private void ClearPartCards()
    {
        for (int i = 0; i < m_partButtons.Count; i++)
        {
            if (m_partButtons[i] != null)
            {
                m_partButtons[i].onClick.RemoveAllListeners();
            }
        }

        for (int i = 0; i < m_partCards.Count; i++)
        {
            if (m_partCards[i] != null)
            {
                Destroy(m_partCards[i]);
            }
        }

        m_partButtons.Clear();
        m_partCards.Clear();
    }

    private void OnPartSelected(string partId)
    {
        if (!CanUseController())
        {
            return;
        }

        m_worldController.BeginCreate(partId);
        RefreshControls();
    }

    private void OnBackClick()
    {
        FlushAutoSave();
        DestroyWindow();
    }

    private void OnGridClick()
    {
        if (!CanUseController())
        {
            return;
        }

        m_worldController.ToggleGrid();
        RefreshControls();
        ScheduleAutoSave();
    }

    private void OnConnectionClick()
    {
        if (!CanUseController())
        {
            return;
        }

        m_worldController.TogglePlacementMode();
        RefreshControls();
        ScheduleAutoSave();
    }

    private void OnSaveClick()
    {
        if (SaveCurrentState(true))
        {
            SetStatus("SAVED", ValidColor);
            SetHint("BUILD SAVED");
        }
    }

    private void OnCheckClick()
    {
        if (CanUseController())
        {
            m_worldController.CheckAssembly();
        }
    }

    private void OnClearClick()
    {
        if (!CanUseController())
        {
            return;
        }

        if (Time.unscaledTime > m_clearConfirmDeadline)
        {
            BeginClearConfirmation();
            return;
        }

        ResetClearConfirmation();
        m_worldController.ClearAll();
        RefreshControls();
        ScheduleAutoSave();
    }

    private void OnRotationSnapClick()
    {
        if (CanUseController())
        {
            m_worldController.ToggleTransformSnap();
            RefreshControls();
        }
    }

    private void OnDuplicateClick()
    {
        if (CanUseController())
        {
            m_worldController.DuplicateEditingPart();
            RefreshControls();
        }
    }

    private void OnDeleteClick()
    {
        if (!CanUseController())
        {
            return;
        }

        m_worldController.DeleteEditingPart();
        RefreshControls();
        ScheduleAutoSave();
    }

    private void RefreshControls()
    {
        bool ready = CanUseController();
        bool previewActive = ready && m_worldController.PreviewActive;

        SetInteractable(m_gridBtn, ready);
        SetInteractable(m_connectionBtn, ready);
        SetInteractable(
            m_saveBtn,
            ready && !previewActive && MPThreeDModuleServices.Storage != null);
        SetInteractable(m_checkBtn, ready);
        SetInteractable(m_clearBtn, ready);
        SetInteractable(m_rotationSnapBtn, ready);
        SetInteractable(
            m_duplicateBtn,
            ready && m_worldController.CanEditPlacedPart);
        SetInteractable(
            m_deleteBtn,
            ready && m_worldController.CanEditPlacedPart);
        for (int i = 0; i < m_partButtons.Count; i++)
        {
            SetInteractable(m_partButtons[i], ready);
        }

        SetButtonLabel(
            m_gridBtn,
            ready && m_worldController.GridVisible ? "GRID ON" : "GRID OFF");
        SetButtonLabel(
            m_connectionBtn,
            ready && m_worldController.RequireConnection ? "SNAP ON" : "SNAP OFF");
        SetButtonLabel(
            m_rotationSnapBtn,
            ready && m_worldController.TransformSnapEnabled
                ? "STEP ON"
                : "STEP OFF");
    }

    private void ScheduleAutoSave()
    {
        if (!CanUseController() || MPThreeDModuleServices.Storage == null)
        {
            return;
        }

        m_savePending = true;
        if (m_autoSaveCoroutine != null)
        {
            StopCoroutine(m_autoSaveCoroutine);
        }

        m_autoSaveCoroutine = StartCoroutine(AutoSaveAfterDelay());
    }

    private IEnumerator AutoSaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(AutoSaveDelay);
        m_autoSaveCoroutine = null;
        SavePendingState();
    }

    private void FlushAutoSave()
    {
        if (m_autoSaveCoroutine != null)
        {
            StopCoroutine(m_autoSaveCoroutine);
            m_autoSaveCoroutine = null;
        }

        SavePendingState();
    }

    private void SavePendingState()
    {
        if (!m_savePending)
        {
            return;
        }

        SaveCurrentState(false);
    }

    private bool SaveCurrentState(bool stopPendingSave)
    {
        if (!CanUseController() || MPThreeDModuleServices.Storage == null)
        {
            return false;
        }

        if (m_worldController.PointerGestureActive)
        {
            m_savePending = true;
            if (m_autoSaveCoroutine == null)
            {
                m_autoSaveCoroutine = StartCoroutine(AutoSaveAfterDelay());
            }

            return false;
        }

        if (stopPendingSave && m_autoSaveCoroutine != null)
        {
            StopCoroutine(m_autoSaveCoroutine);
            m_autoSaveCoroutine = null;
        }

        m_savePending = false;
        try
        {
            MPThreeDAssemblySaveDto state = m_worldController.CaptureState();
            if (state != null)
            {
                MPThreeDModuleServices.Storage.Save(state);
                return true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ThreeD] Auto save failed: {exception}");
            SetHint("AUTO SAVE FAILED");
        }

        return false;
    }

    private void BeginClearConfirmation()
    {
        ResetClearConfirmation();
        m_clearConfirmDeadline = Time.unscaledTime + ClearConfirmDuration;
        SetButtonLabel(m_clearBtn, "CLEAR?");
        SetHint("PRESS CLEAR AGAIN TO CONFIRM");
        m_clearConfirmCoroutine = StartCoroutine(ResetClearAfterDelay());
    }

    private IEnumerator ResetClearAfterDelay()
    {
        yield return new WaitForSecondsRealtime(ClearConfirmDuration);
        m_clearConfirmCoroutine = null;
        m_clearConfirmDeadline = -1f;
        SetButtonLabel(m_clearBtn, "CLEAR");
    }

    private void ResetClearConfirmation()
    {
        if (m_clearConfirmCoroutine != null)
        {
            StopCoroutine(m_clearConfirmCoroutine);
            m_clearConfirmCoroutine = null;
        }

        m_clearConfirmDeadline = -1f;
        SetButtonLabel(m_clearBtn, "CLEAR");
    }

    private void ReleaseRuntime()
    {
        if (m_released)
        {
            return;
        }

        if (m_viewportInput != null)
        {
            // 先回滚尚未松手的直接旋转，避免退出时把中间角度写入存档。
            m_viewportInput.Shutdown();
            m_viewportInput.enabled = false;
        }

        FlushAutoSave();
        ResetClearConfirmation();
        m_released = true;
        m_runtimeInitialized = false;

        UnregisterUI();
        UnsubscribeControllerEvents();

        if (m_viewportInput != null)
        {
            m_viewportInput = null;
        }

        if (m_sceneViewport != null)
        {
            m_sceneViewport.texture = null;
        }

        ShutdownController();
        ClearPartCards();
    }

    private void ShutdownController()
    {
        if (m_worldController == null)
        {
            return;
        }

        try
        {
            m_worldController.Shutdown();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ThreeD] Controller shutdown failed: {exception}");
        }

        m_worldController = null;
    }

    private bool CanUseController()
    {
        return !m_released
            && m_runtimeInitialized
            && m_worldController != null;
    }

    private void SetTitle(string text)
    {
        if (m_titleText != null)
        {
            m_titleText.text = text;
        }
    }

    private void SetStatus(string text, Color color)
    {
        if (m_statusText != null)
        {
            m_statusText.text = text;
            m_statusText.color = color;
        }
    }

    private void SetHint(string text)
    {
        if (m_hintText != null)
        {
            m_hintText.text = text;
        }
    }

    private static void SetButtonLabel(Button button, string text)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text;
        }
    }

    private static void SetInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static string ToEnglishMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        bool asciiOnly = true;
        for (int i = 0; i < message.Length; i++)
        {
            if (message[i] > 127)
            {
                asciiOnly = false;
                break;
            }
        }

        if (asciiOnly)
        {
            return message.ToUpperInvariant();
        }

        if (HasText(message, "检查通过"))
        {
            return "ASSEMBLY CHECK PASSED";
        }

        if (HasText(message, "穿模") || HasText(message, "碰撞"))
        {
            return "PARTS ARE OVERLAPPING";
        }

        if (HasText(message, "超出"))
        {
            return "MOVE INSIDE THE BUILD AREA";
        }

        if (HasText(message, "吸附"))
        {
            return "SNAP READY - PRESS PLACE";
        }

        if (HasText(message, "连接拼装模式"))
        {
            return "CONNECTION MODE ENABLED";
        }

        if (HasText(message, "连接点"))
        {
            return "MOVE CLOSER TO A CONNECTION";
        }

        if (HasText(message, "正在放置"))
        {
            return "MOVE THE PART, THEN PRESS PLACE";
        }

        if (HasText(message, "载入"))
        {
            return "BUILD LOADED";
        }

        if (HasText(message, "撤销"))
        {
            return "UNDO COMPLETE";
        }

        if (HasText(message, "重做"))
        {
            return "REDO COMPLETE";
        }

        if (HasText(message, "删除"))
        {
            return "PART DELETED";
        }

        if (HasText(message, "清空"))
        {
            return "BUILD CLEARED";
        }

        if (HasText(message, "取消"))
        {
            return "EDIT CANCELED";
        }

        if (HasText(message, "确认"))
        {
            return "PART PLACED";
        }

        if (HasText(message, "网格"))
        {
            return "GRID UPDATED";
        }

        if (HasText(message, "自由"))
        {
            return "FREE PLACEMENT ENABLED";
        }

        if (HasText(message, "保存失败"))
        {
            return "SAVE FAILED - TRY AGAIN";
        }

        if (HasText(message, "选择"))
        {
            return "SELECT A PART TO START";
        }

        return "BUILD UPDATED";
    }

    private static bool HasText(string source, string value)
    {
        return source.IndexOf(value, StringComparison.Ordinal) >= 0;
    }

    private static void RegisterButton(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnregisterButton(Button button, UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }

    private static Image CreateRuntimeImage(
        string name,
        Transform parent,
        Color color)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateRuntimeText(
        string name,
        Transform parent,
        string text,
        float fontSize,
        FontStyles fontStyle)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);

        TextMeshProUGUI textComponent =
            gameObject.GetComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        textComponent.color = CardTextColor;
        textComponent.enableWordWrapping = false;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            textComponent.font = TMP_Settings.defaultFontAsset;
        }

        return textComponent;
    }

    private static void SetRuntimeRect(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
    }
}
