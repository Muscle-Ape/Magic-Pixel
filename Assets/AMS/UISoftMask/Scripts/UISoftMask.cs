using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AMS.UI.SoftMask
{
    using static UISoftMaskUtils;

    [AddComponentMenu("UI/AMS/UI Soft Mask"),
     HelpURL("https://ams.sorialexandre.tech/ui-soft-mask/")]
    public class UISoftMask : RectUV, ICanvasRaycastFilter
    {
        [Header("Mask Settings")]
        [SerializeField]
        private Sprite m_Mask;

        public Sprite mask
        {
            get => m_Mask;
            set
            {
                if (m_Mask == value)
                    return;

                m_Mask = value;
                ComputeFinalMaskForRendering();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField,
         Tooltip("Preview mask output (Editor/Development build only)." +
                 "\n\nNote: Requires '_DEBUG_MASK' feature. For node shaders use 'UISoftMaskWithPreview'. For hand‑coded, see 'UISoftMaskPass.hlsl'.")]
        private bool m_MaskPreview;

        /// <summary>
        /// Enable/disable mask preview for debugging purposes (Editor/Development build only).
        /// <para></para>
        /// Note: Requires '_DEBUG_MASK' feature. For node shaders use 'UISoftMaskWithPreview'. For hand‑coded, see 'UISoftMaskPass.hlsl'."
        /// </summary>
        public bool maskPreview
        {
            get => m_MaskPreview || maskData.parentMask && maskData.parentMask.maskPreview;
            set => m_MaskPreview = value;
        }
#endif

        [SerializeField, Tooltip("The mask output size.\n\nNote: Keep it low to save memory allocation.")]
        private MaskSize m_MaskSize = MaskSize._128;

        public MaskSize maskSize
        {
            get => m_MaskSize;

            set
            {
                if (m_MaskSize == value)
                    return;

                m_MaskSize = value;
                ComputeFinalMaskForRendering();
            }
        }

        [SerializeField, Tooltip("Select between a Simple or a Sliced (9-slicing) uv coordinate.")]
        private MaskUV m_MaskUV = MaskUV.Simple;

        public MaskUV maskUV => m_MaskUV;

        [SerializeField, Min(0.01f)]
        private float m_PixelsPerUnitMultiplier = 1;

        public float pixelsPerUnitMultiplier
        {
            get => m_PixelsPerUnitMultiplier;
            set
            {
                if (Mathf.Approximately(m_PixelsPerUnitMultiplier, value))
                    return;

                m_PixelsPerUnitMultiplier = value;
                ComputeFinalMaskForRendering();
            }
        }

        [SerializeField]
        private MaskMode m_MaskMode = MaskMode.Normal;

        public MaskMode maskMode
        {
            get => m_MaskMode;
            set => m_MaskMode = value;
        }

        [SerializeField,
         Tooltip("Allows mask bleeding outside the rect area.\n\n" +
                 "Note: this will draw outside by stretching the rect pixel from each corresponding side. " +
                 "Use accordingly, as this behaviour may not be suitable for all mask setups.")]
        private bool m_AllowBleed;

        public bool allowBleed
        {
            get => m_AllowBleed;
            set => m_AllowBleed = value;
        }

        internal MaskData maskData { get; private set; } = new();

        [SerializeField, Range(0, 1)]
        private float m_FallOff = 1;

        public float fallOff
        {
            get => m_FallOff;
            set => m_FallOff = value;
        }

        [SerializeField, Range(0, 1)]
        private float m_Opacity = 1;

        public float opacity
        {
            get => m_Opacity;
            set => m_Opacity = value;
        }

        [Header("Override Settings")]
        [SerializeField,
         Tooltip("Use this to override the temporary mask material with a material asset from your project." +
                 "\n\nNote: It requires a unique material per mask and the shader must be compatible with AMS UI Soft Mask.")]
        private Material m_OverrideMaskMaterial;

        private Material m_LateOverrideMaskMaterial;

        /// <summary>
        /// Use this to override the temporary mask material with a material asset from your project.
        /// <para></para>
        /// Note: It requires a unique material per mask and the shader must be compatible with AMS UI Soft Mask.
        /// </summary>
        public Material overrideMaterial
        {
            get => m_OverrideMaskMaterial;
            set
            {
                m_OverrideMaskMaterial = value;
                CheckTargetMaterial();
            }
        }

        [SerializeField, Tooltip("Override transform to decouple mask size, position and rotation.")]
        private RectTransform m_OverrideTransform;

        /// <summary>
        /// Override transform to decouple mask size, position and rotation.
        /// </summary>
        public RectTransform overrideTransform
        {
            get => m_OverrideTransform ? m_OverrideTransform : rectTransform;

            set
            {
                if (m_OverrideTransform == value)
                    return;

                m_OverrideTransform = value;
                ForceUpdateMask();
            }
        }

        [Header("Raycast Settings")]
        [SerializeField, Tooltip(
             "When enabled, raycasts are filtered by the soft mask alpha. Only pixels above the threshold will be considered clickable.")]
        private bool m_RaycastTarget;

        public bool raycastTarget
        {
            get => m_RaycastTarget;
            set => m_RaycastTarget = value;
        }

        [SerializeField, Range(0f, 1f), Tooltip(
             "Minimum alpha value required for a raycast to be valid. Pixels below this threshold will be ignored.")]
        private float m_RaycastThreshold = 0.25f;

        private RenderTexture m_MaskForRenderingRT;
        private Texture2D m_ReadableMask;

        /// <summary>
        /// Return maskable graphic objects.
        /// </summary>
        public List<UISoftMaskWatcher> maskableGraphicObjects { get; private set; } = new();

        private void RegisterMaskableObjectIfNotContain(UISoftMaskWatcher maskableObject)
        {
            if (!maskableGraphicObjects.Contains(maskableObject))
                maskableGraphicObjects.Add(maskableObject);
        }

        private Dictionary<UISoftMaskWatcher, bool> m_MaskableObjectsState = new();

        private List<UISoftMask> m_ParentMasks = new();

        private List<UISoftMask> m_ChildrenMasks = new();

        private RenderMode m_LateCanvasMode;

        private bool m_Idle;

        private bool m_Started;

        #region UNITY_EVENTS

        private void Awake()
        {
            GetParentMasks();
            GetChildrenMask();
            CheckTargetMaterial();
        }

        protected void OnEnable()
        {
            RegisterRendererContext();
            duringBeginContextRendering ??= OnBeginFrameRendering;
            duringCameraPreRender ??= DuringCameraPreRender;
            CheckParentDirty(GetValidParentMask());
        }

        private void OnDisable()
        {
            m_Started = false;

            var parentMask = GetValidParentMask();
            if (parentMask)
            {
                parentMask.GetMaskableObjects();
                parentMask.ComputeFinalMaskForRendering();
            }
            else
            {
                SetMaskableMaterialDirty();
                ComputeChildrenMaskChain();
            }
        }

        private void OnDestroy()
        {
            UnregisterRendererContext();
            CleanupHierarchyLinks();
            CleanupResources();
        }

        private void OnValidate()
        {
            if (!enabled)
                return;

            CheckTargetMaterial();
            ComputeFinalMaskForRendering();
        }

        private void LateUpdate()
        {
            if (!enabled || m_Idle)
                return;

            if (!m_Started)
            {
                CheckMaskData();
                ForceRebuildRectParams(overrideTransform);
                ComputeFinalMaskForRendering();
                UpdateMaterials();
                m_Started = true;
                return;
            }

            CheckTransformParentChange();
            CheckTransformChildrenChange();

            var shouldRecompute = IsRectUvDirty(overrideTransform);
            shouldRecompute |= HasRectMoved(overrideTransform) && maskData.parentMask;
            shouldRecompute |= CheckMaskData();
            if (shouldRecompute)
                ComputeFinalMaskForRendering();

            UpdateMaterials();
        }

        private int m_ParentCount = -1;

        private void CheckTransformParentChange()
        {
            var parentCount = ParentCount(rectTransform);
            if (parentCount == m_ParentCount)
                return;

            m_ParentCount = parentCount;

            var oldParent = GetValidParentMask();
            maskData.parentMask = null;
            GetParentMasks();
            CheckParentDirty(oldParent);
            ComputeFinalMaskForRendering();
        }

        private int m_DescendantCount = -1;

        private void CheckTransformChildrenChange()
        {
            var descendantsCount = DescendantsCount(rectTransform);
            if (descendantsCount == m_DescendantCount)
                return;

            m_DescendantCount = descendantsCount;

            GetChildrenMask();
            GetMaskableObjects();
            ForceUpdateMask();
        }

        private void GetParentMasks()
        {
            m_ParentMasks = new List<UISoftMask>(GetComponentsInParent<UISoftMask>(true));
            m_ParentMasks.Remove(this);

            if (m_Started)
                return;

            // Make sure to re-register children for when adding new mask components
            for (var i = 0; i < m_ParentMasks.Count; i++)
                if (m_ParentMasks[i] is { m_Started: true } parentMask && !parentMask.m_ChildrenMasks.Contains(this))
                    parentMask.GetChildrenMask();
        }

        internal UISoftMask GetValidParentMask()
        {
            for (var i = 0; i < m_ParentMasks.Count; i++)
            {
                var parentMask = m_ParentMasks[i];
                if (parentMask && parentMask.enabled)
                    return maskData.parentMask = parentMask;
            }

            return null;
        }

        private void CheckParentDirty(UISoftMask targetParent)
        {
            if (targetParent)
            {
                maskableGraphicObjects.Clear();
                //Get MaskableObjects
                var maskableObjectsRaw = GetComponentsInChildren<MaskableGraphic>(true);
                for (var i = 0; i < maskableObjectsRaw.Length; i++)
                {
                    var maskableGraphic = maskableObjectsRaw[i];

                    if (IsChildOfAnotherMask(maskableGraphic.transform))
                        continue;

                    var maskableObject = maskableGraphic.gameObject;
                    if (maskableObject.GetComponent<UISoftMaskWatcher>() is not { } watcher)
                    {
                        maskableObject.AddComponent<UISoftMaskWatcher>();
                    }
                    else
                    {
                        watcher.softMask = this;
                        maskableGraphicObjects.Add(watcher);
                        maskableGraphic.SetMaterialDirty();
                    }
                }
            }
            else
                GetMaskableObjects();
        }

        private void GetChildrenMask()
        {
            m_ChildrenMasks = new List<UISoftMask>(GetComponentsInChildren<UISoftMask>(true));
            m_ChildrenMasks.Remove(this);

            if (m_Started)
                return;

            // Make sure to re-register parents for when adding new mask components
            for (var i = 0; i < m_ChildrenMasks.Count; i++)
                if (m_ChildrenMasks[i] is { m_Started: true } childMask && !childMask.m_ParentMasks.Contains(this))
                    childMask.GetParentMasks();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!m_RaycastTarget)
                return true;

            var targetTransform = rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetTransform, screenPoint, eventCamera, out var local);

            var rect = targetTransform.rect;
            var uv = new Vector2(
                (local.x - rect.x) / rect.width,
                (local.y - rect.y) / rect.height);

            //If outside mask bounds lets skip read pixel
            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                return false;

            var previous = RenderTexture.active;
            RenderTexture.active = m_MaskForRenderingRT;

            if (!m_ReadableMask)
                m_ReadableMask = new Texture2D(1, 1);

            var size = (int)maskSize;
            var x = Mathf.Clamp(Mathf.FloorToInt(uv.x * size), 0, size - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(uv.y * size), 0, size - 1);

            m_ReadableMask.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            m_ReadableMask.Apply();

            RenderTexture.active = previous;

            var alpha = m_ReadableMask.GetPixel(0, 0).r;

            if (maskMode == MaskMode.Inverted)
                alpha = Mathf.Clamp(1 - alpha, 0, 1);

            alpha *= opacity;
            alpha = Mathf.SmoothStep(0, fallOff, alpha);

            var hit = alpha > m_RaycastThreshold;

            if (maskData.parentMask is { } parentMask)
                return hit && parentMask.IsRaycastLocationValid(screenPoint, eventCamera);

            return hit;
        }

        #endregion

        #region SOFT_MASK_MATERIAL_EVENTS

        private Material m_SoftMaskBlitMaterial;

        private Material m_TempMaterial;

        public Material GetMaskMaterial() => overrideMaterial ? overrideMaterial : m_TempMaterial;

        internal List<ExternalMaterialData> m_ExternalMaterialsData = new();

        public void RegisterExternalMaterial(Material material)
        {
            if (!material || !MaterialHasSoftMask(material))
                return;

            // TODO: Check impact of have instance external material per mask group
            if (!material || ExternalMaterialData.FindData(m_ExternalMaterialsData, material, out _))
                return;

            var mewInstance = new Material(material)
            {
                name = $"{k_SoftMaskMatTag}{GetInstanceID()}:{material.name}",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor |
                            HideFlags.NotEditable
            };
            var newData = new ExternalMaterialData(material, mewInstance);
            m_ExternalMaterialsData.Add(newData);
        }

        public void UnregisterExternalMaterial(UISoftMaskWatcher watcher, Material material)
        {
            if (!material)
                return;

            maskableGraphicObjects.Remove(watcher);

            for (var i = 0; i < maskableGraphicObjects.Count; i++)
            {
                if (maskableGraphicObjects[i].maskableObject is { } maskableGraphic &&
                    maskableGraphic.material == material)
                    return;
            }

            if (!ExternalMaterialData.FindData(m_ExternalMaterialsData, material, out var foundData))
                return;

            if (foundData.instanceMaterial is { } instanceMat)
                SafeDestroyMaterial(instanceMat);

            m_ExternalMaterialsData.Remove(foundData);
        }

        internal List<FontMaterialData> TMPFontMaterialData { get; private set; } = new();

        internal void UpdateMaterials()
        {
            UpdateMaterial(m_TempMaterial);
            UpdateExternalMaterials();
            UpdateFontMaterials();
        }

        private void CheckTargetMaterial()
        {
            switch (m_OverrideMaskMaterial && MaterialHasSoftMask(m_OverrideMaskMaterial))
            {
                case true when m_TempMaterial != m_OverrideMaskMaterial:
                    m_LateOverrideMaskMaterial = m_TempMaterial = m_OverrideMaskMaterial;
                    SetMaskableMaterialDirty();
                    return;
                case false when !m_TempMaterial:
                case false when m_TempMaterial && m_TempMaterial == m_LateOverrideMaskMaterial:
                    m_LateOverrideMaskMaterial = null;
                    m_TempMaterial = new Material(s_SoftMaskShader)
                    {
                        hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.NotEditable
                    };
                    SetMaskableMaterialDirty();
                    m_TempMaterial.name = $"{k_SoftMaskMatTag}{m_TempMaterial.GetInstanceID()}";
                    break;
            }
        }

        private void UpdateExternalMaterials()
        {
            for (var i = 0; i < m_ExternalMaterialsData.Count; i++)
            {
                if (m_ExternalMaterialsData[i] is not
                    { instanceMaterial: { } instanceMaterial, keyMaterial: { } keyMaterial } externalMaterialData)
                    continue;

                if (keyMaterial.shader != instanceMaterial.shader)
                    instanceMaterial.shader = keyMaterial.shader;

                instanceMaterial.CopyPropertiesFromMaterial(keyMaterial);

                UpdateMaterial(externalMaterialData.instanceMaterial);
            }
        }

        private void UpdateFontMaterials()
        {
            for (var i = 0; i < TMPFontMaterialData.Count; i++)
            {
                if (TMPFontMaterialData[i] is not { } fontData)
                    continue;

                foreach (var pair in fontData.Instances)
                {
                    if (pair.Value is not { } instanceFontMaterial)
                        continue;

                    if (fontData.GetRelativeKeyMaterial(pair.Key) is { } fontMaterial)
                    {
                        if (fontMaterial.shader != instanceFontMaterial.shader)
                            instanceFontMaterial.shader = fontMaterial.shader;

                        instanceFontMaterial.CopyPropertiesFromMaterial(fontMaterial);
                        instanceFontMaterial.EnableKeyword(k_USING_SOFT_MASK);
                    }

                    UpdateMaterial(instanceFontMaterial);
                }
            }
        }

        private void UpdateMaterial(Material material)
        {
            if (!material)
                return;

            material.SetTexture(s_SoftMaskID, m_MaskForRenderingRT);
            material.SetVector(s_MaskDataSettingsID, maskData.settings);

            if (enabled)
                material.EnableKeyword(k_USING_SOFT_MASK);
            else
                material.DisableKeyword(k_USING_SOFT_MASK);

            SetMaterialRectParams(material);
#if UNITY_EDITOR
            if (maskData.preview)
                material.EnableKeyword(k_DEBUG_MASK);
            else
                material.DisableKeyword(k_DEBUG_MASK);
#endif
        }

        internal Material GetInstanceExternalMaterial(Material keyMaterial)
        {
            if (!keyMaterial)
                return null;

            return !ExternalMaterialData.FindData(m_ExternalMaterialsData, keyMaterial, out var data)
                ? null
                : data.instanceMaterial;
        }

        internal Material GetTMPInstanceMaskMaterial(TMP_Text textMesh)
        {
            if (!textMesh ||
                textMesh.fontSharedMaterial is not { } fontSharedMaterial)
                return null;

            var fontAsset = textMesh.font;
            if (FontMaterialData.FindData(TMPFontMaterialData, fontAsset, out var softMaskFontData))
                return softMaskFontData?.TryRegisterInstanceMaterial(fontSharedMaterial);

            if (!MaterialHasSoftMask(fontSharedMaterial))
                return null;

            var newFontData = new FontMaterialData(fontAsset)
            {
                maskID = GetInstanceID()
            };
            TMPFontMaterialData.Add(newFontData);
            return newFontData.TryRegisterInstanceMaterial(fontSharedMaterial);
        }

        internal void SafeDestroyMaterial(Material target)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
#else
            Destroy(target);
#endif
        }

        #endregion

        #region SOFT_MASK_RENDERING_EVENTS

        private void OnBeginFrameRendering(List<Camera> cameras)
        {
            if (canvas && (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                           (canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                            !canvas.worldCamera)))
            {
                SetOverlayCanvasMaterials();
#if UNITY_EDITOR
                for (var i = 0; i < cameras.Count; i++)
                {
                    var cam = cameras[i];
                    if (cam && cam.cameraType != CameraType.Game)
                        SetWorldCanvasMaterials();
                }
#endif
            }
            else
                SetWorldCanvasMaterials();
        }

        private void DuringCameraPreRender(Camera targetCamera)
        {
            if (canvas && (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                           (canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                            !canvas.worldCamera)))
            {
                SetOverlayCanvasMaterials();
#if UNITY_EDITOR
                if (targetCamera.cameraType == CameraType.SceneView)
                    SetWorldCanvasMaterials();
#endif
            }
            else
                SetWorldCanvasMaterials();
        }

        private void SetWorldCanvasMaterials() => SetWorldCanvasProperty(1);

        private void SetOverlayCanvasMaterials() => SetWorldCanvasProperty(0);

        private void SetWorldCanvasProperty(int value)
        {
            if (m_TempMaterial)
                m_TempMaterial.SetInt(s_WORLDCANVAS, value);

            for (var i = 0; i < m_ExternalMaterialsData.Count; i++)
                if (m_ExternalMaterialsData[i].instanceMaterial is { } externalMat)
                    externalMat.SetInt(s_WORLDCANVAS, value);

            for (var i = 0; i < TMPFontMaterialData.Count; i++)
            {
                if (TMPFontMaterialData[i] is not { Instances: { Count: > 0 } instances })
                    continue;

                foreach (var pair in instances)
                    pair.Value?.SetInt(s_WORLDCANVAS, value);
            }
        }

        #endregion

        #region SOFT_MASK_EVENTS

        /// <summary>
        /// Force update mask setup.
        /// </summary>
        [ContextMenu("Force Update Mask")]
        public void ForceUpdateMask()
        {
            CheckMaskData();
            ForceRebuildRectParams(overrideTransform);
            ComputeFinalMaskForRendering();
        }

        private bool CheckMaskData()
        {
            var update = false;

            maskData.settings.z = rectProperties.gamma2linear ? 1 : 0;
#if UNITY_EDITOR
            maskData.preview = maskPreview;
#endif

            if (!Mathf.Approximately(maskData.fallOff, m_FallOff))
            {
                maskData.fallOff = m_FallOff;
                update = true;
            }

            var mode = (int)m_MaskMode;
            if (!Mathf.Approximately(maskData.settings.x, mode))
            {
                maskData.settings.x = mode;
                update = true;
            }

            if (!Mathf.Approximately(maskData.settings.y, m_Opacity))
            {
                maskData.settings.y = m_Opacity;
                update = true;
            }

            var bleedfactor = allowBleed ? 1 : 0;
            if (!Mathf.Approximately(maskData.settings.w, bleedfactor))
            {
                maskData.settings.w = bleedfactor;
                update = true;
            }

            switch (m_MaskUV)
            {
                case MaskUV.Simple:
                    if (maskData.uvType != m_MaskUV)
                    {
                        maskData.uvType = m_MaskUV;
                        update = true;
                    }

                    break;
                case MaskUV.Sliced:
                    if (maskData.uvType != m_MaskUV ||
                        m_Mask && (!maskData.sprite || maskData.sprite != m_Mask) ||
                        !Mathf.Approximately(maskData.pixelsPerUnitMultiplier, m_PixelsPerUnitMultiplier))
                    {
                        maskData.uvType = m_MaskUV;

                        var sprite = maskData.sprite = m_Mask;
                        var size = Vector2.one / sprite.rect.size;
                        var borders = sprite.border;
                        borders.x *= size.x;
                        borders.y *= size.y;
                        borders.z *= size.x;
                        borders.w *= size.y;

                        maskData.slicedBorder = borders;
                        maskData.pixelsPerUnitMultiplier = m_PixelsPerUnitMultiplier;

                        update = true;
                    }

                    break;
            }

            return update;
        }

        private Vector2 GetSliceScale(Vector2 textureSize) =>
            rectTransform.rect.size / textureSize * maskData.pixelsPerUnitMultiplier;

        private void CheckRenderingMaskSetup()
        {
            m_SoftMaskBlitMaterial ??= defaultSoftMaskBlitMaterial;

            var selectedSize = (int)m_MaskSize;

            if (!m_MaskForRenderingRT)
            {
                m_MaskForRenderingRT =
                    RenderTexture.GetTemporary(selectedSize, selectedSize, 0,
                        RenderTextureFormat.RG32); //R8 is unsupported for some platforms
                m_MaskForRenderingRT.name = $"{k_SoftMaskMatTag}{m_MaskForRenderingRT.GetInstanceID()}";
                // m_MaskForRenderingRT.Release();
                m_MaskForRenderingRT.autoGenerateMips = false;
                m_MaskForRenderingRT.useMipMap = false;
            }
            else if (m_MaskForRenderingRT && m_MaskForRenderingRT.width != selectedSize)
            {
                m_MaskForRenderingRT.Release();
                m_MaskForRenderingRT.height = m_MaskForRenderingRT.width = selectedSize;
            }
        }

        private void GetMaskableObjects()
        {
            var lateMaskableObjects = maskableGraphicObjects.ToList();

            maskableGraphicObjects.Clear();

            //Get MaskableObjects
            var maskableObjectsRaw = GetComponentsInChildren<MaskableGraphic>(true);
            for (var i = 0; i < maskableObjectsRaw.Length; i++)
            {
                var maskableGraphic = maskableObjectsRaw[i];

                if (IsChildOfAnotherMask(maskableGraphic.transform))
                    continue;

                var maskableObject = maskableGraphic.gameObject;
                if (maskableObject.GetComponent<UISoftMaskWatcher>() is { } watcher)
                {
                    watcher.softMask = this;
                    maskableGraphicObjects.Add(watcher);
                    maskableGraphic.SetMaterialDirty();
                    lateMaskableObjects.Remove(watcher);
                }
                else
                {
                    maskableObject.AddComponent<UISoftMaskWatcher>();
                }
            }

            // Make sure to re-register/destroy late remaining watchers
            for (var i = 0; i < lateMaskableObjects.Count; i++)
            {
                var maskableObject = lateMaskableObjects[i];
                if (maskableObject)
                {
                    var newMask = maskableObject.softMask = FindFirstValidSoftMask(maskableObject.transform);
                    if (newMask)
                    {
                        newMask.RegisterMaskableObjectIfNotContain(maskableObject);
                        maskableObject.maskableObject.SetMaterialDirty();
                    }
                    else
                    {
                        var graphic = maskableObject.maskableObject;
                        maskableObject.SafeDestroy();
                        graphic?.SetMaterialDirty();
                    }
                }
            }
        }

        private bool IsChildOfAnotherMask(Transform childTransform)
        {
            for (var i = 0; i < m_ChildrenMasks.Count; i++)
            {
                if (m_ChildrenMasks[i] is { enabled: true } childMask &&
                    childTransform.IsChildOf(childMask.transform))
                    return true;
            }

            return false;
        }

        private void ComputeFinalMaskForRendering()
        {
            CheckRenderingMaskSetup();

            // Check parent tex before final blit
            if (GetValidParentMask() is { } parentMask)
                GetParentMaskData(parentMask);
            else
            {
                m_SoftMaskBlitMaterial.SetMatrix(s_ParentMaskMatrixID, Matrix4x4.identity);
                m_SoftMaskBlitMaterial.SetTexture(s_ParentMaskID, Texture2D.whiteTexture);
                m_SoftMaskBlitMaterial.SetVector(s_ParentMaskScaleID, new Vector2(1, 1));
                m_SoftMaskBlitMaterial.SetVector(s_ParentMaskDataID, new Vector4(0, 1, 0));
            }

            m_SoftMaskBlitMaterial.SetFloat(s_FalloffID, m_FallOff);

            var textureMask = Texture2D.whiteTexture;
            if (m_Mask)
            {
                var sourceTexRect = m_Mask.rect;
                var texRect = m_Mask.textureRect;
                var rectOffset = m_Mask.textureRectOffset;
                textureMask = m_Mask.texture;
                var textureAtlasFactor = new Vector2(1f / textureMask.width, 1f / textureMask.height);
                var spriteOffset = (texRect.min - rectOffset) * textureAtlasFactor;
                var size = sourceTexRect.size * textureAtlasFactor;
                var atlasData = new Vector4(size.x, size.y, spriteOffset.x, spriteOffset.y);
                m_SoftMaskBlitMaterial.SetVector(s_AtlasDataID, atlasData);

                if (maskUV == MaskUV.Sliced)
                {
                    m_SoftMaskBlitMaterial.EnableKeyword(k_SLICED);
                    m_SoftMaskBlitMaterial.SetVector(s_SliceScaleID, GetSliceScale(sourceTexRect.size));
                    m_SoftMaskBlitMaterial.SetVector(s_SliceBorderID, maskData.slicedBorder);
                }
                else
                    m_SoftMaskBlitMaterial.DisableKeyword(k_SLICED);
            }

            Graphics.Blit(textureMask, m_MaskForRenderingRT, m_SoftMaskBlitMaterial);

            ComputeChildrenMaskChain();
        }

        private void GetParentMaskData(UISoftMask parentMask)
        {
            if (overrideTransform is not { } childTransform || parentMask.overrideTransform is not { } parentTransform)
                return;

            var parentSizeWorld = parentTransform.rect.size * parentTransform.lossyScale;
            var childSizeWorld = childTransform.rect.size * childTransform.lossyScale;

            var aspectRatio = Mathf.Min(parentSizeWorld.x, parentSizeWorld.y);

            var parentCenterWorld = parentTransform.TransformPoint(parentTransform.rect.center);
            var childCenterWorld = childTransform.TransformPoint(childTransform.rect.center);
            var offsetWorld = childCenterWorld - parentCenterWorld;

            var offsetPlane = new Vector2(
                Vector3.Dot(offsetWorld, parentTransform.right) / aspectRatio,
                Vector3.Dot(offsetWorld, parentTransform.up) / aspectRatio
            );

            var scalePlane = new Vector2(
                childSizeWorld.x / aspectRatio,
                childSizeWorld.y / aspectRatio
            );

            var rotationDelta = Quaternion.Inverse(parentTransform.rotation) * childTransform.rotation;
            var finalMatrix = Matrix4x4.TRS(offsetPlane, rotationDelta, scalePlane);

            m_SoftMaskBlitMaterial.SetMatrix(s_ParentMaskMatrixID, finalMatrix);
            m_SoftMaskBlitMaterial.SetTexture(s_ParentMaskID, parentMask.m_MaskForRenderingRT);

            var parentMaskScale = new Vector2(
                aspectRatio / parentSizeWorld.x,
                aspectRatio / parentSizeWorld.y
            );
            m_SoftMaskBlitMaterial.SetVector(s_ParentMaskScaleID, parentMaskScale);

            var parentMaskData = new Vector3(
                (int)parentMask.maskMode,
                parentMask.opacity,
                parentMask.allowBleed ? 1 : 0
            );
            m_SoftMaskBlitMaterial.SetVector(s_ParentMaskDataID, parentMaskData);
        }

        private void ComputeChildrenMaskChain()
        {
            for (var i = 0; i < m_ChildrenMasks.Count; i++)
            {
                if (m_ChildrenMasks[i] is { enabled: true } childMask)
                    childMask.ComputeFinalMaskForRendering();
            }
        }

        private static void SafeReleaseTempRT(ref RenderTexture renderTexture)
        {
            if (!renderTexture)
                return;

            if (RenderTexture.active == renderTexture)
                RenderTexture.active = null;

            RenderTexture.ReleaseTemporary(renderTexture);
            renderTexture = null;
        }

        internal void SetMaskToActive(bool active)
        {
            if (!active && !m_Idle)
                m_MaskableObjectsState =
                    maskableGraphicObjects.ToDictionary(g => g, g => g.gameObject.activeInHierarchy);

            var changed = false;
            if (m_Idle != !active)
            {
                m_Idle = !active;
                if (m_Idle)
                    SafeReleaseTempRT(ref m_MaskForRenderingRT);

                changed = true;
            }

            if (!changed)
                return;

            var graphicObjects = m_MaskableObjectsState.Keys.ToArray();
            for (var i = 0; i < m_MaskableObjectsState.Count; i++)
            {
                var graphicObj = graphicObjects[i];
                m_MaskableObjectsState.TryGetValue(graphicObj, out var state);
                graphicObj.gameObject.SetActive(active && state);
            }

            enabled = active;

            if (!m_Idle)
                m_MaskableObjectsState.Clear();
        }

        private void SetMaskableMaterialDirty()
        {
            for (var i = 0; i < maskableGraphicObjects.Count; i++)
            {
                var watcher = maskableGraphicObjects[i];
                if (watcher)
                    watcher.maskableObject?.SetMaterialDirty();
            }
        }


        private void CleanupHierarchyLinks()
        {
            //Destroy watchers if none parent mask
            if (!GetValidParentMask())
            {
                for (var i = 0; i < maskableGraphicObjects.Count; i++)
                {
                    if (maskableGraphicObjects[i] is { } watcher)
                        watcher.SafeDestroy();
                }
            }
            else
            {
                var inheritedMaskable = false;
                // Make sure to unregister it from parents childrenMasks
                for (var i = 0; i < m_ParentMasks.Count; i++)
                {
                    if (m_ParentMasks[i] is not { } parentMask)
                        continue;

                    parentMask.m_ChildrenMasks.Remove(this);

                    //Mak sure to inherite chidlren graphics if a mask is enabled
                    // if (!inheritedChildrenGraphics && parentMask.enabled)
                    if (!inheritedMaskable)
                    {
                        inheritedMaskable = true;
                        parentMask.GetMaskableObjects();
                    }
                }
            }

            // Make sure to unregister it from children parentMasks
            for (var i = 0; i < m_ChildrenMasks.Count; i++)
            {
                if (m_ChildrenMasks[i] is not { } childMask)
                    continue;

                childMask.m_ParentMasks.Remove(this);
                childMask.ComputeFinalMaskForRendering();
            }
        }

        private void CleanupResources()
        {
            SafeReleaseTempRT(ref m_MaskForRenderingRT);

            //Destroy if temp material
            if (m_TempMaterial && m_TempMaterial.name.Contains(k_SoftMaskMatTag))
                SafeDestroyMaterial(m_TempMaterial);

            // Make sure to destroy TMP instance materials
            for (var i = 0; i < TMPFontMaterialData.Count; i++)
            {
                var instances = TMPFontMaterialData[i].Instances;
                foreach (var instancePair in instances)
                {
                    if (instancePair.Value is not { } instanceMaterial)
                        continue;
                    SafeDestroyMaterial(instanceMaterial);
                }
            }

            // Make sure to destroy external instance materials
            for (var i = 0; i < m_ExternalMaterialsData.Count; i++)
            {
                if (m_ExternalMaterialsData[i]?.instanceMaterial is not { } externalInstanceMaterial)
                    continue;
                SafeDestroyMaterial(externalInstanceMaterial);
            }

            maskableGraphicObjects.Clear();
            TMPFontMaterialData.Clear();
            m_ExternalMaterialsData.Clear();
        }

        #endregion
    }
}