#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.U2D;
using UnityEngine.U2D;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AMS.UI.SoftMask
{
    using static UISoftMaskUtils;

    [CustomEditor(typeof(UISoftMask), true), CanEditMultipleObjects]
    public class UISoftMaskEditor : Editor
    {
        private UISoftMask m_Target;

        private SerializedProperty m_Script;
        private SerializedProperty m_IncludedShaders;

        private SerializedObject m_GraphicsSettingsObject;

        public List<string> m_ExcludeProperties = new() { "m_Script" };

        private const string k_PixelsPerUnitMultiplierProperty = "m_PixelsPerUnitMultiplier";
        private const string k_RaycastThresholdProperty = "m_RaycastThreshold";
        private const string k_ScriptProperty = "m_Script";

        private const string k_AtlasTightModeWarningMessage =
            "\nSprite mask is part of an atlas and uses a 'Tight' packing mode. To prevent rendering outbound seams please set packing mode to 'Full Rect'.\n";

        private const string k_OverrideMaterialMessage =
            " doesn't support UI Soft Mask. Please add support to it or select a different shader.\n";

        private const string k_InvalidFontMaterialMessage =
            " doesn't support UI Soft Mask.\nPlease add support to it or select a different shader.\n\nHave you imported TMP_SoftMaskSupport package?\n" +
            "For TMP support please import package at plugin's folder 'Resources/Packages/TMP_SoftMaskSupport.unitypackage'.\n" +
            "Supported shaders:\n-TMP_SDF;\n-TMP_SDF_Mobile;\n";

        private const string k_IncludeShaderWarningMessage =
            "\nIt's required to include AMS shaders into 'Project Settings > Graphics > Always Included Shaders' to prevent Unity skip variants/shader from builds.\n";

        private static readonly string k_ScrollRectHandleWarningMessage =
            "\nParent ScrollRect detected.\n\n" +
            "If you're experiencing performance issues with multiple Soft Masks inside the viewport,\n" +
            $"consider adding '{nameof(ScrollRectSoftMaskHandler)}' to the ScrollRect to optimize masking.\n";

        private const string k_UnityMaskWarningMessage =
            "\nBuilt-in Mask component detected in the hierarchy!\n" +
            "Unity's Mask overrides child object material, making it incompatible with UI Soft Mask.\n" +
            "Consider removing/disable it to avoid rendering conflicts.\n";

        private void OnEnable()
        {
            m_Target = target as UISoftMask;
            m_Script = serializedObject.FindProperty(k_ScriptProperty);

            var graphicsSettingsAsset =
                AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            m_GraphicsSettingsObject = new SerializedObject(graphicsSettingsAsset);
            m_IncludedShaders = m_GraphicsSettingsObject.FindProperty("m_AlwaysIncludedShaders");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(m_Script);
            EditorGUI.EndDisabledGroup();

            switch (m_Target.maskUV)
            {
                case MaskUV.Sliced:
                    if (m_ExcludeProperties.Contains(k_PixelsPerUnitMultiplierProperty))
                        m_ExcludeProperties.Remove(k_PixelsPerUnitMultiplierProperty);
                    break;
                case MaskUV.Simple:
                default:
                    if (!m_ExcludeProperties.Contains(k_PixelsPerUnitMultiplierProperty))
                        m_ExcludeProperties.Add(k_PixelsPerUnitMultiplierProperty);
                    break;
            }

            if (m_Target.raycastTarget)
            {
                if (m_ExcludeProperties.Contains(k_RaycastThresholdProperty))
                    m_ExcludeProperties.Remove(k_RaycastThresholdProperty);
            }
            else
            {
                if (!m_ExcludeProperties.Contains(k_RaycastThresholdProperty))
                    m_ExcludeProperties.Add(k_RaycastThresholdProperty);
            }

            DrawPropertiesExcluding(serializedObject, m_ExcludeProperties.ToArray());

            CheckAtlasPackingMode();
            CheckValidTargetMaterial();
            CheckIncludedShaders();
            CheckFontMaterials();
            CheckScrollRectHandle();
            CheckUnityMasks();

            serializedObject.ApplyModifiedProperties();
        }

        private void CheckValidTargetMaterial()
        {
            if (m_Target.overrideMaterial is var overrideMaterial && overrideMaterial &&
                !MaterialHasSoftMask(overrideMaterial))
            {
                EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("\n" + overrideMaterial.name + k_OverrideMaterialMessage, MessageType.Warning);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void CheckAtlasPackingMode()
        {
            if (m_Target.mask is var sprite && sprite && sprite.packed)
            {
                var assetPath = AssetDatabase.GetAssetPath(sprite);
                var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                if (!textureImporter)
                    return;

                var textureSettings = new TextureImporterSettings();
                textureImporter.ReadTextureSettings(textureSettings);

                if (textureSettings.spriteMeshType == SpriteMeshType.Tight)
                {
                    EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Fix", GUILayout.Width(50), GUILayout.ExpandHeight(true)))
                    {
                        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                        textureImporter.SetTextureSettings(textureSettings);
                        textureImporter.SaveAndReimport();
                        UpdateAtlas(sprite);
                    }

                    EditorGUILayout.HelpBox(k_AtlasTightModeWarningMessage, MessageType.Warning);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void CheckFontMaterials()
        {
            foreach (var fontMaterialData in m_Target.TMPFontMaterialData)
                if (fontMaterialData is { Instances: { } instances })
                    foreach (var material in instances)
                        if (material.Value is var fontMaterial && !MaterialHasSoftMask(material.Value))
                        {
                            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.HelpBox(
                                $"\nTMP material [{fontMaterial.name}]" + k_InvalidFontMaterialMessage,
                                MessageType.Warning);
                            EditorGUILayout.EndHorizontal();
                            break;
                        }
        }

        private void CheckIncludedShaders()
        {
            m_GraphicsSettingsObject.Update();

            var shaders = new List<Shader>();
            for (var i = 0; i < m_IncludedShaders.arraySize; i++)
            {
                if (m_IncludedShaders.GetArrayElementAtIndex(i).objectReferenceValue is Shader shader && shader)
                    shaders.Add(shader);
            }

            if (shaders.Contains(s_SoftMaskShader) && shaders.Contains(s_SoftMaskBlitShader))
                return;

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fix", GUILayout.Width(50), GUILayout.ExpandHeight(true)))
                ForceIncludeShaders();
            EditorGUILayout.HelpBox(k_IncludeShaderWarningMessage, MessageType.Error);
            EditorGUILayout.EndHorizontal();
        }

        private void UpdateAtlas(Sprite sprite)
        {
            AssetDatabase.FindAssets("t:spriteatlas").ToList().ForEach(guid =>
            {
                var atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                var sprites = atlas.GetPackables().ToList()
                    .Select(o => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(o)));

                if (sprites.Contains(sprite))
                {
                    var spriteAtlasImporter = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
                    spriteAtlasImporter?.SaveAndReimport();
                }
            });

            m_Target.ForceUpdateMask();
        }

        private void CheckScrollRectHandle()
        {
            if (!m_Target || m_Target.GetComponentsInParent<ScrollRect>() is not { } scrollRects ||
                scrollRects.FirstOrDefault() is not { } firstScrollRect ||
                m_Target.transform == firstScrollRect.viewport.transform ||
                firstScrollRect.GetComponent<ScrollRectSoftMaskHandler>())
                return;

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add", GUILayout.Width(50), GUILayout.ExpandHeight(true)))
                firstScrollRect.gameObject.AddComponent<ScrollRectSoftMaskHandler>();

            EditorGUILayout.HelpBox(k_ScrollRectHandleWarningMessage, MessageType.Info);
            EditorGUILayout.EndHorizontal();
        }

        private void CheckUnityMasks()
        {
            if (m_Target && (m_Target.GetComponentsInParent<Mask>(true) is { Length: > 0 } parentMasks &&
                             parentMasks.Any(m => m.enabled) ||
                             m_Target.GetComponentsInChildren<Mask>(true) is { Length: > 0 } childrenMask &&
                             childrenMask.Any(m => m.enabled)))
            {
                EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(k_UnityMaskWarningMessage, MessageType.Warning);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif