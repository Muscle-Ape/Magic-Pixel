using DG.Tweening;
using HQ.UIManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏通关时需要做的事情
/// </summary>
public partial class MPGameView
{
    /// <summary>
    /// 结算数字提示框渐隐时长。
    /// </summary>
    private const float SETTLEMENT_NUMBER_FADE_DURATION = 0.35f;

    /// <summary>
    /// 结算单个像素格缩放变色时长。
    /// </summary>
    private const float SETTLEMENT_BLOCK_ANIMATION_DURATION = 0.16f;

    /// <summary>
    /// 结算像素动画总推进时长。
    /// </summary>
    private const float SETTLEMENT_BLOCK_TOTAL_DELAY = 0.9f;

    /// <summary>
    /// 更新数据
    /// </summary>
    private void UpdateData()
    {
        m_hasCompleted = true;
        ClearProgressCache();
        RefreshPropButtons();

        if (m_isCustomLevel)
        {
            MPUser.instance.CustomLevelPass(m_blockInfo.ID);
            m_refreshAction?.Invoke();
            return;
        }

        // 1、记录当前已通关关卡。宝箱奖励返回主关卡页面后由用户主动领取。
        MPUser.instance.MainLevelPass(m_blockInfo.ID, m_lovesCount);

        // 2、更新解锁到的关卡位置，解锁新关卡
        if (m_index == MPUser.instance.GetMainLevlPassIndex())
        {
            // 向后遍历，检查是否有通关关卡
            int newIndex = m_index;
            var blockList = MPDataManager.Instance.m_mainLevelModel.blockInfos;
            for (int i = newIndex + 1; i < blockList.Count; i++)
            {
                newIndex++;

                if (!MPUser.instance.MainLevelIsPass(blockList[i].ID))
                {
                    break;
                }
            }

            MPUser.instance.SetMainLevelPassIndex(newIndex);
            MPUser.instance.MainLevelUnlock(blockList[newIndex].ID);
        }

        m_refreshAction?.Invoke();
    }

    /// <summary>
    /// 播放游戏完成后的结算动画
    /// </summary>
    private IEnumerator PlayCompletedAnimation()
    {
        LockCompletedInteraction();

        if (m_input != null)
        {
            m_input.gameObject.SetActive(false);
        }

        FadeNumberFrames();
        FadeSettlementUI();
        FadeCompletedFrame();

        transform.Find("View/Content/Frame").GetComponent<Image>().DOFade(0, SETTLEMENT_NUMBER_FADE_DURATION);

        yield return new WaitForSeconds(SETTLEMENT_NUMBER_FADE_DURATION * 0.5f);

        if (m_pixel == null || m_blocks == null || m_blocks.Count == 0)
        {
            OpenCompletedView();
            yield break;
        }

        Texture2D readableTexture = CreateReadableTexture(m_pixel);
        if (readableTexture == null)
        {
            OpenCompletedView();
            yield break;
        }

        if (m_lineNode != null)
        {
            m_lineNode.gameObject.SetActive(false);
        }

        // 像素渐显动画音效
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundPixelAnimation);

        int diagonalCount = Mathf.Max(1, m_size * 2 - 1);
        float diagonalDelay = Mathf.Clamp(SETTLEMENT_BLOCK_TOTAL_DELAY / diagonalCount, 0.02f, 0.06f);
        for (int diagonal = 0; diagonal < diagonalCount; diagonal++)
        {
            for (int row = 0; row < m_size; row++)
            {
                int column = diagonal - row;
                if (column < 0 || column >= m_size)
                    continue;

                int index = row * m_size + column;
                if (index >= m_blocks.Count)
                    continue;

                Color pixelColor = GetSettlementPixelColor(readableTexture, row, column);
                m_blocks[index].PlaySettlementAnimation(pixelColor, SETTLEMENT_BLOCK_ANIMATION_DURATION);
            }

            if (diagonal < diagonalCount - 1)
            {
                yield return new WaitForSeconds(diagonalDelay);
            }
        }

        yield return new WaitForSeconds(SETTLEMENT_BLOCK_ANIMATION_DURATION);

        // 所有方块完成变色后，仅播放一次单段震动反馈。
        MPVibrationManager.Instance.PlayHeavyImpact();

