using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 主关卡与大图关卡共用的可视化关卡编辑器。
/// </summary>
public sealed class MPLevelEditorWindow : EditorWindow
{
    private enum EditLayer
    {
        Background,
        Blocks,
    }

    private enum EditOperation
    {
        Paint,
        Erase,
    }

    private const int MinGridSize = MPLevelEditorStorage.MinMainGridSize;
    private const int MaxGridSize = MPLevelEditorStorage.MaxGridSize;
    private const int MinLargeImageGridSize = MPLevelEditorStorage.MinLargeImageGridSize;
    private const float MinCellSize = 10f;
    private const float MaxCellSize = 48f;

    private static readonly Color MissingColorA = new Color(0.24f, 0.24f, 0.24f, 1f);
    private static readonly Color MissingColorB = new Color(0.32f, 0.32f, 0.32f, 1f);
    private static readonly Color BlockOverlayColor = new Color(0f, 0f, 0f, 0.28f);
    private static readonly Color BlockBorderColor = new Color(1f, 0.72f, 0.12f, 1f);
    private static readonly Color GridLineColor = new Color(0f, 0f, 0f, 0.45f);

    private MPLevelEditorMode m_mode = MPLevelEditorMode.Main;
    private bool m_isExisting;
    private bool m_isDirty;
    private bool m_newIdExists;
    private string m_id = string.Empty;
    private string m_levelName = string.Empty;
    private int m_awardCoin = 300;
    private int m_gridSize = 5;
    private int m_requestedGridSize = 5;
    private Texture2D m_sourceTexture;
    private string m_sourceAssetPath = string.Empty;
    private Color[] m_colors = Array.Empty<Color>();
    private bool[] m_colorAssigned = Array.Empty<bool>();
    private bool[] m_blocks = Array.Empty<bool>();

    private EditLayer m_editLayer = EditLayer.Background;
    private EditOperation m_editOperation = EditOperation.Paint;
    private Color m_brushColor = Color.white;
    private float m_cellSize = 32f;
    private Vector2 m_windowScroll;
    private Vector2 m_gridScroll;
    private int m_lastPaintedIndex = -1;
    private int m_hoveredIndex = -1;

