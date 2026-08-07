using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AMS.UI.SoftMask
{
    public static class UISoftMaskUtils
    {
        //UISoftMaskShader
        private const string k_DefaultSoftMaskShader = "AMS/UISoftMask";
        public const string k_USING_SOFT_MASK = "USING_SOFT_MASK";
        public const string k_DEBUG_MASK = "_DEBUG_MASK";

        public static readonly int s_WORLDCANVAS = Shader.PropertyToID("_WORLDCANVAS");

        public static readonly int s_SoftMaskID = Shader.PropertyToID("_SoftMask");
        public static readonly int s_RectUvSizeID = Shader.PropertyToID("_RectUvSize");
        public static readonly int s_WorldCanvasMatrixID = Shader.PropertyToID("_WorldCanvasMatrix");
        public static readonly int s_OverlayCanvasMatrixID = Shader.PropertyToID("_OverlayCanvasMatrix");
        public static readonly int s_MaskDataSettingsID = Shader.PropertyToID("_MaskDataSettings");


        public static readonly Shader s_SoftMaskShader = Shader.Find(k_DefaultSoftMaskShader);

        //UISoftMaskBlitShader
        private const string k_SoftMaskBlitShader = "Hidden/AMS/UISoftMaskBlit";
        public static readonly Shader s_SoftMaskBlitShader = Shader.Find(k_SoftMaskBlitShader);
        public static readonly int s_ParentMaskID = Shader.PropertyToID("_ParentMask");
        public static readonly int s_ParentMaskMatrixID = Shader.PropertyToID("_ParentMaskMatrix");
        public static readonly int s_ParentMaskScaleID = Shader.PropertyToID("_ParentMaskScale");
        public static readonly int s_ParentMaskDataID = Shader.PropertyToID("_ParentMaskData");
        public static readonly int s_FalloffID = Shader.PropertyToID("_FallOff");
        public static readonly int s_AtlasDataID = Shader.PropertyToID("_AtlasData");
        public const string k_SLICED = "_SLICED";
        public static readonly int s_SliceScaleID = Shader.PropertyToID("_SliceScale");
        public static readonly int s_SliceBorderID = Shader.PropertyToID("_SliceBorder");

        public const string k_SoftMaskMatTag = "SoftMaskMat:";
        private const string k_SoftMaskFontMatTag = "SoftMaskFontMat:";

        // Stencil
        public static readonly int s_StencilCompID = Shader.PropertyToID("_StencilComp");
        public static readonly int s_StencilID = Shader.PropertyToID("_Stencil");
        public static readonly int s_StencilOpID = Shader.PropertyToID("_StencilOp");
        public static readonly int s_StencilWriteMaskID = Shader.PropertyToID("_StencilWriteMask");
        public static readonly int s_StencilReadMaskID = Shader.PropertyToID("_StencilReadMask");

        private static Material s_DefaultSoftMaskBlitMaterial;

        public static Material defaultSoftMaskBlitMaterial
        {
            get
            {
                if (s_DefaultSoftMaskBlitMaterial)
                    return s_DefaultSoftMaskBlitMaterial;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!s_SoftMaskBlitShader)
                {
                    Debug.LogWarning($"[{nameof(UISoftMaskUtils)}] Soft mask blit shader is null." +
                                     "Mask rendering will not function.");
                    return null;
                }
#endif

                s_DefaultSoftMaskBlitMaterial = new Material(s_SoftMaskBlitShader)
                {
                    name = "DefaultSoftMaskBlitMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };

                return s_DefaultSoftMaskBlitMaterial;
            }
        }

        public enum MaskUV
        {
            Simple,
            Sliced
        }

        [Serializable]
        internal class MaskData
        {
            internal MaskUV uvType = default;
            internal Sprite sprite = null;
            internal float pixelsPerUnitMultiplier = 0;
            internal float fallOff = 0;
            internal Vector4 slicedBorder = default;
            internal Vector4 settings = default; // x: mode | y: opacity | z: gamma2linear | w: rectClamp
            internal UISoftMask parentMask = null;
#if UNITY_EDITOR
            internal bool preview = false;
#endif
        }

        public enum MaskMode
        {
            Normal = 0,
            Inverted = 1
        }

        public enum MaskSize
        {
            _32 = 32,
            _64 = 64,
            _128 = 128,
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096
        }

        [Serializable]
        internal class ExternalMaterialData
        {
            internal readonly Material keyMaterial;
            internal readonly Material instanceMaterial;

            internal ExternalMaterialData(Material keyMaterial, Material instanceMaterial)
            {
                this.keyMaterial = keyMaterial;
                this.instanceMaterial = instanceMaterial;
            }

            internal static bool FindData(List<ExternalMaterialData> externalMaterialDataList, Material targetMaterial,
                out ExternalMaterialData foundData)
            {
                foreach (var externalMatData in externalMaterialDataList)
                {
                    if (externalMatData is not { keyMaterial: var value } || value != targetMaterial)
                        continue;

                    foundData = externalMatData;
                    return true;
                }

                foundData = null;
                return false;
            }
        }

        internal class FontMaterialData
        {
            private readonly TMP_FontAsset fontAsset;
            private readonly List<Material> fontMaterials = new();
            private readonly List<string> fontMaterialKeys = new();
            internal Dictionary<string, Material> Instances = new();
            internal int maskID;

            internal FontMaterialData(TMP_FontAsset fontAsset)
            {
                this.fontAsset = fontAsset;
            }

            internal Material GetRelativeKeyMaterial(string keyName)
            {
                return fontMaterials[fontMaterialKeys.IndexOf(keyName)];
            }

            internal static bool FindData(List<FontMaterialData> fontMaterialDataList, TMP_FontAsset targetAsset,
                out FontMaterialData foundData)
            {
                foreach (var fontMaterialData in fontMaterialDataList)
                {
                    if (fontMaterialData is not { fontAsset: var value } || value != targetAsset)
                        continue;

                    foundData = fontMaterialData;
                    return true;
                }

                foundData = null;
                return false;
            }

            internal Material TryRegisterInstanceMaterial(Material material)
            {
                Instances ??= new Dictionary<string, Material>();

                if (Instances.Values.Contains(material))
                    return material;

                if (fontMaterials.Contains(material))
                {
                    var keyName = material.name;
                    if (Instances.Count == fontMaterials.Count && Instances[keyName] is { } foundInstanceMaterial)
                        return foundInstanceMaterial;

                    Instances.Clear();
                    for (var i = 0; i < fontMaterials.Count; i++)
                    {
                        var key = fontMaterials[i];
                        if (CreateNewFontMaterial(key) is { } newInstanceMaterial)
                        {
                            Instances.Add(key.name, newInstanceMaterial);
                        }
                    }

                    return Instances[keyName];
                }

                if (CreateNewFontMaterial(material) is not { } newInstance)
                    return null;

                fontMaterials.Add(material);
                Instances.Add(material.name, newInstance);
                fontMaterialKeys.Add(material.name);

                return newInstance;
            }

            private Material CreateNewFontMaterial(Material material)
            {
                return !material
                    ? null
                    : new Material(material)
                    {
                        name = $"{k_SoftMaskFontMatTag}{maskID}:{material.name}",
                        hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor |
                                    HideFlags.NotEditable
                    };
            }
        }

        public static bool MaterialHasSoftMask(Material targetMaterial) =>
            targetMaterial && targetMaterial.HasProperty(s_SoftMaskID);

        internal static UISoftMask FindFirstValidSoftMask(Transform target)
        {
            while (target)
            {
                var softMask = target.GetComponent<UISoftMask>();
                if (softMask && softMask.enabled)
                    return softMask;

                target = target.parent;
            }

            return null;
        }


        internal static int ParentCount(Transform child)
        {
            var count = 0;
            var current = child.parent;

            while (current)
            {
                count++;
                current = current.parent;
            }

            return count;
        }

        internal static int DescendantsCount(Transform parent)
        {
            var count = 0;

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                count++;
                count += DescendantsCount(child);
            }

            return count;
        }

#if UNITY_EDITOR
        [MenuItem("Window/AMS/UISoftMask/Force Include Shaders (ProjectSettings)", priority = 0)]
        public static void ForceIncludeShaders()
        {
            var graphicsSettingsObj =
                AssetDatabase.LoadAssetAtPath<GraphicsSettings>(
                    "ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettingsObj);
            var includedShaderProperty = serializedObject.FindProperty("m_AlwaysIncludedShaders");
            var shaders = new List<Shader>();
            for (var i = 0; i < includedShaderProperty.arraySize; i++)
            {
                if (includedShaderProperty.GetArrayElementAtIndex(i).objectReferenceValue is Shader shader && shader)
                    shaders.Add(shader);
                else
                {
                    includedShaderProperty.DeleteArrayElementAtIndex(i);
                    i--;
                }
            }

            if (!shaders.Contains(s_SoftMaskShader))
            {
                var index = includedShaderProperty.arraySize;
                includedShaderProperty.InsertArrayElementAtIndex(index);
                var arrayElem = includedShaderProperty.GetArrayElementAtIndex(index);
                arrayElem.objectReferenceValue = s_SoftMaskShader;
            }

            if (!shaders.Contains(s_SoftMaskBlitShader))
            {
                var index = includedShaderProperty.arraySize;
                includedShaderProperty.InsertArrayElementAtIndex(index);
                var arrayElem = includedShaderProperty.GetArrayElementAtIndex(index);
                arrayElem.objectReferenceValue = s_SoftMaskBlitShader;
            }

            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
#endif
    }
}