        Destroy(readableTexture);
        OpenCompletedView();
    }

    /// <summary>
    /// 淡入结算完成图片框。
    /// </summary>
    /// <summary>
    /// 游戏完成瞬间锁定页面交互，避免结算动画播放期间继续点击按钮。
    /// </summary>
    private void LockCompletedInteraction()
    {
        if (m_viewCanvasGroup == null)
            return;

        m_viewCanvasGroup.interactable = false;
        m_viewCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 淡出结算期间不再需要展示的标题、生命值、模式切换和道具区域。
    /// </summary>
    private void FadeSettlementUI()
    {
        FadeGraphics(m_titleText.rectTransform, SETTLEMENT_NUMBER_FADE_DURATION);
        FadeGraphics(m_lovesNode, SETTLEMENT_NUMBER_FADE_DURATION);

        if (m_modeSwitchFrame != null)
        {
            FadeGraphics(m_modeSwitchFrame.transform as RectTransform, SETTLEMENT_NUMBER_FADE_DURATION);
        }

        FadeGraphics(m_props, SETTLEMENT_NUMBER_FADE_DURATION);
    }

    /// <summary>
    /// 淡出指定节点下所有UGUI图形元素。
    /// </summary>
    /// <param name="root">需要淡出的节点。</param>
    /// <param name="duration">淡出时长。</param>
    private void FadeGraphics(RectTransform root, float duration)
    {
        if (root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
                continue;

            graphics[i].DOKill();
            graphics[i].DOFade(0f, duration).SetEase(Ease.Linear).SetLink(graphics[i].gameObject);
        }
    }

    private void FadeCompletedFrame()
    {
        if (m_completedFrame == null)
            return;

        m_completedFrame.DOKill();
        Color color = m_completedFrame.color;
        color.a = 0;
        m_completedFrame.color = color;
        m_completedFrame.DOFade(1f, SETTLEMENT_NUMBER_FADE_DURATION).SetEase(Ease.Linear).SetLink(m_completedFrame.gameObject);
    }

    /// <summary>
    /// 打开主关卡结算界面并关闭当前游戏界面。
    /// </summary>
    private void OpenCompletedView()
    {
        RectTransform completedFrameTransform = m_completedFrame == null ? null : m_completedFrame.transform as RectTransform;
        Canvas completedFrameCanvas = completedFrameTransform == null ? null : completedFrameTransform.GetComponentInParent<Canvas>();
        Camera completedFrameCamera = completedFrameCanvas != null && completedFrameCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? completedFrameCanvas.worldCamera : null;
        MPGameCompletedViewUIMsgData data = new MPGameCompletedViewUIMsgData()
        {
            blockInfo = m_blockInfo,
            customLevelInfo = m_customLevelInfo,
            isCustomLevel = m_isCustomLevel,
            index = m_index,
            lovesCount = m_isCustomLevel ? m_loves.Count : m_lovesCount,
            pictureStartAnchoredPosition = completedFrameTransform == null ? Vector2.zero : completedFrameTransform.anchoredPosition,
            pictureStartScreenPosition = completedFrameTransform == null ? Vector2.zero : RectTransformUtility.WorldToScreenPoint(completedFrameCamera, completedFrameTransform.position),
            hasPictureStartScreenPosition = completedFrameTransform != null,
            refresh = m_refreshAction,
        };

        UIManager.Inst.ShowWindow<MPGameCompletedView>(data);
        DestroyWindow();
    }

    /// <summary>
    /// 淡出上方和左侧的数字提示框。
    /// </summary>
    private void FadeNumberFrames()
    {
        if (m_numberHorizontalList != null)
        {
            for (int i = 0; i < m_numberHorizontalList.Count; i++)
            {
                m_numberHorizontalList[i].FadeOut(SETTLEMENT_NUMBER_FADE_DURATION);
            }
        }

        if (m_numberVerticalList != null)
        {
            for (int i = 0; i < m_numberVerticalList.Count; i++)
            {
                m_numberVerticalList[i].FadeOut(SETTLEMENT_NUMBER_FADE_DURATION);
            }
        }
    }

    /// <summary>
    /// 获取结算像素图中指定网格对应的颜色。
    /// </summary>
    /// <param name="texture">可读取的像素图。</param>
    /// <param name="row">从上到下的行下标。</param>
    /// <param name="column">从左到右的列下标。</param>
    /// <returns>网格对应像素颜色。</returns>
    private Color GetSettlementPixelColor(Texture2D texture, int row, int column)
    {
        int x = Mathf.Clamp(column * texture.width / m_size, 0, texture.width - 1);
        int y = Mathf.Clamp(texture.height - 1 - row * texture.height / m_size, 0, texture.height - 1);
        Color color = texture.GetPixel(x, y);
        color = Color.Lerp(Color.white, new Color(color.r, color.g, color.b, 1), color.a);
        color.a = 1;
        return color;
    }

    /// <summary>
    /// 将不可读的资源纹理复制成临时可读取纹理，避免修改资源导入设置。
    /// </summary>
    /// <param name="source">资源原始纹理。</param>
    /// <returns>临时可读取纹理。</returns>
    private Texture2D CreateReadableTexture(Texture2D source)
    {
        if (source == null)
            return null;

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Texture2D readableTexture = null;

        try
        {
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;

            readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readableTexture.Apply(false, false);
            return readableTexture;
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    /// <summary>
    /// 释放自定义关卡结算时从本地读取出来的运行时像素图。
    /// </summary>
    private void ReleaseRuntimePixelTexture()
    {
        if (!m_isRuntimePixelTexture || m_pixel == null)
            return;

        Destroy(m_pixel);
        m_pixel = null;
        m_isRuntimePixelTexture = false;
    }
}
