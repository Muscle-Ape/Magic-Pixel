using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AMS.UI.SoftMask
{
    using static UISoftMaskUtils;

    [ExecuteAlways, RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
    public class RectUV : MonoBehaviour
    {
        private Canvas m_Canvas;

        internal Canvas canvas => m_Canvas
            ? m_Canvas
            : m_Canvas =
                rectTransform && rectTransform.GetComponentsInParent<Canvas>() is { Length: > 0 } canvasArray
                    ? canvasArray.First()
                    : null;

        private RectTransform m_CanvasTransform;

        private RectTransform canvasTransform =>
            m_CanvasTransform ? m_CanvasTransform :
            canvas is { } foundCanvas ? m_CanvasTransform = (RectTransform)foundCanvas.transform : null;

        private RectTransform m_RectTransform;

        public RectTransform rectTransform => m_RectTransform ? m_RectTransform :
            GetComponent<RectTransform>() is
                { } foundRect ? m_RectTransform = foundRect : null;

        private Vector2 m_RectUVSize;

        private Matrix4x4 m_WorldCanvasMatrix = Matrix4x4.identity;
        private Matrix4x4 m_OverlayCanvasMatrix = Matrix4x4.identity;

        internal readonly RectProperties rectProperties = new();

        [Serializable]
        internal class RectProperties
        {
            public Vector3 pos;
            public Vector3 rotation;
            public Vector2 size;
            public Vector2 pivot;
            public Vector3 scale;

            public float scaleFactor = 1;
            public Vector2 displaySize;
            public bool gamma2linear;

            public bool HasMoved(RectTransform rect)
            {
                return pos != rect.position;
            }

            public bool IsDirty(Canvas canvas, RectTransform rect)
            {
                if (canvas)
                {
                    gamma2linear = canvas.vertexColorAlwaysGammaSpace;
                    if (!Mathf.Approximately(canvas.scaleFactor, scaleFactor))
                        scaleFactor = canvas.scaleFactor;

                    if (!Mathf.Approximately(Vector2.Distance(displaySize, canvas.renderingDisplaySize), 0))
                        displaySize = canvas.pixelRect.size;
                }

                var isDirty = pivot != rect.pivot ||
                              rotation != rect.eulerAngles ||
                              scale != rect.lossyScale ||
                              size != rect.sizeDelta;

                if (isDirty)
                    UpdateValues(rect);

                return isDirty;
            }

            internal void UpdateValues(RectTransform rect)
            {
                pivot = rect.pivot;
                rotation = rect.eulerAngles;
                scale = rect.lossyScale;
                size = rect.sizeDelta;
            }
        }


        #region UNITY_RENDERING_EVENTS

        internal delegate void OnBeginContextRendering(List<Camera> cameras);

        internal OnBeginContextRendering duringBeginContextRendering = null;

        internal delegate void DuringPreCullCamera(Camera cam);

        internal DuringPreCullCamera duringCameraPreRender = null;

#if UNITY_2021_3
        private Action RenderPipelineTypeChangedHandler;
#else
        private Action<RenderPipelineAsset, RenderPipelineAsset> RenderPipelineAssetChangedHandler;
#endif
        private Action<ScriptableRenderContext, List<Camera>> BeginContextRenderingHandler;

        private Camera.CameraCallback OnCameraPreRenderHandler;

        private bool? m_UsingSRP;

        internal void RegisterRendererContext()
        {
            CheckRenderContextHandles();
            CheckCurrentRenderPipeline(GraphicsSettings.defaultRenderPipeline);
#if UNITY_2021_3
            RenderPipelineManager.activeRenderPipelineTypeChanged += RenderPipelineTypeChangedHandler;
#else
            RenderPipelineManager.activeRenderPipelineAssetChanged += RenderPipelineAssetChangedHandler;
#endif
        }

        internal void UnregisterRendererContext()
        {
#if UNITY_2021_3
            RenderPipelineManager.activeRenderPipelineTypeChanged -= RenderPipelineTypeChangedHandler;
#else
            RenderPipelineManager.activeRenderPipelineAssetChanged -= RenderPipelineAssetChangedHandler;
#endif
            RenderPipelineManager.beginContextRendering -= BeginContextRenderingHandler;
            Camera.onPreRender -= OnCameraPreRenderHandler;
            m_UsingSRP = null;
        }

        private void CheckRenderContextHandles()
        {
#if UNITY_2021_3
            RenderPipelineTypeChangedHandler ??= RenderPipelineTypeChanged;
#else
            RenderPipelineAssetChangedHandler ??= RenderPipelineAssetChanged;
#endif

            BeginContextRenderingHandler ??= DuringOnBeginContextRendering;
            OnCameraPreRenderHandler ??= DuringOnCameraPreRender;
        }

#if UNITY_2021_3
        private void RenderPipelineTypeChanged()
        {
            CheckCurrentRenderPipeline(RenderPipelineManager.currentPipeline != null);
        }
#else
        private void RenderPipelineAssetChanged(RenderPipelineAsset from, RenderPipelineAsset to)
        {
            CheckCurrentRenderPipeline(to);
        }
#endif

        private void CheckCurrentRenderPipeline(bool hasRenderPipeline)
        {
            if (m_UsingSRP.HasValue && m_UsingSRP == hasRenderPipeline)
                return;

            m_UsingSRP = hasRenderPipeline;

            if (hasRenderPipeline)
            {
                Camera.onPreRender -= OnCameraPreRenderHandler;
                RenderPipelineManager.beginContextRendering += BeginContextRenderingHandler;
            }
            else
            {
                RenderPipelineManager.beginContextRendering -= BeginContextRenderingHandler;
                Camera.onPreRender += OnCameraPreRenderHandler;
            }
        }

        private void DuringOnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            duringBeginContextRendering?.Invoke(cameras);
        }

        private void DuringOnCameraPreRender(Camera cam)
        {
            duringCameraPreRender?.Invoke(cam);
        }

        #endregion

        private void UpdateWorldRectParams(RectTransform targetRectTransform)
        {
            var rect = targetRectTransform.rect;
            var rectSize = rect.size;

            rectProperties.pos = rect.position;

            m_RectUVSize.x = rectSize.x;
            m_RectUVSize.y = rectSize.y;

            m_WorldCanvasMatrix = Matrix4x4.TRS(
                targetRectTransform.TransformPoint(new Vector3(rect.x, rect.y, 0f)),
                targetRectTransform.rotation,
                targetRectTransform.lossyScale).inverse;

            m_OverlayCanvasMatrix = m_WorldCanvasMatrix * canvasTransform.localToWorldMatrix;
        }

        /// <summary>
        /// Return true if rectUV has moved.
        /// </summary>
        /// <param name="overrideTransform">Override transform to decouple RectUV.</param>
        /// <returns></returns>
        protected bool HasRectMoved(RectTransform overrideTransform = null)
        {
            var targetRect = overrideTransform ? overrideTransform : rectTransform;

            if (!rectProperties.HasMoved(targetRect))
                return false;

            UpdateWorldRectParams(targetRect);
            return true;
        }

        /// <summary>
        /// Return true if rectUV is dirty.
        /// </summary>
        /// <param name="overrideTransform">Override transform to decouple RectUV.</param>
        /// <returns></returns>
        protected bool IsRectUvDirty(RectTransform overrideTransform = null)
        {
            var targetRect = overrideTransform ? overrideTransform : rectTransform;

            if (!rectProperties.IsDirty(canvas, targetRect))
                return false;

            UpdateWorldRectParams(targetRect);
            return true;
        }

        /// <summary>
        /// Force rebuild rect parameters
        /// </summary>
        /// <param name="overrideTransform">Override transform to decouple RectUV.</param>
        protected void ForceRebuildRectParams(RectTransform overrideTransform = null)
        {
            var targetRect = overrideTransform ? overrideTransform : rectTransform;
            if(canvas is not {} targetCanvas)
                return;
            rectProperties.gamma2linear = canvas.vertexColorAlwaysGammaSpace;
            rectProperties.scaleFactor = targetCanvas.scaleFactor;
            rectProperties.displaySize = targetCanvas.pixelRect.size;
            rectProperties.UpdateValues(targetRect);
            UpdateWorldRectParams(targetRect);
        }

        /// <summary>
        /// Set material rect params;
        /// </summary>
        /// <param name="material"></param>
        protected void SetMaterialRectParams(Material material)
        {
            material.SetVector(s_RectUvSizeID, m_RectUVSize);
            material.SetMatrix(s_WorldCanvasMatrixID, m_WorldCanvasMatrix);
            material.SetMatrix(s_OverlayCanvasMatrixID, m_OverlayCanvasMatrix);
        }
    }
}