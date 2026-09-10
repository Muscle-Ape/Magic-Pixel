#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed partial class MPGuidePreview
{
    [MenuItem("MagicPixel/Guide/Rebuild prefab from game layout")]
    public static void BuildPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YooRes/Prefabs/GameMain/MPGameView.prefab");
        GameObject guide = Instantiate(source);
        guide.name = "MPGuideView";
        try
        {
            foreach (string path in new[] { "View/Head", "View/Loves", "View/Content/Shadow", "View/Content/Animation", "View/Content/Line",
                "View/Btns/HintBtn", "View/Btns/RecoverBtn", "View/Btns/PetSkillBtn" })
            {
                Transform node = guide.transform.Find(path);
                if (node != null) DestroyImmediate(node.gameObject);
            }
            RectTransform view = (RectTransform)guide.transform.Find("View");
            TMP_Text heading = view.Find("Title").GetComponent<TMP_Text>();
            heading.name = "Heading";
            SetRect(heading.rectTransform, 650, 90, 0, 864);
            heading.text = "How to Play";
            heading.fontSize = 64;
            heading.enableAutoSizing = false;
            TMP_Text template = LoadPrefab("MPGameNumberFrameVertical").transform.Find("Number").GetComponent<TMP_Text>();

            Image instruction = NewImage(view, "Instruction", 1008, 250, 0, 674.5f, LoadSprite("game_number_c"));
            instruction.type = Image.Type.Sliced;
            NewText(instruction.transform, "Text", "Welcome to MagicPixel!", template, 930, 210, 0, 0, 40, new Color32(103, 66, 25, 255));
            NewText(view, "Feedback", "", template, 990, 80, 0, -683, 32, Color.white);
            Button next = NewButton(view, "Next", 420, 140, 230, -888, LoadSprite("completed_btn_next"));
            NewText(next.transform, "Text", "Let's Play", template, 350, 110, 0, 5, 44, Color.white);
            Button skip = NewButton(view, "Skip", 180, 85, 435, 864, null);
            skip.GetComponent<Image>().color = Color.clear;
            NewText(skip.transform, "Text", "Skip", template, 150, 70, 0, 0, 36, Color.white);
            RectTransform highlights = NewRect(view.Find("Content"), "Highlights", 0, 0, 0, 0);
            highlights.anchorMin = Vector2.zero;
            highlights.anchorMax = Vector2.one;
            highlights.offsetMin = highlights.offsetMax = Vector2.zero;
            Image hand = NewImage(view, "Hand", 144, 144, 0, 0, LoadSprite("tutorial_hand"));
            hand.preserveAspect = true;
            hand.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            hand.gameObject.SetActive(false);
            guide.transform.Find("View/Content/CompletedFrame").gameObject.SetActive(false);
            // Grid、Vertical、Horizontal、Input、ModeSwitch 的位置、布局和资源引用全部来自游戏页。
            PrefabUtility.SaveAsPrefabAsset(guide, "Assets/YooRes/Prefabs/GameMain/MPGuideView.prefab");
            BuildSettingsEntry();
            AssetDatabase.SaveAssets();
            Debug.Log("MPGuideView 已按游戏页布局生成。");
        }
        finally { DestroyImmediate(guide); }
    }

    private static void BuildSettingsEntry()
    {
        const string path = "Assets/YooRes/Prefabs/Pop/MPSettingPop.prefab";
        // 已有入口时无需重新序列化整个设置页。
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path).transform.Find("View/Window/GuideBtn") != null) return;
        GameObject settings = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform window = settings.transform.Find("View/Window");
            RectTransform replay = (RectTransform)window.Find("ReplayBtn");
            SetRect(replay, 290, 90, -155, -420);
            TMP_Text replayText = replay.GetComponentInChildren<TMP_Text>(true);
            SetRect(replayText.rectTransform, 272, 78, 0, 0);
            replayText.text = "Replay";
            Transform existing = window.Find("GuideBtn");
            RectTransform guide = existing != null ? (RectTransform)existing : Instantiate(replay, window, false);
            guide.name = "GuideBtn";
            guide.gameObject.SetActive(true);
            SetRect(guide, 290, 90, 155, -420);
            TMP_Text label = guide.GetComponentInChildren<TMP_Text>(true);
            label.text = "How to Play";
            SetRect(label.rectTransform, 272, 78, 0, 0);
            label.fontSize = 34;
            TMP_Text number = LoadPrefab("MPGameNumberFrameVertical").transform.Find("Number").GetComponent<TMP_Text>();
            label.font = number.font;
            label.fontSharedMaterial = number.fontSharedMaterial;
            PrefabUtility.SaveAsPrefabAsset(settings, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(settings); }
    }

    private static RectTransform NewRect(Transform parent, string name, float w, float h, float x, float y)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        SetRect(rect, w, h, x, y);
        return rect;
    }

    private static void SetRect(RectTransform rect, float w, float h, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, y);
        rect.localScale = Vector3.one;
    }

    private static Image NewImage(Transform parent, string name, float w, float h, float x, float y, Sprite sprite)
    {
        Image image = NewRect(parent, name, w, h, x, y).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text NewText(Transform parent, string name, string value, TMP_Text template,
        float w, float h, float x, float y, int size, Color color)
    {
        TMP_Text text = Instantiate(template, parent, false);
        text.name = name;
        SetRect(text.rectTransform, w, h, x, y);
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static Button NewButton(Transform parent, string name, float w, float h, float x, float y, Sprite sprite)
    {
        Image image = NewImage(parent, name, w, h, x, y, sprite);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }
}
#endif
