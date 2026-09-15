using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 首发临时关闭的功能。保留业务实现和预制体，接入完成后在这里逐项恢复。
/// 开关只控制功能和入口，不删除 SDK，也不修改玩家已有存档。
/// </summary>
public static class MPReleaseFeatures
{
    public static bool Ads => false;
    public static bool Facebook => false;
    public static bool Vip => false;
    public static bool InAppPurchases => false;
    public static bool Shop => false;
    public static bool SignIn => false;
    public static bool ShopPets => false;
    public static bool ThreeD => false;
    public static bool HomeReward => false;

    /// <summary>按稳定 ID 过滤购买宠物，避免解锁排序或配置顺序变化后隐藏错误的宠物。</summary>
    public static bool IsHomePetVisible(string petId)
    {
        return ShopPets || (petId != "pet_poodle_gold" && petId != "pet_fox_royal");
    }

    public static void ApplyHead(Transform head)
    {
        if (Shop && InAppPurchases) return;
        Hide(head, "Coin/Add");
        Hide(head, "Fluorite/Add");
    }

    public static void ApplyHome(Transform root)
    {
        ApplyHead(root.Find("View/Head"));
        Transform widgets = root.Find("View/Center/Home/Widgets");
        if (widgets == null) return;
        if (!Shop || !InAppPurchases) Hide(widgets, "ShopBtn");
        if (!Vip || !InAppPurchases) Hide(widgets, "VIPBtn");
        if (!Ads || !InAppPurchases) Hide(widgets, "NoAdsBtn");
        if (!SignIn) Hide(widgets, "SignInBtn");
        if (!ThreeD) Hide(widgets, "ThreeDBtn");
        if (!HomeReward) Hide(widgets, "RewardBtn");
        // 商店隐藏后，普通倒计时奖励居中，避免原来左右两列空掉一列。
        if (HomeReward && (!Shop || !InAppPurchases))
        {
            RectTransform reward = widgets.Find("RewardBtn") as RectTransform;
            if (reward != null)
                reward.anchoredPosition = new Vector2(0f, reward.anchoredPosition.y);
        }
    }

    /// <summary>仅在窗口 OnCreate 调用一次；缩短 VIP 所占的底部空间。</summary>
    public static void ApplyUnlock(Transform root)
    {
        Transform window = root.Find("View/Window");
        if (!Ads) Hide(window, "AdBtn");
        if (!Vip || !InAppPurchases)
        {
            Hide(window, "VipBtn");
            TrimBottom(window as RectTransform, 179f);
            // 广告按钮在窗口外，未来仅恢复广告时也要跟随缩短后的窗口。
            if (Ads) MoveY(window, "AdBtn", 179f);
        }
    }

    public static void ApplyGameFail(Transform root)
    {
        if (Ads) return;
        Transform window = root.Find("View/Window");
        Hide(window, "ReviveAdBtn");
        // 复活按钮位于背景下方，收回广告行后将底部两个按钮上移一行。
        MoveY(window, "ReplayBtn", 192f);
        MoveY(window, "QuitBtn", 192f);
    }

    private static void Hide(Transform root, string path)
    {
        Transform node = root?.Find(path);
        if (node != null) node.gameObject.SetActive(false);
    }

    public static void ApplyAssetComparison(Transform root)
    {
        int hiddenRows = (Vip && InAppPurchases ? 0 : 1) + (Ads && InAppPurchases ? 0 : 1);
        if (hiddenRows == 0) return;
        RectTransform window = root.Find("View/Window") as RectTransform;
        RectTransform panel = window?.Find("Panel") as RectTransform;
        RectTransform content = panel?.Find("Content") as RectTransform;
        if (content == null || content.childCount == 0) return;
        float removed = content.rect.height / content.childCount * hiddenRows;
        TrimBottom(content, removed);
        TrimBottom(panel, removed);
        MoveY(window, "LocalBtn", removed);
        MoveY(window, "CloudBtn", removed);
        TrimBottom(window, removed);
    }

    private static void MoveY(Transform root, string path, float delta)
    {
        RectTransform node = root?.Find(path) as RectTransform;
        if (node != null) node.anchoredPosition += Vector2.up * delta;
    }

    /// <summary>保持窗口顶部及现有内容位置不变，只裁去底部空白。</summary>
    private static void TrimBottom(RectTransform window, float amount)
    {
        if (window == null || amount <= 0f) return;
        Vector3[] positions = new Vector3[window.childCount];
        // 弹窗入场时 Window 可能处于零缩放，必须保存本地坐标，不能用世界坐标反算。
        for (int i = 0; i < positions.Length; i++) positions[i] = window.GetChild(i).localPosition;
        window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, window.rect.height - amount);
        float offset = amount * (1f - window.pivot.y);
        window.anchoredPosition += Vector2.up * offset;
        for (int i = 0; i < positions.Length; i++) window.GetChild(i).localPosition = positions[i] - Vector3.up * offset;
    }
}
