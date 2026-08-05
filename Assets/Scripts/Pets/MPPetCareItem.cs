using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 宠物食物/玩具列表项。
/// 只绑定 prefab 中已经存在的节点，并根据配置和运行时数据刷新显示。
/// </summary>
public class MPPetCareItem : MonoBehaviour
{
    /// <summary>
    /// 选中时 UI 恢复到 prefab 原始布局的动画时长。
    /// </summary>
    private const float SELECT_LAYOUT_DURATION = 0.3f;

    /// <summary>
    /// 取消选中时 UI 移动到未选中布局的动画时长。
    /// </summary>
    private const float UNSELECT_LAYOUT_DURATION = 0.3f;

    /// <summary>
    /// 未选中时 Name 节点的目标 y 坐标，让内容在隐藏 UseBtn 后更靠中间。
    /// </summary>
    private const float UNSELECTED_NAME_Y = -15f;

    /// <summary>
    /// 未选中时 Add 节点的目标 y 坐标。
    /// </summary>
    private const float UNSELECTED_ADD_Y = -70f;

    /// <summary>
    /// 未选中时 CountFrame 节点的目标 y 坐标。
    /// </summary>
    private const float UNSELECTED_COUNT_Y = -130f;

    /// <summary>
    /// Add 节点，包含 FoodIcon/ToyIcon 和 AddValue。
    /// </summary>
    private RectTransform m_addRect;

    /// <summary>
    /// 道具主图标。食物对应 Add/FoodIcon，玩具对应 Add/ToyIcon；同时兼容 Add/Toycon。
    /// </summary>
    private Image m_icon;

    /// <summary>
    /// 道具名称文本，对应 Name。
    /// </summary>
    private TMP_Text m_nameText;

    /// <summary>
    /// Name 节点的 RectTransform，用于切换选中状态时移动布局。
    /// </summary>
    private RectTransform m_nameRect;

    /// <summary>
    /// 恢复效果文本，对应 Add/AddValue，例如 +10 Health。
    /// </summary>
    private TMP_Text m_restoreText;

    /// <summary>
    /// 道具剩余数量文本，对应 CountFrame/CountText。
    /// </summary>
    private TMP_Text m_countText;

    /// <summary>
    /// 数量显示容器，对应 CountFrame。
    /// </summary>
    private GameObject m_countFrame;

    /// <summary>
    /// CountFrame 的 RectTransform，用于切换选中状态时移动布局。
    /// </summary>
    private RectTransform m_countFrameRect;

    /// <summary>
    /// 选中框节点，对应 Selected。
    /// </summary>
    private GameObject m_selected;

    /// <summary>
    /// 使用按钮根节点，对应 UseBtn。
    /// </summary>
    private GameObject m_useButtonRoot;

    /// <summary>
    /// 使用按钮 RectTransform，用于缩放弹出动画。
    /// </summary>
    private RectTransform m_useButtonRect;

    /// <summary>
    /// 使用按钮组件，对应 UseBtn 上的 Button。
    /// </summary>
    private Button m_useButton;

    /// <summary>
    /// 使用按钮文本，对应 UseBtn/Text。
    /// </summary>
    private TMP_Text m_useButtonText;

    /// <summary>
    /// 未解锁遮罩节点，对应 LockFrame。
    /// </summary>
    private GameObject m_lockMask;

    /// <summary>
    /// 未解锁按钮根节点，对应 LockBtn。
    /// LockBtn/Text 的文案由 prefab 自身维护，代码不再动态修改。
    /// </summary>
    private GameObject m_lockButtonRoot;

    /// <summary>
    /// Item 根节点按钮。已解锁和未解锁状态都允许点击。
    /// </summary>
    private Button m_button;

    /// <summary>
    /// 未解锁状态下的按钮。它可能盖在根按钮上方，所以需要单独注册点击事件。
    /// </summary>
    private Button m_lockButton;

    /// <summary>
    /// Item 点击回调，由 MPPetsView 处理选中或未解锁提示。
    /// </summary>
    private Action<MPPetCareItemConfig> m_onClick;

    /// <summary>
    /// 使用按钮点击回调，由 MPPetsView 执行真正的使用逻辑。
    /// </summary>
    private Action<MPPetCareItemConfig> m_onUseClick;

    /// <summary>
    /// 当前复用格子绑定的配置数据。
    /// </summary>
    private MPPetCareItemConfig m_config;

