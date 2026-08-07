using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AMS.UI.SoftMask
{
    using static UISoftMaskUtils;

    [ExecuteAlways,
     RequireComponent(typeof(MaskableGraphic)), DisallowMultipleComponent]
    public class UISoftMaskWatcher : MonoBehaviour, IMaterialModifier
    {
        private UISoftMask m_SoftMask;

        internal UISoftMask softMask
        {
            get => m_SoftMask ? m_SoftMask : null;
            set
            {
                if (m_SoftMask && m_SoftMask != value)
                {
                    if (m_ExternalMaterial)
                        m_SoftMask.UnregisterExternalMaterial(this, m_ExternalMaterial);
                    else
                        m_SoftMask.maskableGraphicObjects.Remove(this);
                }

                m_SoftMask = value;
            }
        }

        internal MaskableGraphic maskableObject { get; private set; }

        internal TextMeshProUGUI tmpText { get; private set; }

        internal Material renderingMaterial { get; private set; }

        private Material m_BaseMaterial;

        private Material m_ExternalMaterial;

        internal Material externalMaterial => m_ExternalMaterial;

        private Shader m_LateExternalMaterialShader;

        private UnityAction m_ModifyTMPMeshHandler;

        private bool? m_LateMaskableState;

        private void Awake()
        {
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            if (!maskableObject)
                maskableObject = GetComponent<MaskableGraphic>();

            if (!tmpText && maskableObject is TextMeshProUGUI tmp)
                tmpText = tmp;

            if (!m_SoftMask && FindFirstValidSoftMask(transform) is { } foundMask)
            {
                m_SoftMask = foundMask;
                m_SoftMask.maskableGraphicObjects.Add(this);
                maskableObject.SetMaterialDirty();
            }
        }

        private void OnDisable()
        {
            if (gameObject.activeInHierarchy && !enabled)
            {
                enabled = true;
                return;
            }

            if (m_ExternalMaterial)
                m_SoftMask?.UnregisterExternalMaterial(this, m_ExternalMaterial);
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            return renderingMaterial = ResolveMaterial(baseMaterial);
        }

        private Material ResolveMaterial(Material baseMaterial)
        {
            m_BaseMaterial = baseMaterial;

            if (!maskableObject.maskable || !m_SoftMask)
                return m_BaseMaterial;

            var selectedSoftMask = m_SoftMask;

            var parentMask = selectedSoftMask.maskData.parentMask;
            if (!selectedSoftMask.enabled && parentMask && parentMask.enabled)
                selectedSoftMask = parentMask;

            if (!selectedSoftMask || !selectedSoftMask.enabled)
                return m_BaseMaterial;

            Material selectedMaterial = null;
            if (!tmpText)
                CheckExternalMaterial(selectedSoftMask);

            if (tmpText)
            {
                selectedMaterial = selectedSoftMask.GetTMPInstanceMaskMaterial(tmpText);
            }
            else if (m_ExternalMaterial)
            {
                if (MaterialHasSoftMask(m_ExternalMaterial))
                    selectedMaterial = selectedSoftMask.GetInstanceExternalMaterial(m_ExternalMaterial);
            }
            else if (selectedSoftMask.GetMaskMaterial() is { } maskMaterial)
                selectedMaterial = maskMaterial;

            if (!selectedMaterial)
                return m_BaseMaterial;

            if (selectedMaterial.HasFloat(s_StencilID) && m_BaseMaterial)
            {
                selectedMaterial.SetFloat(s_StencilCompID, m_BaseMaterial.GetFloat(s_StencilCompID));
                selectedMaterial.SetFloat(s_StencilID, m_BaseMaterial.GetFloat(s_StencilID));
                selectedMaterial.SetFloat(s_StencilOpID, m_BaseMaterial.GetFloat(s_StencilOpID));
                selectedMaterial.SetFloat(s_StencilReadMaskID, m_BaseMaterial.GetFloat(s_StencilReadMaskID));
                selectedMaterial.SetFloat(s_StencilWriteMaskID, m_BaseMaterial.GetFloat(s_StencilWriteMaskID));
            }

            return selectedMaterial;
        }

        internal void CheckExternalMaterial(UISoftMask selectedMask)
        {
            var usingMaterial = m_BaseMaterial == maskableObject.defaultMaterial
                ? null
                : m_BaseMaterial;

            if (usingMaterial &&
                (!m_LateExternalMaterialShader || m_LateExternalMaterialShader != usingMaterial.shader))
                m_LateExternalMaterialShader = usingMaterial.shader;

            if (!m_ExternalMaterial || m_ExternalMaterial && m_ExternalMaterial != usingMaterial)
            {
                if (m_ExternalMaterial)
                    selectedMask.UnregisterExternalMaterial(this, m_ExternalMaterial);

                m_ExternalMaterial = usingMaterial;
            }

            selectedMask.RegisterExternalMaterial(m_ExternalMaterial);
        }

        internal void SafeDestroy()
        {
            Invoke(nameof(DestroySelf), 0f);
        }

        private void DestroySelf()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this);
#else
            Destroy(this);
#endif
        }
    }
}