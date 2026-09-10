#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HQ.UIManager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>在独立预览场景检查教学，不进入游戏、不修改当前场景或玩家存档。</summary>
public sealed partial class MPGuidePreview : EditorWindow
{
    private Scene m_scene;
    private GameObject m_root;
    private Camera m_camera;
    private RenderTexture m_texture;
    private int m_stage;
    private int m_height = 2338;

    [MenuItem("MagicPixel/Guide/Preview and validate")]
    public static void Open()
    {
        ValidateLesson();
        var window = GetWindow<MPGuidePreview>("玩法教学预览");
        window.minSize = new Vector2(390, 660);
        window.Rebuild();
    }

    [MenuItem("MagicPixel/Guide/Export validation images")]
    public static void ExportValidationImages()
    {
        GetWindow<MPGuidePreview>("玩法教学预览").Export();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("上一页", EditorStyles.toolbarButton)) { m_stage = Mathf.Max(0, m_stage - 1); Rebuild(); }
        GUILayout.Label($"{m_stage + 1}/12  {(MPGuideLesson.Stage)m_stage}");
        if (GUILayout.Button("下一页", EditorStyles.toolbarButton)) { m_stage = Mathf.Min(11, m_stage + 1); Rebuild(); }
        if (GUILayout.Button("导出检查图", EditorStyles.toolbarButton)) Export();
        EditorGUILayout.EndHorizontal();
        int height = EditorGUILayout.IntPopup("手机画布高度（宽 1080）", m_height,
            new[] { "1920 / 16:9", "2338 / 手机长屏", "2160 / 18:9" }, new[] { 1920, 2338, 2160 });
        if (height != m_height) { m_height = height; Rebuild(); }
        if (m_texture != null)
        {
            Rect area = GUILayoutUtility.GetRect(position.width, position.height - 65);
            GUI.DrawTexture(area, m_texture, ScaleMode.ScaleToFit, false);
        }
    }

    private void Rebuild()
    {
        Release();
        m_scene = EditorSceneManager.NewPreviewScene();
        m_root = new GameObject("TutorialPreviewCanvas", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(m_root, m_scene);
        Canvas canvas = m_root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = m_root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1080, m_height);
        canvasRect.localScale = Vector3.one * 0.01f;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YooRes/Prefabs/GameMain/MPGuideView.prefab");
        GameObject page = Instantiate(prefab, canvasRect, false);
        page.name = "TutorialPreview";
        var view = page.AddComponent<MPGuideView>();
        view.BuildEditorPreview(LoadPrefab, LoadSprite, m_stage);
        ValidateLayout(view);

        var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, m_scene);
        m_camera = cameraObject.GetComponent<Camera>();
        m_camera.scene = m_scene;
        m_camera.transform.position = new Vector3(0, 0, -20);
        m_camera.orthographic = true;
        m_camera.orthographicSize = m_height * 0.005f;
        m_camera.nearClipPlane = 0.1f;
        m_camera.farClipPlane = 100f;
        m_camera.clearFlags = CameraClearFlags.SolidColor;
        m_camera.backgroundColor = Color.black;
        m_camera.enabled = false;
        m_texture = new RenderTexture(1080, m_height, 24);
        m_texture.Create();
        m_camera.targetTexture = m_texture;
        canvas.worldCamera = m_camera;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in page.GetComponentsInChildren<TMP_Text>())
        {
            if (text.preferredHeight > text.rectTransform.rect.height + 1)
                throw new InvalidOperationException($"引导英文文案高度不足：{text.name}, {text.preferredHeight}/{text.rectTransform.rect.height}");
        }
        m_camera.Render();
        Repaint();
    }

    private static GameObject LoadPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YooRes/Prefabs/GameMain/" + name + ".prefab");

    private static Sprite LoadSprite(string name)
    {
        string path = AssetDatabase.FindAssets(name + " t:Sprite", new[] { "Assets/YooRes/Sprites" })
            .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
        Sprite sprite = path == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("教学图片缺失：" + name);
        return sprite;
    }

    private void Export()
    {
        string directory = Path.Combine(Path.GetTempPath(), "MagicPixelGuidePreview");
        Directory.CreateDirectory(directory);
        int original = m_stage;
        int originalHeight = m_height;
        try
        {
            ValidateLesson();
            m_stage = 2;
            Rebuild();
            ValidateDrag();
            foreach (int height in new[] { 1920, 2338, 2160 })
            {
                m_height = height;
                for (m_stage = 0; m_stage <= 11; m_stage++)
                {
                    Rebuild();
                    RenderTexture previous = RenderTexture.active;
                    var image = new Texture2D(m_texture.width, m_texture.height, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = m_texture;
                        image.ReadPixels(new Rect(0, 0, m_texture.width, m_texture.height), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(Path.Combine(directory, $"{height}-stage-{m_stage:00}.png"), image.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; DestroyImmediate(image); }
                }
            }
            ExportEntryAndSettlement(directory);
            Debug.Log("教学流程、英文布局和 36 张画面检查完成：" + directory);
        }
        finally { m_stage = original; m_height = originalHeight; Rebuild(); }
    }

    private static void ValidateLayout(MPGuideView guide)
    {
        GameObject game = LoadPrefab("MPGameView");
        foreach (string path in new[] { "View/Content", "View/Content/Grid", "View/Content/Input", "View/Content/Vertical", "View/Content/Horizontal", "View/Btns/ModeSwitch" })
        {
            RectTransform expected = (RectTransform)game.transform.Find(path);
            RectTransform actual = (RectTransform)guide.transform.Find(path);
            Check(actual.anchoredPosition == expected.anchoredPosition && actual.sizeDelta == expected.sizeDelta &&
                actual.anchorMin == expected.anchorMin && actual.anchorMax == expected.anchorMax, "布局必须与游戏页一致：" + path);
        }
        int[] counts = { 0, 0, 1, 1, 2, 2, 1, 0, 7, 1, 1, 0 };
        Check(guide.PreviewHighlightCount == counts[(int)guide.PreviewStage], "高亮应按连续组框选");
        foreach (Image frame in guide.transform.Find("View/Content/Highlights").GetComponentsInChildren<Image>())
            Check(frame.type == Image.Type.Sliced && !frame.fillCenter && frame.transform.childCount == 0 &&
                frame.sprite.border.x > 0, "高亮必须使用单张九宫格图片");
    }

    private void ExportEntryAndSettlement(string directory)
    {
        m_height = 2338;
        m_stage = 6;
        Rebuild();
        m_root.GetComponentInChildren<MPGuideView>().gameObject.SetActive(false);
        GameObject settings = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YooRes/Prefabs/Pop/MPSettingPop.prefab"), m_root.transform, false);
        Button guide = settings.transform.Find("View/Window/GuideBtn").GetComponent<Button>();
        Check(guide != null && guide.gameObject.activeSelf, "设置页必须包含引导按钮");
        SavePreview(Path.Combine(directory, "settings-guide.png"));
        DestroyImmediate(settings);
        foreach (bool replay in new[] { false, true })
        {
            GameObject page = Instantiate(LoadPrefab("MPGameCompletedView"), m_root.transform, false);
            MPGameCompletedView completed = page.AddComponent<MPGameCompletedView>();
            UIManagerUtils.InitComponent(completed);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MPGameCompletedView).GetField("m_isGuide", flags).SetValue(completed, true);
            typeof(MPGameCompletedView).GetField("m_isGuideReplay", flags).SetValue(completed, replay);
            Color[] pixels = (Color[])typeof(MPGuideView).GetField("PixelColors", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            typeof(MPGameCompletedView).GetField("m_guidePixelColors", flags).SetValue(completed, pixels);
            typeof(MPGameCompletedView).GetMethod("RefreshCustomModeLayout", flags).Invoke(completed, null);
            typeof(MPGameCompletedView).GetMethod("RefreshPicture", flags).Invoke(completed, null);
            Button next = (Button)typeof(MPGameCompletedView).GetField("m_nextBtn", flags).GetValue(completed);
            Button again = (Button)typeof(MPGameCompletedView).GetField("m_replayBtn", flags).GetValue(completed);
            Check(next.gameObject.activeSelf == !replay, "只有首次引导结算可以进入下一关");
            Check(!replay || Mathf.Approximately(((RectTransform)again.transform).anchoredPosition.x, 0), "重看引导的 Replay 必须居中");
            SavePreview(Path.Combine(directory, replay ? "completed-replay.png" : "completed-first.png"));
            // 预览场景使用立即销毁，避免运行时代码的延迟 Destroy 进入编辑器日志。
            foreach (Transform pixel in page.GetComponentsInChildren<Transform>(true).Where(node => node.name.StartsWith("Pixel_")))
                DestroyImmediate(pixel.gameObject);
            DestroyImmediate(page);
        }
    }

    private void SavePreview(string path)
    {
        Canvas.ForceUpdateCanvases();
        m_camera.Render();
        RenderTexture previous = RenderTexture.active;
        Texture2D image = new Texture2D(1080, m_height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = m_texture;
            image.ReadPixels(new Rect(0, 0, 1080, m_height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; DestroyImmediate(image); }
    }

    private static void ValidateLesson()
    {
        var lesson = new MPGuideLesson();
        Check(!lesson.TryMark(0) && !lesson.SwitchMode(), "欢迎页不能操作棋盘");
        Check(lesson.Advance() && lesson.CanSwitch, "开始后进入模式切换");
        Check(!lesson.Advance(), "不能跳过模式切换");
        Check(lesson.SwitchMode() && lesson.FillMode, "切换后必须进入填色练习");
        Check(!lesson.TryMark(-1) && !lesson.TryMark(25) && !lesson.TryMark(5), "越界和未开放行不能修改");
        int markCount = 0;
        int safety = 0;
        while (lesson.Current != MPGuideLesson.Stage.Completed && safety++ < 20)
        {
            if (lesson.CanSwitch) { Check(lesson.SwitchMode(), "切换失败"); continue; }
            if (lesson.FirstTarget() >= 0) Check(!lesson.Advance(), "未完成操作不能下一步");
            int index;
            while ((index = lesson.FirstTarget()) >= 0)
            {
                Check(lesson.TryMark(index), "目标必须可操作");
                Check(!lesson.TryMark(index), "重复操作应无副作用");
                markCount++;
            }
            Check(lesson.Advance(), "步骤无法推进");
        }
        Check(lesson.Current == MPGuideLesson.Stage.Completed && markCount == 24, "完整教学必须包含 24 次操作和 1 格预填");
        for (int i = 0; i < 25; i++) Check(lesson.IsMarked(i), "完成时不得遗漏格子");
        Check(!lesson.Advance() && !lesson.SwitchMode() && !lesson.TryMark(0), "完成后不得继续改变教学");
        for (int line = 0; line < 5; line++)
        {
            Check(Clue(false, line) == MPGuideLesson.RowHint(line).Replace("  ", " "), "行提示与答案不符");
            Check(Clue(true, line) == MPGuideLesson.ColumnHint(line).Replace("\n", " "), "列提示与答案不符");
        }
    }

    private void ValidateDrag()
    {
        var view = m_root.GetComponentInChildren<MPGuideView>();
        var input = m_root.GetComponentInChildren<MPGuideInput>();
        var raycaster = m_root.AddComponent<GraphicRaycaster>();
        RectTransform board = m_root.GetComponentInChildren<MPGuideView>().transform.Find("View/Content/Grid") as RectTransform;
        RectTransform first = (RectTransform)board.Find("Cell0");
        RectTransform last = (RectTransform)board.Find("Cell4");
        var pointer = new PointerEventData(null)
        {
            pointerId = 7,
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(m_camera, first.position),
            pointerPressRaycast = new RaycastResult { module = raycaster }
        };
        input.OnPointerDown(pointer);
        Check(view.PreviewStage == MPGuideLesson.Stage.FullRow, "只点第一格不能完成整行");
        Check(!view.PreviewHintVisible, "首次有效操作后手势必须隐藏");
        pointer.position = RectTransformUtility.WorldToScreenPoint(m_camera, last.position);
        input.OnDrag(pointer);
        Check(view.PreviewStage == MPGuideLesson.Stage.Consecutive, "快速拖拽应补齐中间格并进入下一步");
        pointer.position = RectTransformUtility.WorldToScreenPoint(m_camera, board.Find("Cell8").position);
        input.OnDrag(pointer);
        Check(view.PreviewStage == MPGuideLesson.Stage.Consecutive, "跨步骤的旧手势应停止");
        input.OnPointerUp(pointer);
        DestroyImmediate(raycaster);
    }

    private static string Clue(bool column, int line)
    {
        var runs = new System.Collections.Generic.List<int>();
        int count = 0;
        for (int i = 0; i <= 5; i++)
        {
            if (i < 5 && MPGuideLesson.IsFill(column ? i * 5 + line : line * 5 + i)) count++;
            else if (count > 0) { runs.Add(count); count = 0; }
        }
        return string.Join(" ", runs);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private void OnDisable() { Release(); }
    private void Release()
    {
        if (m_camera != null) m_camera.targetTexture = null;
        if (m_texture != null) { m_texture.Release(); DestroyImmediate(m_texture); }
        if (m_scene.IsValid()) EditorSceneManager.ClosePreviewScene(m_scene);
        m_texture = null;
        m_root = null;
        m_camera = null;
    }
}
#endif