    private Vector2 m_addOriginalPosition;
    private Vector2 m_nameOriginalPosition;
    private Vector2 m_countOriginalPosition;
    private Vector3 m_useButtonOriginalScale = Vector3.one;
    private bool m_layoutCached;
    private bool m_hasRefreshed;
    private bool m_lastLayoutSelected;
    private bool m_lastUnlocked;
    private Sequence m_layoutSequence;
    private Tween m_useButtonScaleTween;

    /// <summary>
    /// 兼容 prefab 上可能配置的初始化入口。
    /// </summary>
    public void Initialization()
    {
        Initialize(null, null);
    }

    /// <summary>
    /// 初始化节点缓存并绑定点击事件。
    /// </summary>
    public void Initialize(Action<MPPetCareItemConfig> onClick, Action<MPPetCareItemConfig> onUseClick)
    {
        m_onClick = onClick;
        m_onUseClick = onUseClick;

        m_addRect = FindComponent<RectTransform>("Add");
        m_icon = FindComponent<Image>("Add/FoodIcon", "Add/Toycon", "Add/ToyIcon", "FoodIcon", "Toycon", "ToyIcon");
        m_nameText = FindComponent<TMP_Text>("Name");
        m_nameRect = FindComponent<RectTransform>("Name");
        m_restoreText = FindComponent<TMP_Text>("Add/AddValue", "AddValue");
        m_countFrame = FindGameObject("CountFrame");
        m_countFrameRect = FindComponent<RectTransform>("CountFrame");
        m_countText = FindComponent<TMP_Text>("CountFrame/CountText");
        m_selected = FindGameObject("Selected");
        m_useButtonRoot = FindGameObject("UseBtn");
        m_useButtonRect = FindComponent<RectTransform>("UseBtn");
        m_useButton = FindComponent<Button>("UseBtn");
        m_useButtonText = FindComponent<TMP_Text>("UseBtn/Text");
        m_lockMask = FindGameObject("LockFrame");
        m_lockButtonRoot = FindGameObject("LockBtn");
        m_lockButton = FindComponent<Button>("LockBtn");
        m_button = GetComponent<Button>();

        CacheOriginalLayout();

        if (m_button != null)
        {
            m_button.onClick.RemoveListener(OnClick);
            m_button.onClick.AddListener(OnClick);
        }

        if (m_lockButton != null)
        {
            m_lockButton.onClick.RemoveListener(OnClick);
            m_lockButton.onClick.AddListener(OnClick);
        }

        if (m_useButton != null)
        {
            m_useButton.onClick.RemoveListener(OnUseClick);
            m_useButton.onClick.AddListener(OnUseClick);
        }

        SetActive(m_selected, false);
        SetActive(m_useButtonRoot, false);
        SetActive(m_lockMask, false);
        SetActive(m_lockButtonRoot, false);
    }

    /// <summary>
    /// 刷新道具显示内容。
    /// </summary>
    /// <param name="config">道具配置。</param>
    /// <param name="runtimeData">道具运行时数据，包含解锁状态和剩余数量。</param>
    /// <param name="selected">当前道具是否处于选中状态。</param>
    /// <param name="canUse">当前道具是否允许使用，包含数量、解锁和目标值是否已满等判断。</param>
    public void Refresh(MPPetCareItemConfig config, MPPetCareRuntimeData runtimeData, bool selected, bool canUse)
    {
        bool configChanged = m_config == null || config == null || m_config.ID != config.ID;
        if (configChanged)
        {
            MPLoad.ReleaseAll(this);
        }

        m_config = config;

        bool unlocked = runtimeData != null && runtimeData.unlocked;
        bool layoutSelected = unlocked && selected;
        bool layoutChanged = !m_hasRefreshed || configChanged || m_lastLayoutSelected != layoutSelected || m_lastUnlocked != unlocked;
        bool animateLayout = m_hasRefreshed && !configChanged && unlocked;
        bool animateUseButton = animateLayout && layoutSelected;

        SetIcon(config);
        SetActive(m_lockMask, !unlocked);
        SetActive(m_lockButtonRoot, !unlocked);
        SetActive(m_selected, layoutSelected);
        SetActive(m_countFrame, unlocked);
        SetActive(m_nameText, true);
        SetActive(m_restoreText, true);

        if (m_button != null)
        {
            // 未解锁道具也允许点击，后续可在 View 层接入解锁弹窗。
            m_button.interactable = true;
        }

        if (m_nameText != null)
        {
            m_nameText.text = config != null ? config.Name : string.Empty;
        }

        if (m_restoreText != null)
        {
            m_restoreText.text = config != null ? config.RestoreText : string.Empty;
        }

        if (layoutChanged)
        {
            ApplyLayoutState(layoutSelected, animateLayout, animateUseButton);
        }

        if (!unlocked)
        {
            m_hasRefreshed = true;
            m_lastLayoutSelected = layoutSelected;
            m_lastUnlocked = false;
            return;
        }

        int count = runtimeData == null ? 0 : Mathf.Max(0, runtimeData.count);
        if (m_countText != null)
        {
            m_countText.text = $"x{count}";
        }

        if (m_useButton != null)
        {
            // 数量不足或目标状态已满时禁用使用按钮，避免玩家无效消耗。
            m_useButton.interactable = canUse;
        }

        if (m_useButtonText != null)
        {
            m_useButtonText.text = "Use";
        }

        m_hasRefreshed = true;
        m_lastLayoutSelected = layoutSelected;
        m_lastUnlocked = true;
    }

