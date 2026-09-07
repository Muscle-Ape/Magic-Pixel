using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 用真实 UGUI 渲染生成拼图，避免线性颜色空间的透明混合与离线合成不一致。
/// 只在编辑器执行，运行时不创建摄像机、RenderTexture 或新图片节点。
/// </summary>
public static class MPTransitionArtworkBaker
{
    public const string PrefabPath = "Assets/YooRes/Prefabs/Home/MPTransitionView.prefab";
    public const string TexturePath = "Assets/YooRes/Sprites/Transition/transition_cloud_curtain.png";
    public const string ExitTexturePath = "Assets/YooRes/Sprites/Transition/transition_cloud_curtain_exit.png";
    private const string DesignPath = "Design/UI/Transition/transition_cloud_curtain_motion.png";
    private const string ExitDesignPath = "Design/UI/Transition/transition_cloud_curtain_motion_exit.png";

    [MenuItem("Tools/MagicPixel/Transition/Bake Cloud Curtain")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出播放模式，再烘焙过渡云幕。");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("找不到过渡预制体：" + PrefabPath);

        // 两张都成功生成并校验后再写入，避免只更新了一半的画面。
        byte[] entry = CapturePng(prefab, false);
        byte[] exit = CapturePng(prefab, true);
        Directory.CreateDirectory(Path.GetDirectoryName(DesignPath));
        File.WriteAllBytes(DesignPath, entry);
        File.WriteAllBytes(ExitDesignPath, exit);
        File.WriteAllBytes(TexturePath, entry);
        File.WriteAllBytes(ExitTexturePath, exit);
        // 保留两张图集的 .meta 和 Sprite ID，不改变方块或终点数组的引用。
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(ExitTexturePath, ImportAssetOptions.ForceUpdate);
        RepairExitSpriteReferences();
        Debug.Log("[MPTransitionArtworkBaker] 已从分层前景生成入场/退场两套云幕拼图。");
    }

    [MenuItem("Tools/MagicPixel/Transition/Repair Exit Sprite References")]
    public static void RepairExitSpriteReferences()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出播放模式，再修复过渡预制体引用。");

