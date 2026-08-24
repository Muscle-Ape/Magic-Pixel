using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成 3D 模块所需的基础 UGUI Prefab。
/// Prefab 只保存视图层级和基础组件，MPThreeDView 由 UIManager 在运行时动态挂载。
/// </summary>
public static class MPThreeDModulePrefabBuilder
{
    public const string PrefabPath =
        "Assets/YooRes/Prefabs/ThreeD/MPThreeDView.prefab";

    private const string MenuPath =
        "MagicPixel/ThreeD/Build And Validate MPThreeDView";

    private static readonly Color PanelColor =
        new Color32(32, 42, 61, 232);
    private static readonly Color PanelSecondaryColor =
        new Color32(46, 58, 80, 238);
    private static readonly Color ButtonColor =
        new Color32(77, 98, 133, 255);
    private static readonly Color ConfirmColor =
        new Color32(63, 174, 118, 255);
    private static readonly Color DangerColor =
        new Color32(201, 82, 82, 255);
    private static readonly Color TextColor =
        new Color32(242, 247, 255, 255);
    private static readonly Color MutedTextColor =
        new Color32(181, 196, 218, 255);

    private sealed class RequiredComponent
    {
        public readonly string Path;
        public readonly Type Type;

        public RequiredComponent(string path, Type type)
        {
            Path = path;
            Type = type;
        }
    }

    private static readonly RequiredComponent[] RequiredComponents =
    {
        new RequiredComponent("SceneViewport", typeof(RawImage)),
        new RequiredComponent("View/Header/BackBtn", typeof(Button)),
        new RequiredComponent("View/Header/TitleText", typeof(TMP_Text)),
        new RequiredComponent("View/Header/GridBtn", typeof(Button)),
        new RequiredComponent("View/Header/ConnectionBtn", typeof(Button)),
        new RequiredComponent("View/Header/SaveBtn", typeof(Button)),
        new RequiredComponent("View/StatusPanel/StatusText", typeof(TMP_Text)),
        new RequiredComponent("View/StatusPanel/HintText", typeof(TMP_Text)),
        new RequiredComponent("View/PartPanel/ScrollView", typeof(ScrollRect)),
        new RequiredComponent(
            "View/PartPanel/ScrollView/Viewport/Content",
            typeof(RectTransform)),
        new RequiredComponent("View/ToolPanel/CheckBtn", typeof(Button)),
        new RequiredComponent("View/ToolPanel/ClearBtn", typeof(Button)),
        new RequiredComponent("View/BottomBar/RotationSnapBtn", typeof(Button)),
        new RequiredComponent(
            "View/BottomBar/RotationSnapBtn/Label",
            typeof(TMP_Text)),
        new RequiredComponent("View/BottomBar/DuplicateBtn", typeof(Button)),
        new RequiredComponent("View/BottomBar/DeleteBtn", typeof(Button)),
    };

    [MenuItem(MenuPath)]
    private static void BuildFromMenu()
    {
        BuildAndValidate();
    }

    /// <summary>
    /// 可由菜单或 Unity 命令行 -executeMethod 调用。
    /// </summary>
    public static void BuildAndValidate()
    {
        string directory = Path.GetDirectoryName(PrefabPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException(
                $"Invalid prefab path: {PrefabPath}");
        }

        Directory.CreateDirectory(directory);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        GameObject root = null;
        try
        {
            root = BuildHierarchy();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                PrefabPath,
                out bool succeeded);

            if (!succeeded || prefab == null)
            {
                throw new InvalidOperationException(
                    $"Failed to save prefab: {PrefabPath}");
            }
        }
        finally
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        AssetDatabase.ImportAsset(
            PrefabPath,
            ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.SaveAssets();
        ValidatePrefab();
        Debug.Log($"[ThreeD] Built and validated: {PrefabPath}");
    }

    private static GameObject BuildHierarchy()
    {
        GameObject root = CreateUIObject("MPThreeDView", null);
        SetFullStretch((RectTransform)root.transform);

        RawImage sceneViewport = CreateRawImage(
            "SceneViewport",
            root.transform);
        SetFullStretch(sceneViewport.rectTransform);
        sceneViewport.raycastTarget = true;

        GameObject view = CreateUIObject("View", root.transform);
        SetFullStretch((RectTransform)view.transform);

        BuildHeader(view.transform);
        BuildStatusPanel(view.transform);
        BuildPartPanel(view.transform);
        BuildToolPanel(view.transform);
        BuildBottomBar(view.transform);
        return root;
    }

    private static void BuildHeader(Transform parent)
    {
        Image header = CreateImage("Header", parent, PanelColor);
        SetRect(
            header.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 150f));

        CreateButton(
            "BackBtn", header.transform, "BACK",
            new Vector2(70f, -75f), new Vector2(116f, 86f), ButtonColor,
            new Vector2(0f, 1f));