    private void CacheOriginalLayout()
    {
        if (m_layoutCached)
            return;

        if (m_addRect != null)
        {
            m_addOriginalPosition = m_addRect.anchoredPosition;
        }
        if (m_nameRect != null)
        {
            m_nameOriginalPosition = m_nameRect.anchoredPosition;
        }
        if (m_countFrameRect != null)
        {
            m_countOriginalPosition = m_countFrameRect.anchoredPosition;
        }
        if (m_useButtonRect != null)
        {
            m_useButtonOriginalScale = m_useButtonRect.localScale;
        }

        m_layoutCached = true;
    }

    private void ApplyLayoutState(bool selected, bool animate, bool animateUseButton)
    {
        CacheOriginalLayout();
        KillLayoutTween();

        if (!selected)
        {
            SetActive(m_useButtonRoot, false);
            SetUseButtonScale(Vector3.zero);

            Vector2 addTarget = new Vector2(m_addOriginalPosition.x, UNSELECTED_ADD_Y);
            Vector2 nameTarget = new Vector2(m_nameOriginalPosition.x, UNSELECTED_NAME_Y);
            Vector2 countTarget = new Vector2(m_countOriginalPosition.x, UNSELECTED_COUNT_Y);

            if (animate)
            {
                m_layoutSequence = DOTween.Sequence().SetLink(gameObject);
                JoinMove(m_layoutSequence, m_addRect, addTarget, UNSELECT_LAYOUT_DURATION, Ease.OutQuad);
                JoinMove(m_layoutSequence, m_nameRect, nameTarget, UNSELECT_LAYOUT_DURATION, Ease.OutQuad);
                JoinMove(m_layoutSequence, m_countFrameRect, countTarget, UNSELECT_LAYOUT_DURATION, Ease.OutQuad);
            }
            else
            {
                SetAnchoredPosition(m_addRect, addTarget);
                SetAnchoredPosition(m_nameRect, nameTarget);
                SetAnchoredPosition(m_countFrameRect, countTarget);
            }
            return;
        }

        Vector3 useButtonTargetScale = GetUseButtonVisibleScale();
        // UseBtn 上可能挂有 MPButton，MPButton 会在 OnEnable 时缓存当前 scale。
        // 先恢复到可见大小再激活，避免它把 0 记录成按钮的正常缩放。
        SetUseButtonScale(useButtonTargetScale);
        SetActive(m_useButtonRoot, true);
        if (animateUseButton)
        {
            if (m_useButtonRect != null)
            {
                m_useButtonRect.DOKill();
                m_useButtonRect.localScale = Vector3.zero;
            }
            else
            {
                SetUseButtonScale(Vector3.zero);
            }

            m_layoutSequence = DOTween.Sequence().SetLink(gameObject);
            JoinMove(m_layoutSequence, m_addRect, m_addOriginalPosition, SELECT_LAYOUT_DURATION, Ease.OutBack);
            JoinMove(m_layoutSequence, m_nameRect, m_nameOriginalPosition, SELECT_LAYOUT_DURATION, Ease.OutBack);
            JoinMove(m_layoutSequence, m_countFrameRect, m_countOriginalPosition, SELECT_LAYOUT_DURATION, Ease.OutBack);
            if (m_useButtonRect != null)
            {
                m_useButtonScaleTween = m_useButtonRect.DOScale(useButtonTargetScale, SELECT_LAYOUT_DURATION)
                    .SetEase(Ease.OutBack)
                    .SetLink(gameObject);
            }
        }
        else
        {
            SetAnchoredPosition(m_addRect, m_addOriginalPosition);
            SetAnchoredPosition(m_nameRect, m_nameOriginalPosition);
            SetAnchoredPosition(m_countFrameRect, m_countOriginalPosition);
            SetUseButtonScale(useButtonTargetScale);
        }
    }