        Dictionary<string, Sprite> spritesByName = new Dictionary<string, Sprite>();
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ExitTexturePath))
        {
            if (asset is Sprite sprite)
                spritesByName[sprite.name] = sprite;
        }

        // 根据行列名排序，不能依赖 LoadAllAssetsAtPath 返回的子资源顺序。
        Sprite[] sprites = new Sprite[36];
        for (int i = 0; i < sprites.Length; i++)
        {
            string spriteName = $"transition_cloud_exit_tile_r{i / 4 + 1:00}_c{i % 4 + 1:00}";
            if (!spritesByName.TryGetValue(spriteName, out sprites[i]))
                throw new InvalidOperationException("退场图集缺少切片：" + spriteName + "，未修改预制体。");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform grid = root.transform.Find("View/Grid");
            Component layout = grid != null ? grid.GetComponent("MPTransitionGridLayout") : null;
            if (layout == null || grid.childCount != sprites.Length)
                throw new InvalidOperationException("View/Grid 必须具有布局组件及 36 个方块，未修改预制体。");

            // 保持独立 Editor 程序集：用序列化属性写入，不直接引用运行时类型。
            SerializedObject serialized = new SerializedObject(layout);
            serialized.Update();
            SerializedProperty references = serialized.FindProperty("m_exitSprites");
            if (references == null || !references.isArray)
                throw new InvalidOperationException("布局组件尚未正确编译或缺少 m_exitSprites 字段，未修改预制体。");

            bool changed = references.arraySize != sprites.Length;
            references.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                SerializedProperty element = references.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == sprites[i])
                    continue;
                element.objectReferenceValue = sprites[i];
                changed = true;
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException("退场切片引用写入失败，请检查预制体是否可保存。");
            }
            Debug.Log("[MPTransitionArtworkBaker] 36 张退场切片引用已校验并绑定。");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/MagicPixel/Transition/Repair Exit Sprite References", true)]
    private static bool CanRepairExitSpriteReferences()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static byte[] CapturePng(GameObject prefab, bool exit)
    {
        Texture2D texture = Capture(prefab, 836, 1881, true, exit ? ApplyExitPose : (Action<Transform>)null);
        try
        {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a != byte.MaxValue)
                    throw new InvalidOperationException("云幕烘焙不完整：Sky 必须完全不透明并覆盖画布，已取消写入。");
            }
            return texture.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    public static void ApplyExitPose(Transform root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        Transform grid = root.Find("View/Grid");
        Transform artwork = root.Find("View/Motion/Artwork");
        // MagicPixel.Editor 的 asmdef 不能直接引用 Assembly-CSharp；仅在烘焙时读取实际组件的常量。
        // 不在编辑器复制三份数值，确保终点图集始终使用与运行时动画相同的位移。
        Component layout = grid != null ? grid.GetComponent("MPTransitionGridLayout") : null;
        if (layout == null || artwork == null)
            throw new InvalidOperationException("过渡预制体缺少 View/Grid 的布局组件或 View/Motion/Artwork。");

        RectTransform back = artwork.Find("CloudBack") as RectTransform;
        RectTransform middle = artwork.Find("CloudMiddle") as RectTransform;
        RectTransform front = artwork.Find("CloudFront") as RectTransform;
        if (back == null || middle == null || front == null)
            throw new InvalidOperationException("过渡预制体缺少 CloudBack、CloudMiddle 或 CloudFront。");

        Type layoutType = layout.GetType();
        float backTravel = ReadCloudTravel(layoutType, "CLOUD_BACK_TRAVEL");
        float middleTravel = ReadCloudTravel(layoutType, "CLOUD_MIDDLE_TRAVEL");
        float frontTravel = ReadCloudTravel(layoutType, "CLOUD_FRONT_TRAVEL");
        back.anchoredPosition += Vector2.right * backTravel;
        middle.anchoredPosition += Vector2.right * middleTravel;
        front.anchoredPosition += Vector2.right * frontTravel;
        // 星点完成整圈，位置与初始姿态相同，无需烘焙浮点旋转误差。
    }

    private static float ReadCloudTravel(Type layoutType, string fieldName)
    {
        FieldInfo field = layoutType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (field == null || !field.IsLiteral || field.FieldType != typeof(float))
            throw new InvalidOperationException($"布局组件缺少有效的 float 常量：{layoutType.FullName}.{fieldName}，已取消烘焙。");

        float value = (float)field.GetRawConstantValue();
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new InvalidOperationException($"云层位移不是有效数值：{fieldName}，已取消烘焙。");
        return value;
    }

    [MenuItem("Tools/MagicPixel/Transition/Bake Cloud Curtain", true)]
    private static bool CanBake()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    public static Texture2D Capture(GameObject prefab, int width, int height, bool layered,
        Action<Transform> configure = null)
    {
        Scene bakeScene = EditorSceneManager.NewPreviewScene();
        GameObject canvasObject = null;
        Camera camera = null;
        Texture2D result = null;
        RenderTexture target = null;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            GameObject cameraObject = new GameObject("TransitionBakeCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, bakeScene);
            camera = cameraObject.GetComponent<Camera>();
            camera.scene = bakeScene;
            camera.enabled = false;
            canvasObject = new GameObject("TransitionBakeCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, bakeScene);
            canvasObject.layer = 5;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            RectTransform canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(width, height);

            GameObject instance = UnityEngine.Object.Instantiate(prefab, canvasRect, false);
            instance.SetActive(true);
            Transform grid = instance.transform.Find("View/Grid");
            Transform motion = instance.transform.Find("View/Motion");
            if (grid == null || motion == null || grid.GetComponent<CanvasGroup>() == null ||
                motion.GetComponent<CanvasGroup>() == null)
                throw new InvalidOperationException("过渡预制体缺少 Grid/Motion 或 CanvasGroup。");

            foreach (Image image in motion.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null)
                    throw new InvalidOperationException("分层图片未设置 Sprite：" + image.name);
            }

            grid.GetComponent<CanvasGroup>().alpha = layered ? 0f : 1f;
            motion.GetComponent<CanvasGroup>().alpha = layered ? 1f : 0f;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid);
            configure?.Invoke(instance.transform);
            Canvas.ForceUpdateCanvases();

            camera.orthographic = true;
            camera.orthographicSize = height * 0.5f;
            camera.aspect = width / (float)height;
            camera.transform.position = canvasRect.position + new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 5;

            camera.allowHDR = false;
            camera.allowMSAA = false;
            target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            result = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            result.Apply();
            return result;
        }
        catch
        {
            if (result != null)
                UnityEngine.Object.DestroyImmediate(result);
            throw;
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (camera != null)
                camera.targetTexture = null;
            if (target != null)
                RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.ClosePreviewScene(bakeScene);
        }
    }
}