        TMP_Text title = CreateText(
            "TitleText", header.transform, "3D BUILDER", 40f,
            TextAlignmentOptions.MidlineLeft, TextColor, FontStyles.Bold);
        SetRect(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(148f, -75f),
            new Vector2(300f, 90f));

        CreateButton(
            "GridBtn", header.transform, "GRID ON",
            new Vector2(-275f, -75f), new Vector2(100f, 78f), ButtonColor,
            new Vector2(1f, 1f));
        CreateButton(
            "ConnectionBtn", header.transform, "LINK ON",
            new Vector2(-165f, -75f), new Vector2(100f, 78f), ButtonColor,
            new Vector2(1f, 1f));
        CreateButton(
            "SaveBtn", header.transform, "SAVE",
            new Vector2(-55f, -75f), new Vector2(100f, 78f), ConfirmColor,
            new Vector2(1f, 1f));
    }

    private static void BuildStatusPanel(Transform parent)
    {
        Image panel = CreateImage("StatusPanel", parent, PanelSecondaryColor);
        SetRect(
            panel.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -174f),
            new Vector2(520f, 100f));

        TMP_Text status = CreateText(
            "StatusText", panel.transform, "READY", 30f,
            TextAlignmentOptions.Center, TextColor, FontStyles.Bold);
        SetRect(
            status.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -4f),
            new Vector2(-24f, 0f));

        TMP_Text hint = CreateText(
            "HintText", panel.transform, "SELECT A PART TO START", 22f,
            TextAlignmentOptions.Center, MutedTextColor, FontStyles.Normal);
        SetRect(
            hint.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 4f),
            new Vector2(-24f, 0f));
    }

    private static void BuildPartPanel(Transform parent)
    {
        Image panel = CreateImage("PartPanel", parent, PanelColor);
        SetRect(
            panel.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(24f, 0f),
            new Vector2(252f, -374f));

        TMP_Text title = CreateText(
            "PartsTitleText", panel.transform, "PARTS", 31f,
            TextAlignmentOptions.Center, TextColor, FontStyles.Bold);
        SetRect(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -12f),
            new Vector2(-20f, 58f));

        GameObject scrollObject = CreateUIObject("ScrollView", panel.transform);
        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = new Color32(20, 27, 41, 160);
        scrollBackground.raycastTarget = true;
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        SetStretchWithOffsets(
            (RectTransform)scrollObject.transform,
            12f, 12f, 72f, 12f);

        Image viewport = CreateImage(
            "Viewport",
            scrollObject.transform,
            new Color32(255, 255, 255, 8));
        SetFullStretch(viewport.rectTransform);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = CreateUIObject("Content", viewport.transform);
        RectTransform contentRect = (RectTransform)content.transform;
        SetRect(
            contentRect,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            Vector2.zero);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.rectTransform;
        scrollRect.content = contentRect;
    }

    private static void BuildToolPanel(Transform parent)
    {
        Image panel = CreateImage("ToolPanel", parent, PanelColor);
        SetRect(
            panel.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-24f, 0f),
            new Vector2(174f, 232f));

        CreateButton(
            "CheckBtn", panel.transform, "CHECK",
            new Vector2(0f, 52f), new Vector2(146f, 78f), ButtonColor);
        CreateButton(
            "ClearBtn", panel.transform, "CLEAR",
            new Vector2(0f, -52f), new Vector2(146f, 78f), DangerColor);
    }

    private static void BuildBottomBar(Transform parent)
    {
        Image panel = CreateImage("BottomBar", parent, PanelColor);
        SetRect(
            panel.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(0f, 184f));

        string[] names =
        {
            "RotationSnapBtn", "DuplicateBtn", "DeleteBtn"
        };
        string[] labels =
        {
            "STEP ON", "COPY", "DELETE"
        };

        const float spacing = 131f;
        float startX = -spacing * (names.Length - 1) * 0.5f;
        for (int i = 0; i < names.Length; i++)
        {
            Color color = ButtonColor;
            if (names[i] == "DeleteBtn")
            {
                color = DangerColor;
            }

            CreateButton(
                names[i],
                panel.transform,
                labels[i],
                new Vector2(startX + spacing * i, 0f),
                new Vector2(118f, 104f),
                color);
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = parent != null
            ? parent.gameObject.layer
            : Mathf.Max(0, LayerMask.NameToLayer("UI"));

        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Color color)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    private static RawImage CreateRawImage(
        string name,
        Transform parent)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        RawImage rawImage = gameObject.AddComponent<RawImage>();
        // RawImage.color 会与 RenderTexture 逐像素相乘；白色代表不做任何染色。
        rawImage.color = Color.white;
        rawImage.raycastTarget = true;
        return rawImage;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        Vector2? anchor = null)
    {
        Image image = CreateImage(name, parent, color);
        Vector2 resolvedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        SetRect(
            image.rectTransform,
            resolvedAnchor,
            resolvedAnchor,
            new Vector2(0.5f, 0.5f),
            anchoredPosition,
            size);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text text = CreateText(
            "Label", button.transform, label, 23f,
            TextAlignmentOptions.Center, TextColor, FontStyles.Bold);
        SetFullStretch(text.rectTransform);
        text.margin = new Vector4(4f, 2f, 4f, 2f);
        return button;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color,
        FontStyles fontStyle)
    {
        GameObject gameObject = CreateUIObject(name, parent);
        TextMeshProUGUI textComponent =
            gameObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.fontStyle = fontStyle;
        textComponent.enableWordWrapping = false;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
        {
            textComponent.font = TMP_Settings.defaultFontAsset;
        }

        return textComponent;
    }

    private static void SetFullStretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetStretchWithOffsets(
        RectTransform rectTransform,
        float left,
        float right,
        float top,
        float bottom)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    private static void SetRect(
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

    private static void ValidatePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            throw new InvalidOperationException(
                $"Unable to load prefab contents: {PrefabPath}");
        }

        try
        {
            List<string> errors = new List<string>();
            ValidateRoot(root, errors);
            ValidateRequiredComponents(root.transform, errors);
            ValidateSceneViewportColor(root.transform, errors);
            ValidateNoRemovedButtons(root.transform, errors);
            ValidateNoMissingScripts(root.transform, errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "MPThreeDView prefab validation failed:\n- "
                    + string.Join("\n- ", errors));
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateRoot(
        GameObject root,
        ICollection<string> errors)
    {
        if (root.name != "MPThreeDView")
        {
            errors.Add($"Root name must be MPThreeDView, actual: {root.name}");
        }

        RectTransform rootRect = root.transform as RectTransform;
        if (rootRect == null)
        {
            errors.Add("Root must contain RectTransform");
            return;
        }

        if (!Approximately(rootRect.anchorMin, Vector2.zero)
            || !Approximately(rootRect.anchorMax, Vector2.one)
            || !Approximately(rootRect.offsetMin, Vector2.zero)
            || !Approximately(rootRect.offsetMax, Vector2.zero))
        {
            errors.Add("Root RectTransform must be full stretch with zero offsets");
        }

        Transform view = root.transform.Find("View");
        if (view == null || view.parent != root.transform)
        {
            errors.Add("A direct child named View is required");
        }

        MonoBehaviour[] rootBehaviours = root.GetComponents<MonoBehaviour>();
        for (int i = 0; i < rootBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = rootBehaviours[i];
            if (behaviour != null
                && behaviour.GetType().Name == "MPThreeDView")
            {
                errors.Add(
                    "MPThreeDView must not be attached to the prefab; "
                    + "UIManager adds it at runtime");
            }
        }
    }

    private static void ValidateRequiredComponents(
        Transform root,
        ICollection<string> errors)
    {
        for (int i = 0; i < RequiredComponents.Length; i++)
        {
            RequiredComponent required = RequiredComponents[i];
            Transform node = root.Find(required.Path);
            if (node == null)
            {
                errors.Add($"Missing required path: {required.Path}");
                continue;
            }

            if (node.GetComponent(required.Type) == null)
            {
                errors.Add(
                    $"Path {required.Path} is missing "
                    + required.Type.Name);
            }
        }
    }

    private static void ValidateNoRemovedButtons(
        Transform root,
        ICollection<string> errors)
    {
        string[] removedPaths =
        {
            "View/BottomBar/RotateLeftBtn",
            "View/BottomBar/RotateRightBtn",
            "View/ToolPanel/ZoomInBtn",
            "View/ToolPanel/ZoomOutBtn",
            "View/BottomBar/HeightDownBtn",
            "View/BottomBar/HeightUpBtn",
            "View/BottomBar/CancelBtn",
            "View/BottomBar/ConfirmBtn",
            "View/Header/UndoBtn",
            "View/Header/RedoBtn"
        };
        for (int i = 0; i < removedPaths.Length; i++)
        {
            if (root.Find(removedPaths[i]) != null)
            {
                errors.Add($"Removed button must not exist: {removedPaths[i]}");
            }
        }
    }

    private static void ValidateSceneViewportColor(
        Transform root,
        ICollection<string> errors)
    {
        RawImage viewport = root.Find("SceneViewport")?.GetComponent<RawImage>();
        if (viewport != null && viewport.color != Color.white)
        {
            errors.Add(
                "SceneViewport RawImage color must remain white to avoid tinting the RenderTexture");
        }
    }

    private static void ValidateNoMissingScripts(
        Transform node,
        ICollection<string> errors)
    {
        Component[] components = node.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                errors.Add($"Missing Script on: {GetHierarchyPath(node)}");
            }
        }

        for (int i = 0; i < node.childCount; i++)
        {
            ValidateNoMissingScripts(node.GetChild(i), errors);
        }
    }

    private static string GetHierarchyPath(Transform node)
    {
        string path = node.name;
        Transform current = node.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return Mathf.Approximately(left.x, right.x)
            && Mathf.Approximately(left.y, right.y);
    }
}
