using System.Collections;
using DG.Tweening;
using HQ.UIManager;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MPGuideView
{
    private static readonly Color32 Green = new Color32(89, 169, 74, 255);
    private static readonly Color32 Leaf = new Color32(112, 189, 73, 255);
    private static readonly Color32 Bark = new Color32(166, 116, 53, 255);
    private static readonly Color32 Apple = new Color32(246, 101, 85, 255);
    private static readonly Color32 Sky = new Color32(208, 244, 249, 255);
    private static readonly Color[] PixelColors =
    {
        Leaf, Green, Leaf, Green, Leaf,
        Green, Apple, Green, Apple, Sky,
        Leaf, Bark, Sky, Bark, Green,
        Green, Sky, Bark, Bark, Leaf,
        Sky, Sky, Bark, Sky, Sky
    };

    private IEnumerator PlayCompletedAnimation()
    {
        m_settling = true;
        StopHandHint();
        m_view.interactable = false;
        m_inputRect.gameObject.SetActive(false);
        m_highlightRoot.gameObject.SetActive(false);
        Fade(m_instruction);
        Fade(m_heading.rectTransform);
        Fade(m_feedback.rectTransform);
        Fade(m_skip.transform as RectTransform);
        Fade(m_next.transform as RectTransform);
        Fade(m_btns);
        Fade(m_floor.rectTransform);
        Fade(m_contentFrame.rectTransform);
        Fade(m_outerFrame.rectTransform);
        foreach (var row in m_rows) m_settlementTweens.Add(row.FadeOut(0.35f));
        foreach (var column in m_columns) m_settlementTweens.Add(column.FadeOut(0.35f));
        m_completedFrame.gameObject.SetActive(true);
        Color frameColor = m_completedFrame.color;
        frameColor.a = 0;
        m_completedFrame.color = frameColor;
        m_settlementTweens.Add(m_completedFrame.DOFade(1, 0.35f).SetLink(gameObject));
        yield return new WaitForSeconds(0.175f);
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundPixelAnimation);
        // 与游戏页保持同样的对角线推进、时长、缩放与变色；直接复用方块实现。
        for (int diagonal = 0; diagonal < 9; diagonal++)
        {
            for (int row = 0; row < 5; row++)
            {
                int column = diagonal - row;
                if (column < 0 || column >= 5) continue;
                int index = row * 5 + column;
                m_settlementTweens.Add(m_blocks[index].PlaySettlementAnimation(PixelColors[index], 0.16f));
            }
            if (diagonal < 8) yield return new WaitForSeconds(0.06f);
        }
        yield return new WaitForSeconds(0.16f);
        MPVibrationManager.Instance.PlayHeavyImpact();
        OpenCompletedView();
    }

    private void Fade(RectTransform root)
    {
        if (root == null) return;
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            m_settlementTweens.Add(graphic.DOFade(0, 0.35f).SetEase(Ease.Linear).SetLink(gameObject));
    }

    private void OpenCompletedView()
    {
        Canvas canvas = m_completedFrame.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var data = new MPGameCompletedViewUIMsgData
        {
            isGuide = true,
            isGuideReplay = m_isReplay,
            guidePixelColors = (Color[])PixelColors.Clone(),
            lovesCount = 3,
            index = -1,
            pictureStartAnchoredPosition = m_completedFrame.rectTransform.anchoredPosition,
            pictureStartScreenPosition = RectTransformUtility.WorldToScreenPoint(camera, m_completedFrame.transform.position),
            hasPictureStartScreenPosition = true
        };
        // 首次启动没有主页历史；在同一帧建立正常返回路径，不先加载第一关。
        if (!m_isReplay) EnsureLevelNavigation();
        MPGameCompletedView completed = UIManager.Inst.ShowWindow<MPGameCompletedView>(data);
        if (completed == null) return;
        SaveFlag(FINISHED_KEY);
        DestroyWindow();
    }

    private static void EnsureLevelNavigation()
    {
        var history = UIManager.Inst.HistoryList;
        if (!history.Exists(window => window is MPHomeView && !window.IsDestoried))
            UIManager.Inst.ShowWindow<MPHomeView>();
        if (!history.Exists(window => window is MPMainLevelView && !window.IsDestoried))
            UIManager.Inst.ShowWindow<MPMainLevelView>();
    }

#if UNITY_EDITOR
    private void ShowCompletedPreview()
    {
        m_highlightRoot.gameObject.SetActive(false);
        m_vertical.gameObject.SetActive(false);
        m_horizontal.gameObject.SetActive(false);
        for (int i = 0; i < 25; i++)
        {
            Transform fill = m_blocks[i].transform.Find("Fill");
            fill.Find("Color").gameObject.SetActive(false);
            fill.gameObject.SetActive(true);
            fill.GetComponent<Image>().color = PixelColors[i];
            m_blocks[i].transform.Find("Blank").gameObject.SetActive(false);
        }
    }
#endif
}