    private void JoinMove(Sequence sequence, RectTransform target, Vector2 position, float duration, Ease ease)
    {
        if (sequence == null || target == null)
            return;

        sequence.Join(target.DOAnchorPos(position, duration).SetEase(ease));
    }

    private void SetAnchoredPosition(RectTransform target, Vector2 position)
    {
        if (target != null)
        {
            target.anchoredPosition = position;
        }
    }

    private void SetUseButtonScale(Vector3 scale)
    {
        if (m_useButtonRect != null)
        {
            m_useButtonRect.localScale = scale;
        }
    }

    private Vector3 GetUseButtonVisibleScale()
    {
        Vector3 scale = m_useButtonOriginalScale;
        if (Mathf.Approximately(scale.x, 0f))
            scale.x = 1f;
        if (Mathf.Approximately(scale.y, 0f))
            scale.y = 1f;
        if (Mathf.Approximately(scale.z, 0f))
            scale.z = 1f;

        return scale;
    }

    private void KillLayoutTween()
    {
        if (m_layoutSequence != null && m_layoutSequence.IsActive())
        {
            m_layoutSequence.Kill();
        }
        m_layoutSequence = null;

        if (m_useButtonScaleTween != null && m_useButtonScaleTween.IsActive())
        {
            m_useButtonScaleTween.Kill();
        }
        m_useButtonScaleTween = null;

        if (m_addRect != null)
            m_addRect.DOKill();
        if (m_nameRect != null)
            m_nameRect.DOKill();
        if (m_countFrameRect != null)
            m_countFrameRect.DOKill();
        if (m_useButtonRect != null)
            m_useButtonRect.DOKill();
    }

    private void OnDisable()
    {
        KillLayoutTween();
    }

    private void OnDestroy()
    {
        KillLayoutTween();
        MPLoad.ReleaseAll(this);
    }

    /// <summary>
    /// 根据配置加载并设置道具图标。加载失败时保留 prefab 原始占位图。
    /// </summary>
    private void SetIcon(MPPetCareItemConfig config)
    {
        if (m_icon == null || config == null || string.IsNullOrEmpty(config.Icon))
            return;

        try
        {
            Sprite sprite = MPLoad.Load<Sprite>(config.Icon, this);
            if (sprite != null)
            {
                m_icon.sprite = sprite;
                m_icon.SetNativeSize();
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Item 本体点击。已解锁时用于选中，未解锁时预留弹窗入口。
    /// </summary>
    private void OnClick()
    {
        if (m_config == null)
            return;

        m_onClick?.Invoke(m_config);

        // 按钮点击音效
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    /// <summary>
    /// 使用按钮点击。这里只回调给 View，不直接修改存档或宠物状态。
    /// </summary>
    private void OnUseClick()
    {
        if (m_config == null)
            return;

        m_onUseClick?.Invoke(m_config);

        // 按钮点击音效
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    /// <summary>
    /// 按固定路径查找组件。
    /// </summary>
    private T FindComponent<T>(params string[] paths) where T : Component
    {
        Transform target = FindTransform(paths);
        return target == null ? null : target.GetComponent<T>();
    }

    /// <summary>
    /// 按固定路径查找 GameObject。
    /// </summary>
    private GameObject FindGameObject(params string[] paths)
    {
        Transform target = FindTransform(paths);
        return target == null ? null : target.gameObject;
    }

    /// <summary>
    /// 查找 prefab 中已经存在的 Transform。
    /// </summary>
    private Transform FindTransform(params string[] paths)
    {
        if (paths == null)
            return null;

        for (int i = 0; i < paths.Length; i++)
        {
            if (string.IsNullOrEmpty(paths[i]))
                continue;

            Transform target = transform.Find(paths[i]);
            if (target != null)
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>
    /// 设置 GameObject 显隐，避免重复 SetActive。
    /// </summary>
    private void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    /// <summary>
    /// 设置组件所在 GameObject 显隐，便于直接控制文本、图片等组件。
    /// </summary>
    private void SetActive(Component target, bool active)
    {
        if (target != null && target.gameObject.activeSelf != active)
        {
            target.gameObject.SetActive(active);
        }
    }
}