    [MenuItem("MagicPixel/Level Editor", false, 20)]
    private static void Open()
    {
        MPLevelEditorWindow window = GetWindow<MPLevelEditorWindow>("Level Editor");
        window.minSize = new Vector2(760f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        wantsMouseMove = true;
        if (m_colors == null || m_colors.Length == 0)
        {
            CreateNewLevel(MPLevelEditorMode.Main, false);
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        m_windowScroll = EditorGUILayout.BeginScrollView(m_windowScroll);
        DrawSourceSection();
        EditorGUILayout.Space(8f);
        DrawBasicSection();
        EditorGUILayout.Space(8f);
        DrawToolSection();
        EditorGUILayout.Space(8f);
        DrawGridSection();
        EditorGUILayout.Space(8f);
        DrawSaveSection();
        if (m_isExisting)
        {
            EditorGUILayout.Space(8f);
            DrawDeleteSection();
        }
        EditorGUILayout.Space(12f);
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("MagicPixel 关卡编辑器", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label(m_isExisting ? "编辑已有" : "新建关卡", EditorStyles.miniLabel);
        if (m_isDirty)
        {
            GUILayout.Label("未保存", EditorStyles.miniBoldLabel);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSourceSection()
    {
        DrawSectionTitle("1. 关卡来源");
        EditorGUILayout.HelpBox(
            "新建关卡可直接设置模式和尺寸；编辑已有内容时，请从 Project 窗口拖入 BlockPixel 目录中的原始 PNG。编辑器会根据图片文件名自动读取对应 JSON 数据。",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        Texture2D newTexture = EditorGUILayout.ObjectField("已有关卡图片", m_sourceTexture, typeof(Texture2D), false) as Texture2D;
        if (newTexture != m_sourceTexture)
        {
            TryChangeSourceTexture(newTexture);
        }

        if (GUILayout.Button("新建空白关卡", GUILayout.Width(130f)))
        {
            TryCreateNewLevel(m_mode);
        }
        EditorGUILayout.EndHorizontal();

        if (m_isExisting)
        {
            EditorGUILayout.LabelField("来源", m_sourceAssetPath);
        }
    }

    private void DrawBasicSection()
    {
        DrawSectionTitle("2. 基础信息");

        using (new EditorGUI.DisabledScope(m_isExisting))
        {
            string[] modeNames = { "主关卡", "大图模式" };
            MPLevelEditorMode newMode = (MPLevelEditorMode)EditorGUILayout.Popup("关卡模式", (int)m_mode, modeNames);
            if (newMode != m_mode)
            {
                TryCreateNewLevel(newMode);
            }
        }

        using (new EditorGUI.DisabledScope(m_isExisting))
        {
            string newId = EditorGUILayout.TextField("关卡 ID", m_id);
            if (newId != m_id)
            {
                m_id = newId.Trim();
                RefreshNewIdExistence();
                MarkDirty();
            }
        }

        if (m_isExisting)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("网格尺寸", m_gridSize);
            }
            EditorGUILayout.HelpBox("编辑已有关卡时，模式、ID 和网格尺寸已锁定。", MessageType.None);
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            m_requestedGridSize = EditorGUILayout.IntField("网格尺寸", m_requestedGridSize);
            if (GUILayout.Button("应用尺寸", GUILayout.Width(100f)))
            {
                TryApplyGridSize();
            }
            EditorGUILayout.EndHorizontal();
        }

        if (m_mode == MPLevelEditorMode.LargeImage)
        {
            string newName = EditorGUILayout.TextField("关卡名称", m_levelName);
            if (newName != m_levelName)
            {
                m_levelName = newName;
                MarkDirty();
            }

            int newAwardCoin = EditorGUILayout.IntField("通关金币", m_awardCoin);
            if (newAwardCoin != m_awardCoin)
            {
                m_awardCoin = Mathf.Max(0, newAwardCoin);
                MarkDirty();
            }
        }

        EditorGUILayout.LabelField("配置文件", MPLevelEditorStorage.GetConfigAssetPath(m_mode));
        EditorGUILayout.LabelField("原图输出", $"{MPLevelEditorStorage.BlockPixelAssetDirectory}/{m_id}.png");
        if (m_mode == MPLevelEditorMode.LargeImage)
        {
            EditorGUILayout.LabelField("缩略图输出", $"{MPLevelEditorStorage.ThumbAssetDirectory}/icon_{m_id}.png");
        }
        else
        {
            EditorGUILayout.LabelField("缩略图输出", "主关卡不生成或更新缩略图");
        }

        if (m_mode == MPLevelEditorMode.Main && m_gridSize != 5 && m_gridSize != 10 && m_gridSize != 15)
        {
            EditorGUILayout.HelpBox(
                "当前主玩法的数字字号和粗分隔线主要按 5×5、10×10、15×15 适配；其他尺寸允许保存，但需要在实际 UI 中额外检查显示效果。",
                MessageType.Warning);
        }

        if (m_mode == MPLevelEditorMode.LargeImage && m_gridSize < MinLargeImageGridSize)
        {
            EditorGUILayout.HelpBox("大图玩法固定显示 10×10 窗口，因此大图尺寸不能小于 10。", MessageType.Error);
        }
    }

    private void DrawToolSection()
    {
        DrawSectionTitle("3. 编辑工具");
        string[] layerNames = { "底色", "Blocks" };
        m_editLayer = (EditLayer)GUILayout.Toolbar((int)m_editLayer, layerNames);

        EditorGUILayout.Space(4f);
        if (m_editLayer == EditLayer.Background)
        {
            string[] operationNames = { "绘制底色", "单格删除" };
            m_editOperation = (EditOperation)GUILayout.Toolbar((int)m_editOperation, operationNames);

            if (m_editOperation == EditOperation.Paint)
            {
                Color newBrushColor = EditorGUILayout.ColorField("画笔颜色", m_brushColor);
                newBrushColor.a = 1f;
                m_brushColor = newBrushColor;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全部填充当前颜色"))
            {
                FillAllColors();
            }
            if (GUILayout.Button("清空全部底色"))
            {
                ClearAllColors();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "绘制底色：左键单击或拖动绘制，Alt + 左键吸取颜色。单格删除：左键单击或拖动只删除经过的格子。右键始终可以删除底色。",
                MessageType.None);
        }
        else
        {
            string[] operationNames = { "添加 Block", "单格删除" };
            m_editOperation = (EditOperation)GUILayout.Toolbar((int)m_editOperation, operationNames);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全部设为 Block"))
            {
                SetAllBlocks(true);
            }
            if (GUILayout.Button("清空 Blocks"))
            {
                SetAllBlocks(false);
            }
            if (GUILayout.Button("反选 Blocks"))
            {
                InvertBlocks();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "添加 Block：左键单击或拖动添加。单格删除：左键单击或拖动只删除经过的 Block。右键始终可以删除；橙色边框表示该格属于 block 数据。",
                MessageType.None);
        }

        int assignedCount = m_colorAssigned == null ? 0 : m_colorAssigned.Count(value => value);
        int blockCount = m_blocks == null ? 0 : m_blocks.Count(value => value);
        int cellCount = m_gridSize * m_gridSize;
        string hoverText = m_hoveredIndex < 0
            ? string.Empty
            : $"    当前格：index {m_hoveredIndex}（行 {m_hoveredIndex / m_gridSize}，列 {m_hoveredIndex % m_gridSize}）";
        EditorGUILayout.LabelField($"底色：{assignedCount}/{cellCount}    Blocks：{blockCount}/{cellCount}{hoverText}");
    }

    private void DrawGridSection()
    {
        DrawSectionTitle("4. 网格编辑");
        m_cellSize = EditorGUILayout.Slider("显示缩放", m_cellSize, MinCellSize, MaxCellSize);

        float canvasSize = Mathf.Max(1, m_gridSize) * m_cellSize;
        float visibleHeight = Mathf.Clamp(position.height * 0.58f, 300f, 720f);
        m_gridScroll = EditorGUILayout.BeginScrollView(
            m_gridScroll,
            true,
            true,
            GUILayout.Height(visibleHeight));

        Rect gridRect = GUILayoutUtility.GetRect(
            canvasSize,
            canvasSize,
            GUILayout.Width(canvasSize),
            GUILayout.Height(canvasSize));

        DrawGridCells(gridRect);
        HandleGridInput(gridRect);
        EditorGUILayout.EndScrollView();
    }

    private void DrawSaveSection()
    {
        DrawSectionTitle("5. 保存");

        bool isValid = TryValidate(out string validationMessage);
        if (!isValid)
        {
            EditorGUILayout.HelpBox(validationMessage, MessageType.Error);
        }
        else
        {
            string resourceDescription = m_mode == MPLevelEditorMode.Main
                ? "BlockPixel"
                : "BlockPixel、Thumb";
            EditorGUILayout.HelpBox(
                m_isExisting
                    ? $"保存会覆盖当前 ID 的 {resourceDescription}，并原位修改 JSON 中对应记录。"
                    : $"保存会创建 {resourceDescription}，并把新记录追加到对应 JSON 末尾。",
                MessageType.Info);

            if (!m_blocks.Any(value => value))
            {
                EditorGUILayout.HelpBox("当前 Blocks 为空；这会创建一个答案全部为空格的关卡。", MessageType.Warning);
            }
        }

        using (new EditorGUI.DisabledScope(!isValid))
        {
            if (GUILayout.Button(m_isExisting ? "保存已有改动" : "创建并保存关卡", GUILayout.Height(36f)))
            {
                SaveCurrentLevel();
            }
        }
    }

    private void DrawDeleteSection()
    {
        DrawSectionTitle("6. 删除关卡");
        string deleteDescription = m_mode == MPLevelEditorMode.Main
            ? "删除会移除当前主关卡在 JSON 中的配置和 BlockPixel 图片，不会删除同 ID 的 Thumb。该操作不支持在编辑器中撤销。"
            : "删除会移除当前大图关卡在 JSON 中的配置、BlockPixel 图片和同 ID Thumb。该操作不支持在编辑器中撤销。";
        EditorGUILayout.HelpBox(
            deleteDescription,
            MessageType.Warning);

        Color previousBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
        if (GUILayout.Button($"删除关卡 {m_id}", GUILayout.Height(32f)))
        {
            DeleteCurrentLevel();
        }
        GUI.backgroundColor = previousBackgroundColor;
    }

    private void DrawGridCells(Rect gridRect)
    {
        if (!HasValidGridArrays())
        {
            return;
        }

        for (int row = 0; row < m_gridSize; row++)
        {
            for (int column = 0; column < m_gridSize; column++)
            {
                int index = row * m_gridSize + column;
                Rect cellRect = new Rect(
                    gridRect.x + column * m_cellSize,
                    gridRect.y + row * m_cellSize,
                    m_cellSize,
                    m_cellSize);

                Color backgroundColor = m_colorAssigned[index]
                    ? m_colors[index]
                    : ((row + column) % 2 == 0 ? MissingColorA : MissingColorB);
                EditorGUI.DrawRect(cellRect, backgroundColor);

                if (m_blocks[index])
                {
                    EditorGUI.DrawRect(cellRect, BlockOverlayColor);
                    DrawCellBorder(cellRect, BlockBorderColor, Mathf.Clamp(m_cellSize * 0.08f, 1f, 3f));
                }
            }
        }

        DrawGridLines(gridRect);
    }

    private void DrawGridLines(Rect gridRect)
    {
        Color previousColor = Handles.color;
        Handles.BeginGUI();
        Handles.color = GridLineColor;
        for (int i = 0; i <= m_gridSize; i++)
        {
            float offset = i * m_cellSize;
            Handles.DrawLine(
                new Vector3(gridRect.x + offset, gridRect.y),
                new Vector3(gridRect.x + offset, gridRect.yMax));
            Handles.DrawLine(
                new Vector3(gridRect.x, gridRect.y + offset),
                new Vector3(gridRect.xMax, gridRect.y + offset));
        }
        Handles.EndGUI();
        Handles.color = previousColor;
    }

    private static void DrawCellBorder(Rect rect, Color color, float width)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
    }

    private void HandleGridInput(Rect gridRect)
    {
        Event current = Event.current;
        if (!HasValidGridArrays())
        {
            return;
        }

        if (gridRect.Contains(current.mousePosition))
        {
            int hoverColumn = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.x - gridRect.x) / m_cellSize), 0, m_gridSize - 1);
            int hoverRow = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.y - gridRect.y) / m_cellSize), 0, m_gridSize - 1);
            int hoverIndex = hoverRow * m_gridSize + hoverColumn;
            if (hoverIndex != m_hoveredIndex)
            {
                m_hoveredIndex = hoverIndex;
                Repaint();
            }
        }
        else if (m_hoveredIndex != -1)
        {
            m_hoveredIndex = -1;
            Repaint();
        }

        if (current.type == EventType.MouseUp)
        {
            m_lastPaintedIndex = -1;
            return;
        }

        bool isPaintEvent = current.type == EventType.MouseDown || current.type == EventType.MouseDrag;
        if (!isPaintEvent || (current.button != 0 && current.button != 1) || !gridRect.Contains(current.mousePosition))
        {
            return;
        }

        int column = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.x - gridRect.x) / m_cellSize), 0, m_gridSize - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.y - gridRect.y) / m_cellSize), 0, m_gridSize - 1);
        int index = row * m_gridSize + column;
        if (index == m_lastPaintedIndex && current.type == EventType.MouseDrag)
        {
            current.Use();
            return;
        }

        if (m_editLayer == EditLayer.Background)
        {
            if (current.alt && current.button == 0)
            {
                SampleBackground(index);
            }
            else
            {
                bool erase = current.button == 1 || m_editOperation == EditOperation.Erase;
                PaintBackground(index, erase);
            }
        }
        else
        {
            bool erase = current.button == 1 || m_editOperation == EditOperation.Erase;
            PaintBlock(index, erase);
        }

        m_lastPaintedIndex = index;
        current.Use();
        Repaint();
    }

    private void PaintBackground(int index, bool clear)
    {
        if (clear)
        {
            m_colorAssigned[index] = false;
            m_colors[index] = Color.clear;
        }
        else
        {
            Color color = m_brushColor;
            color.a = 1f;
            m_colors[index] = color;
            m_colorAssigned[index] = true;
        }

        MarkDirty();
    }

    private void SampleBackground(int index)
    {
        if (m_colorAssigned[index])
        {
            m_brushColor = m_colors[index];
            m_brushColor.a = 1f;
        }
    }

    private void PaintBlock(int index, bool erase)
    {
        m_blocks[index] = !erase;
        MarkDirty();
    }

    private void TryChangeSourceTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (!CanDiscardUnsavedChanges("载入已有关卡"))
        {
            return;
        }

        if (!MPLevelEditorStorage.TryLoadExisting(texture, out MPLevelEditorData data, out string error))
        {
            EditorUtility.DisplayDialog("载入失败", error, "确定");
            return;
        }

        ApplyData(data, texture);
    }

    private void TryCreateNewLevel(MPLevelEditorMode mode)
    {
        if (!CanDiscardUnsavedChanges("新建关卡"))
        {
            return;
        }

        CreateNewLevel(mode, true);
    }

    private void CreateNewLevel(MPLevelEditorMode mode, bool focusWindow)
    {
        m_mode = mode;
        m_isExisting = false;
        m_isDirty = false;
        m_sourceTexture = null;
        m_sourceAssetPath = string.Empty;
        m_id = MPLevelEditorStorage.GetNextId(mode);
        m_newIdExists = false;
        string idSuffix = m_id.Substring(MPLevelEditorStorage.GetIdPrefix(mode).Length);
        m_levelName = mode == MPLevelEditorMode.LargeImage ? $"Large Image {idSuffix}" : string.Empty;
        m_awardCoin = mode == MPLevelEditorMode.LargeImage ? 300 : 0;
        m_gridSize = mode == MPLevelEditorMode.Main ? 5 : 20;
        m_requestedGridSize = m_gridSize;
        CreateEmptyGrid(m_gridSize);
        m_gridScroll = Vector2.zero;
        m_hoveredIndex = -1;
        if (focusWindow)
        {
            Focus();
        }
    }

    private void TryApplyGridSize()
    {
        int minSize = m_mode == MPLevelEditorMode.LargeImage ? MinLargeImageGridSize : MinGridSize;
        int newSize = Mathf.Clamp(m_requestedGridSize, minSize, MaxGridSize);
        m_requestedGridSize = newSize;
        if (newSize == m_gridSize)
        {
            return;
        }

        bool hasContent = m_colorAssigned.Any(value => value) || m_blocks.Any(value => value);
        if (hasContent && !EditorUtility.DisplayDialog(
                "调整网格尺寸",
                "调整尺寸会清空当前已经编辑的底色和 Blocks，是否继续？",
                "继续",
                "取消"))
        {
            m_requestedGridSize = m_gridSize;
            return;
        }

        m_gridSize = newSize;
        CreateEmptyGrid(m_gridSize);
        m_gridScroll = Vector2.zero;
        MarkDirty();
    }

    private void CreateEmptyGrid(int size)
    {
        int cellCount = size * size;
        m_colors = new Color[cellCount];
        m_colorAssigned = new bool[cellCount];
        m_blocks = new bool[cellCount];
    }

    private void ApplyData(MPLevelEditorData data, Texture2D texture)
    {
        m_mode = data.Mode;
        m_isExisting = data.IsExisting;
        m_isDirty = false;
        m_id = data.ID;
        m_newIdExists = false;
        m_levelName = data.Name;
        m_awardCoin = data.AwardCoin;
        m_gridSize = data.Size;
        m_requestedGridSize = data.Size;
        m_colors = (Color[])data.Colors.Clone();
        m_colorAssigned = (bool[])data.ColorAssigned.Clone();
        m_blocks = (bool[])data.Blocks.Clone();
        m_sourceTexture = texture;
        m_sourceAssetPath = data.SourceAssetPath;
        m_gridScroll = Vector2.zero;
        m_hoveredIndex = -1;
        Repaint();
    }

    private void FillAllColors()
    {
        Color color = m_brushColor;
        color.a = 1f;
        for (int i = 0; i < m_colors.Length; i++)
        {
            m_colors[i] = color;
            m_colorAssigned[i] = true;
        }

        MarkDirty();
    }

    private void ClearAllColors()
    {
        if (!EditorUtility.DisplayDialog("清空底色", "确定清空所有格子的底色吗？", "清空", "取消"))
        {
            return;
        }

        Array.Clear(m_colors, 0, m_colors.Length);
        Array.Clear(m_colorAssigned, 0, m_colorAssigned.Length);
        MarkDirty();
    }

    private void SetAllBlocks(bool value)
    {
        for (int i = 0; i < m_blocks.Length; i++)
        {
            m_blocks[i] = value;
        }

        MarkDirty();
    }

    private void InvertBlocks()
    {
        for (int i = 0; i < m_blocks.Length; i++)
        {
            m_blocks[i] = !m_blocks[i];
        }

        MarkDirty();
    }

    private bool TryValidate(out string message)
    {
        if (string.IsNullOrWhiteSpace(m_id))
        {
            message = "关卡 ID 不能为空。";
            return false;
        }

        string expectedPrefix = MPLevelEditorStorage.GetIdPrefix(m_mode);
        string idPattern = "^" + Regex.Escape(expectedPrefix) + "[A-Za-z0-9_-]+$";
        if (!Regex.IsMatch(m_id, idPattern))
        {
            message = $"关卡 ID 必须以 {expectedPrefix} 开头，并且只能包含字母、数字、下划线和短横线。";
            return false;
        }

        if (!m_isExisting && m_newIdExists)
        {
            message = $"关卡 ID 已存在：{m_id}。新建关卡不允许重复 ID。";
            return false;
        }

        int minSize = m_mode == MPLevelEditorMode.LargeImage ? MinLargeImageGridSize : MinGridSize;
        if (m_gridSize < minSize || m_gridSize > MaxGridSize)
        {
            message = $"网格尺寸必须在 {minSize}～{MaxGridSize} 之间。";
            return false;
        }

        if (!HasValidGridArrays())
        {
            message = "网格数据长度与当前尺寸不一致，请重新应用网格尺寸。";
            return false;
        }

        int unassignedCount = m_colorAssigned.Count(value => !value);
        if (unassignedCount > 0)
        {
            message = $"还有 {unassignedCount} 个格子没有底色，必须填充所有底色后才能保存。";
            return false;
        }

        if (m_mode == MPLevelEditorMode.LargeImage && string.IsNullOrWhiteSpace(m_levelName))
        {
            message = "大图关卡名称不能为空。";
            return false;
        }

        if (m_awardCoin < 0)
        {
            message = "通关金币不能小于 0。";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool HasValidGridArrays()
    {
        int expectedCount = m_gridSize * m_gridSize;
        return m_colors != null
            && m_colorAssigned != null
            && m_blocks != null
            && m_colors.Length == expectedCount
            && m_colorAssigned.Length == expectedCount
            && m_blocks.Length == expectedCount;
    }

    private void SaveCurrentLevel()
    {
        if (!TryValidate(out string validationMessage))
        {
            EditorUtility.DisplayDialog("无法保存", validationMessage, "确定");
            return;
        }

        if (m_isExisting && !EditorUtility.DisplayDialog(
                "保存已有改动",
                m_mode == MPLevelEditorMode.Main
                    ? $"将覆盖关卡 {m_id} 的原图和 JSON 数据，是否继续？"
                    : $"将覆盖关卡 {m_id} 的原图、缩略图和 JSON 数据，是否继续？",
                "保存",
                "取消"))
        {
            return;
        }

        var data = new MPLevelEditorData
        {
            Mode = m_mode,
            IsExisting = m_isExisting,
            ID = m_id,
            Name = m_levelName == null ? string.Empty : m_levelName.Trim(),
            AwardCoin = m_awardCoin,
            Size = m_gridSize,
            Colors = (Color[])m_colors.Clone(),
            ColorAssigned = (bool[])m_colorAssigned.Clone(),
            Blocks = (bool[])m_blocks.Clone(),
            SourceAssetPath = m_sourceAssetPath,
        };

        try
        {
            MPLevelEditorSaveResult result = MPLevelEditorStorage.Save(data);
            Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(result.PixelAssetPath);
            string loadError = savedTexture == null ? "保存后的原图没有被 Unity 正确导入。" : string.Empty;
            if (savedTexture != null && MPLevelEditorStorage.TryLoadExisting(savedTexture, out MPLevelEditorData savedData, out loadError))
            {
                ApplyData(savedData, savedTexture);
            }
            else
            {
                m_isExisting = true;
                m_isDirty = false;
                m_sourceTexture = savedTexture;
                m_sourceAssetPath = result.PixelAssetPath;
                Debug.LogWarning($"[MPLevelEditor] 保存成功，但重新载入校验失败：{loadError}");
            }

            string resultMessage = $"配置：{result.ConfigAssetPath}\n原图：{result.PixelAssetPath}";
            if (!string.IsNullOrEmpty(result.ThumbAssetPath))
            {
                resultMessage += $"\n缩略图：{result.ThumbAssetPath}";
            }

            EditorUtility.DisplayDialog("保存成功", resultMessage, "确定");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("保存失败", exception.Message, "确定");
        }
    }

    private void DeleteCurrentLevel()
    {
        if (!m_isExisting)
        {
            return;
        }

        string deleteResourceList =
            $"• {MPLevelEditorStorage.GetConfigAssetPath(m_mode)} 中对应的 JSON 记录\n" +
            $"• {MPLevelEditorStorage.BlockPixelAssetDirectory}/{m_id}.png";
        if (m_mode == MPLevelEditorMode.LargeImage)
        {
            deleteResourceList += $"\n• {MPLevelEditorStorage.ThumbAssetDirectory}/icon_{m_id}.png（如果存在）";
        }

        string deleteMessage =
            $"确定删除关卡 {m_id} 吗？\n\n" +
            $"将删除：\n" +
            deleteResourceList + "\n\n" +
            "该操作无法在编辑器中撤销。";
        if (!EditorUtility.DisplayDialog("删除关卡", deleteMessage, "确认删除", "取消"))
        {
            return;
        }

        string deletedId = m_id;
        MPLevelEditorMode deletedMode = m_mode;
        try
        {
            MPLevelEditorDeleteResult result = MPLevelEditorStorage.Delete(deletedMode, deletedId);
            CreateNewLevel(deletedMode, true);

            string resultMessage =
                $"已删除关卡：{deletedId}\n" +
                $"已更新配置：{result.ConfigAssetPath}\n" +
                $"已删除原图：{result.PixelAssetPath}";
            if (result.ThumbDeleted)
            {
                resultMessage += $"\n已删除缩略图：{result.ThumbAssetPath}";
            }

            EditorUtility.DisplayDialog("删除成功", resultMessage, "确定");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("删除失败", exception.Message, "确定");
        }
    }

    private bool CanDiscardUnsavedChanges(string actionName)
    {
        return !m_isDirty || EditorUtility.DisplayDialog(
            actionName,
            "当前关卡有未保存修改，继续操作会丢失这些修改。",
            "继续",
            "取消");
    }

    private void MarkDirty()
    {
        m_isDirty = true;
        Repaint();
    }

    private void RefreshNewIdExistence()
    {
        m_newIdExists = !m_isExisting && MPLevelEditorStorage.IdExists(m_id);
    }

    private static void DrawSectionTitle(string title)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }
}
