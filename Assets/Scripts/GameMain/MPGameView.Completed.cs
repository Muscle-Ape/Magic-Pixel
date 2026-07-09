using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        // 1、记录当前已通关关卡
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
        if (m_input != null)
        {
            m_input.gameObject.SetActive(false);
        }

        FadeNumberFrames();

        yield return new WaitForSeconds(SETTLEMENT_NUMBER_FADE_DURATION * 0.5f);

        if (m_pixel == null || m_blocks == null || m_blocks.Count == 0)
        {
            yield break;
        }

        Texture2D readableTexture = CreateReadableTexture(m_pixel);
        if (readableTexture == null)
        {
            yield break;
        }

        if (m_lineNode != null)
        {
            m_lineNode.gameObject.SetActive(false);
        }

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

            yield return new WaitForSeconds(diagonalDelay);
        }

        yield return new WaitForSeconds(SETTLEMENT_BLOCK_ANIMATION_DURATION);

        Destroy(readableTexture);
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
}
