using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MPGuideView
{
    private void BuildBoard(Func<string, GameObject> loadPrefab, Func<string, Sprite> loadSprite)
    {
        GameObject blockPrefab = loadPrefab("MPGameBlock");
        GameObject rowPrefab = loadPrefab("MPGameNumberFrameVertical");
        GameObject columnPrefab = loadPrefab("MPGameNumberFrameHorizontal");
        Sprite frame = loadSprite("game_block_5");
        Sprite fill = loadSprite("game_block_fill_5");
        Sprite highlight = loadSprite("guide_highlight_frame");
        m_grid.cellSize = Vector2.one * 160f;
        for (int i = 0; i < 25; i++)
        {
            GameObject node = Instantiate(blockPrefab, m_grid.transform, false);
            node.name = "Cell" + i;
            m_blocks[i] = node.GetComponent<MPGameBlock>() ?? node.AddComponent<MPGameBlock>();
            m_blocks[i].Init(MPGuideLesson.IsFill(i), i);
            m_blocks[i].SetFrameSprite(frame);
            m_blocks[i].SetFillSprite(fill);
            m_blocks[i].SetFillColor(m_preview ? MPUser.DefaultGameFillColor : MPUser.instance.gameFillColor);
            if (node.GetComponent<CanvasGroup>() == null) node.AddComponent<CanvasGroup>();
        }
        for (int i = 0; i < 5; i++)
        {
            GameObject row = Instantiate(rowPrefab, m_vertical, false);
            GameObject column = Instantiate(columnPrefab, m_horizontal, false);
            m_rows[i] = row.GetComponent<MPGameNumberFrameVertical>() ?? row.AddComponent<MPGameNumberFrameVertical>();
            m_columns[i] = column.GetComponent<MPGameNumberFrameHorizontal>() ?? column.AddComponent<MPGameNumberFrameHorizontal>();
            m_rows[i].Init(ParseClue(MPGuideLesson.RowHint(i)), new Vector2(55, 70));
            m_columns[i].Init(ParseClue(MPGuideLesson.ColumnHint(i)), new Vector2(55, 70));
            m_rows[i].SetFrameSprite(frame);
            m_columns[i].SetFrameSprite(frame);
        }
        for (int i = 0; i < m_highlights.Length; i++)
        {
            var node = new GameObject("Highlight" + i, typeof(RectTransform), typeof(Image));
            node.layer = m_highlightRoot.gameObject.layer;
            node.transform.SetParent(m_highlightRoot, false);
            Image image = node.GetComponent<Image>();
            image.sprite = highlight;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = 2f;
            image.raycastTarget = false;
            m_highlights[i] = image;
        }
        m_input = m_inputRect.GetComponent<MPGuideInput>() ?? m_inputRect.gameObject.AddComponent<MPGuideInput>();
        m_inputRect.GetComponent<Image>().raycastTarget = true;
        m_completedFrame.gameObject.SetActive(false);
        m_hand.GetComponent<Image>().sprite = loadSprite("tutorial_hand");
        m_hand.pivot = new Vector2(0.17f, 0.85f);
    }

    private static List<int> ParseClue(string text)
    {
        var result = new List<int>();
        foreach (string part in text.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            result.Add(int.Parse(part));
        return result;
    }

    private void RefreshHighlights()
    {
        foreach (Image image in m_highlights) image.gameObject.SetActive(false);
        int count = 0;
        switch (m_lesson.Current)
        {
            case MPGuideLesson.Stage.FullRow: Highlight(ref count, 0, 4); break;
            case MPGuideLesson.Stage.Consecutive: Highlight(ref count, 5, 8); break;
            case MPGuideLesson.Stage.Groups: Highlight(ref count, 10, 11); Highlight(ref count, 13, 14); break;
            case MPGuideLesson.Stage.Order: Highlight(ref count, 15, 15); Highlight(ref count, 17, 19); break;
            case MPGuideLesson.Stage.Columns: Highlight(ref count, 2, 22); break;
            case MPGuideLesson.Stage.Blanks:
                for (int i = 0; i < 25; i++) if (m_lesson.IsTarget(i)) Highlight(ref count, i, i);
                break;
            case MPGuideLesson.Stage.LastSwitch:
            case MPGuideLesson.Stage.LastCell: Highlight(ref count, 22, 22); break;
        }
    }

    private void Highlight(ref int count, int first, int last)
    {
        Image image = m_highlights[count++];
        Vector2 start = m_highlightRoot.InverseTransformPoint(m_blocks[first].transform.position);
        Vector2 end = m_highlightRoot.InverseTransformPoint(m_blocks[last].transform.position);
        image.rectTransform.anchoredPosition = (start + end) * 0.5f;
        image.rectTransform.sizeDelta = new Vector2(Mathf.Abs(end.x - start.x) + 224, Mathf.Abs(end.y - start.y) + 224);
        image.gameObject.SetActive(true);
    }

    private Vector2 HandPoint(int index) => m_hand.parent.InverseTransformPoint(m_blocks[index].transform.position);

    private void StartHandHint()
    {
        StopHandHint();
        if (m_hintDismissed || m_settling || !isActiveAndEnabled) return;
        int target = m_lesson.FirstTarget();
        if (!m_lesson.CanSwitch && target < 0) return;
        m_hand.gameObject.SetActive(true);
        m_handGroup.alpha = 1f;
        m_hand.localScale = Vector3.one;
        Vector2 switchPoint = m_hand.parent.InverseTransformPoint(m_switch.transform.position);
        m_hand.anchoredPosition = m_lesson.CanSwitch ? switchPoint : HandPoint(target);
        if (m_preview) return;
        m_handTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        switch (m_lesson.Current)
        {
            case MPGuideLesson.Stage.FullRow: AppendSwipe(0, 4); break;
            case MPGuideLesson.Stage.Consecutive: AppendSwipe(6, 8); break;
            case MPGuideLesson.Stage.Groups: AppendSwipe(10, 11); AppendSwipe(13, 14); break;
            case MPGuideLesson.Stage.Order: AppendSwipe(15, 15); AppendSwipe(17, 19); break;
            default:
                m_handTween.Append(m_hand.DOScale(0.88f, 0.4f)).Append(m_hand.DOScale(1f, 0.4f));
                break;
        }
        m_handTween.AppendInterval(0.5f).SetLoops(-1, LoopType.Restart);
    }

    private void AppendSwipe(int first, int last)
    {
        Vector2 start = HandPoint(first);
        Vector2 end = HandPoint(last);
        m_handTween.AppendCallback(() =>
        {
            if (m_hand == null) return;
            m_hand.anchoredPosition = start;
            m_hand.localScale = Vector3.one;
        });
        m_handTween.Append(m_handGroup.DOFade(1f, 0.15f));
        m_handTween.Append(m_hand.DOScale(0.9f, 0.15f));
        if (first != last) m_handTween.Append(m_hand.DOAnchorPos(end, 0.85f).SetEase(Ease.InOutSine));
        else m_handTween.AppendInterval(0.2f);
        m_handTween.Append(m_hand.DOScale(1f, 0.15f));
        m_handTween.Append(m_handGroup.DOFade(0f, 0.18f));
    }

    private void StopHandHint()
    {
        m_handTween?.Kill();
        m_handTween = null;
        if (m_hand != null) m_hand.gameObject.SetActive(false);
    }
}
