using UnityEngine;
using DG.Tweening;
using HQ.UIManager;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// 游戏通关时需要做的事情
/// </summary>
public partial class MPLargeImageGameView
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

        // 1、记录当前已通关关卡
        MPUser.instance.LargeImageLevelPass(m_blockInfo.ID, m_lovesCount);
        MPUser.instance.TryClaimLargeImageLevelCoinAward(m_blockInfo);

        // 2、更新解锁到的关卡位置，解锁新关卡
        if (m_index == MPUser.instance.GetLargeImageLevlPassIndex())
        {
            // 向后遍历，检查是否有通关关卡
            int newIndex = m_index;
            var blockList = MPDataManager.Instance.m_largeImageModel.blockInfos;
            for (int i = newIndex + 1; i < blockList.Count; i++)
            {
                newIndex++;

                if (!MPUser.instance.LargeImageLevelIsPass(blockList[i].ID))
                {
                    break;
                }
            }

            MPUser.instance.SetLargeImageLevelPassIndex(newIndex);
            MPUser.instance.LargeImageLevelUnlock(blockList[newIndex].ID);
        }

        m_refreshAction?.Invoke();
    }

    /// <summary>
    /// 播放大图模式完成后的结算动画。
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
        FadeContentFrame();

        yield return new WaitForSeconds(SETTLEMENT_NUMBER_FADE_DURATION * 0.5f);

        if (m_pixel == null || m_blockGrid2Array == null)
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

        int diagonalCount = Mathf.Max(1, FIXED_SIZE * 2 - 1);
        float diagonalDelay = Mathf.Clamp(SETTLEMENT_BLOCK_TOTAL_DELAY / diagonalCount, 0.02f, 0.06f);
        for (int diagonal = 0; diagonal < diagonalCount; diagonal++)
        {
            for (int row = 0; row < FIXED_SIZE; row++)
            {
                int column = diagonal - row;
                if (column < 0 || column >= FIXED_SIZE)
                    continue;

                MPLargeImageGameBlock block = m_blockGrid2Array[row][column];
                if (block == null)
                    continue;

                Vector2Int pixelPos = m_blockStatueHead + new Vector2Int(row, column);
                Color pixelColor = GetSettlementPixelColor(readableTexture, pixelPos.x, pixelPos.y);
                block.PlaySettlementAnimation(pixelColor, SETTLEMENT_BLOCK_ANIMATION_DURATION);
            }

            if (diagonal < diagonalCount - 1)
            {
                yield return new WaitForSeconds(diagonalDelay);
            }
        }

        yield return new WaitForSeconds(SETTLEMENT_BLOCK_ANIMATION_DURATION);

        // 当前可视拼图的所有方块完成变色后，仅播放一次单段震动反馈。
        MPVibrationManager.Instance.PlayHeavyImpact();

        Destroy(readableTexture);
        OpenCompletedView();
    }

    /// <summary>
    /// 使用主游戏结算界面展示大图模式结算，并关闭当前游戏界面。
    /// </summary>
    private void OpenCompletedView()
    {
        RectTransform gridTransform = m_blockGrid == null ? null : m_blockGrid.transform as RectTransform;
        Canvas gridCanvas = gridTransform == null ? null : gridTransform.GetComponentInParent<Canvas>();
        Camera gridCamera = gridCanvas != null && gridCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? gridCanvas.worldCamera : null;

        MPGameCompletedViewUIMsgData data = new MPGameCompletedViewUIMsgData()
        {
            largeImageBlockInfo = m_blockInfo,
            isLargeImageLevel = true,
            index = m_index,
            lovesCount = m_lovesCount,
            largeImageViewHead = m_blockStatueHead,
            largeImageSize = m_size,
            largeImageVisibleSize = FIXED_SIZE,
            pictureStartAnchoredPosition = gridTransform == null ? Vector2.zero : gridTransform.anchoredPosition,
            pictureStartScreenPosition = gridTransform == null ? Vector2.zero : RectTransformUtility.WorldToScreenPoint(gridCamera, gridTransform.position),
            hasPictureStartScreenPosition = gridTransform != null,
            refresh = m_refreshAction,
        };

        UIManager.Inst.ShowWindow<MPGameCompletedView>(data);
        DestroyWindow();
    }

    /// <summary>
    /// 游戏完成瞬间锁定页面交互，避免结算动画播放期间继续点击或滑动。
    /// </summary>
    private void LockCompletedInteraction()
    {
        SetNumberFrameInteractable(m_numberHorizontal, false);
        SetNumberFrameInteractable(m_numberVertical, false);

        if (m_backBtn != null)
        {
            m_backBtn.interactable = false;
        }

        if (m_settingBtn != null)
        {
            m_settingBtn.interactable = false;
        }

        if (m_modeSwitchFrame != null)
        {
            m_modeSwitchFrame.interactable = false;
        }

        if (m_hintPropBtn != null)
        {
            m_hintPropBtn.interactable = false;
        }

        if (m_loveRecoverPropBtn != null)
        {
            m_loveRecoverPropBtn.interactable = false;
        }
    }

    /// <summary>
    /// 设置数字栏是否可以接收拖拽事件。
    /// </summary>
    /// <param name="target">数字栏节点。</param>
    /// <param name="interactable">是否允许交互。</param>
    private void SetNumberFrameInteractable(RectTransform target, bool interactable)
    {
        if (target == null)
            return;

        CanvasGroup cg = target.GetComponent<CanvasGroup>();
        if (cg == null)
            return;

        cg.interactable = interactable;
        cg.blocksRaycasts = interactable;
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

    /// <summary>
    /// 淡出游戏内容边框。
    /// </summary>
    private void FadeContentFrame()
    {
        Image frame = m_contentFrame;
        if (frame == null)
        {
            Transform frameTransform = transform.Find("View/Content/Frame");
            frame = frameTransform == null ? null : frameTransform.GetComponent<Image>();
        }

        if (frame == null)
            return;

        frame.DOKill();
        frame.DOFade(0f, SETTLEMENT_NUMBER_FADE_DURATION).SetEase(Ease.Linear).SetLink(frame.gameObject);
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
    /// 获取大图结算像素图中完整网格坐标对应的颜色。
    /// </summary>
    /// <param name="texture">可读取的像素图。</param>
    /// <param name="row">完整大图中从上到下的行下标。</param>
    /// <param name="column">完整大图中从左到右的列下标。</param>
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
}
