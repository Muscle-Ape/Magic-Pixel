using System;
using Lofelt.NiceVibrations;
using UnityEngine;

/// <summary>
/// 游戏业务使用的震动类型。
/// 业务层只依赖该枚举，避免直接散落 Nice Vibrations 的插件类型。
/// </summary>
public enum MPVibrationType
{
    Selection,
    LightImpact,
    MediumImpact,
    HeavyImpact,
    RigidImpact,
    SoftImpact,
    Success,
    Warning,
    Failure,
}

/// <summary>
/// Nice Vibrations 统一控制器。
/// 负责读取玩家震动设置、初始化插件、播放预设/自定义震动，以及处理应用生命周期。
/// </summary>
public sealed class MPVibrationManager
{
    private const float DEFAULT_OUTPUT_LEVEL = 1f;

    private static MPVibrationManager m_instance;

    private bool m_initialized;
    private bool m_initializationFailed;
    private bool m_hasLoggedPlaybackError;
    private float m_outputLevel = DEFAULT_OUTPUT_LEVEL;
    private MPVibrationLifecycleHook m_lifecycleHook;

    private MPVibrationManager()
    {
    }

    public static MPVibrationManager Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new MPVibrationManager();
            }

            return m_instance;
        }
    }

    /// <summary>
    /// 玩家当前是否允许震动。
    /// </summary>
    public bool IsEnabled => MPUser.instance.isVibration;

    /// <summary>
    /// 当前系统版本是否达到插件支持的最低要求。
    /// 设备是否支持高级震动由插件在实际播放时自动判断并降级。
    /// </summary>
    public bool IsPlatformSupported => DeviceCapabilities.isVersionSupported;

    /// <summary>
    /// 提前初始化插件，避免第一次播放时产生初始化开销。
    /// 应在 MPUser 初始化及云存档同步完成后调用。
    /// </summary>
    public void Initialize()
    {
        if (m_initializationFailed)
        {
            return;
        }

        if (!m_initialized)
        {
            try
            {
                CreateLifecycleHook();
                HapticController.Init();
                HapticController.outputLevel = m_outputLevel;
                m_initialized = true;
            }
            catch (Exception exception)
            {
                m_initializationFailed = true;
                Debug.LogWarning($"[MPVibrationManager] Nice Vibrations 初始化失败，已停用本次运行的震动：{exception.Message}");
                return;
            }
        }

        ApplyEnabledState(IsEnabled);
    }

    /// <summary>
    /// 修改并保存玩家震动开关，同时同步插件运行状态。
    /// </summary>
    public void SetEnabled(bool isEnabled)
    {
        MPUser.instance.SetVibrationStatus(isEnabled);
        Initialize();
        ApplyEnabledState(isEnabled);
    }

    /// <summary>
    /// 设置全局震动强度，范围为 0~1。本值仅作用于当前运行周期。
    /// </summary>
    public void SetOutputLevel(float outputLevel)
    {
        m_outputLevel = Mathf.Clamp01(outputLevel);
        Initialize();

        if (m_initialized)
        {
            HapticController.outputLevel = m_outputLevel;
        }
    }

    /// <summary>
    /// 播放一个业务预设震动。
    /// </summary>
    public void Play(MPVibrationType vibrationType)
    {
        if (!CanPlay())
        {
            return;
        }

        ExecutePlayback(() => HapticPatterns.PlayPreset(ToPresetType(vibrationType)));
    }

    public void PlaySelection()
    {
        Play(MPVibrationType.Selection);
    }

    public void PlayLightImpact()
    {
        Play(MPVibrationType.LightImpact);
    }

    public void PlayMediumImpact()
    {
        Play(MPVibrationType.MediumImpact);
    }

    public void PlayHeavyImpact()
    {
        Play(MPVibrationType.HeavyImpact);
    }

    public void PlaySuccess()
    {
        Play(MPVibrationType.Success);
    }

    public void PlayWarning()
    {
        Play(MPVibrationType.Warning);
    }

    public void PlayFailure()
    {
        Play(MPVibrationType.Failure);
    }

    /// <summary>
    /// 播放运行时生成的单次强调反馈。
    /// 强度和频率都会限制在 0~1。
    /// </summary>
    public void PlayEmphasis(float amplitude, float frequency)
    {
        if (!CanPlay())
        {
            return;
        }

        ExecutePlayback(() => HapticPatterns.PlayEmphasis(
            Mathf.Clamp01(amplitude),
            Mathf.Clamp01(frequency)));
    }

    /// <summary>
    /// 播放一段恒定震动。duration 单位为秒，必须大于 0。
    /// </summary>
    public void PlayConstant(float amplitude, float frequency, float duration)
    {
        if (duration <= 0f || !CanPlay())
        {
            return;
        }

        ExecutePlayback(() => HapticPatterns.PlayConstant(
            Mathf.Clamp01(amplitude),
            Mathf.Clamp01(frequency),
            duration));
    }

    /// <summary>
    /// 播放导入的 .haptic 自定义片段。
    /// 不支持高级震动的设备会使用 fallbackType 对应的预设反馈。
    /// </summary>
    public void PlayClip(
        HapticClip clip,
        float clipLevel = 1f,
        bool loop = false,
        MPVibrationType fallbackType = MPVibrationType.MediumImpact)
    {
        if (clip == null || !CanPlay())
        {
            return;
        }

        ExecutePlayback(() =>
        {
            HapticController.fallbackPreset = ToPresetType(fallbackType);
            HapticController.Load(clip);
            HapticController.clipLevel = Mathf.Clamp01(clipLevel);
            HapticController.Loop(loop);
            HapticController.Play();
        });
    }

    /// <summary>
    /// 立即停止当前可停止的震动或循环片段。
    /// iOS 的系统预设属于瞬时反馈，触发后无法中途停止。
    /// </summary>
    public void Stop()
    {
        if (!m_initialized)
        {
            return;
        }

        ExecutePlayback(HapticController.Stop);
    }

    internal void ProcessApplicationFocus(bool hasFocus)
    {
        if (!m_initialized)
        {
            return;
        }

        ExecutePlayback(() => HapticController.ProcessApplicationFocus(hasFocus));
    }

    internal void ProcessApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Stop();
        }
    }

    internal void ProcessLifecycleDestroyed(MPVibrationLifecycleHook lifecycleHook)
    {
        if (m_lifecycleHook != lifecycleHook)
        {
            return;
        }

        Stop();
        m_lifecycleHook = null;
        m_initialized = false;
    }

    private bool CanPlay()
    {
        Initialize();
        if (!m_initialized || !IsEnabled)
        {
            return false;
        }

        ApplyEnabledState(true);
        return true;
    }

    private void ApplyEnabledState(bool isEnabled)
    {
        if (!m_initialized || HapticController.hapticsEnabled == isEnabled)
        {
            return;
        }

        HapticController.hapticsEnabled = isEnabled;
    }

    private void CreateLifecycleHook()
    {
        if (m_lifecycleHook != null)
        {
            return;
        }

        GameObject lifecycleObject = new GameObject("[MPVibrationManager]");
        UnityEngine.Object.DontDestroyOnLoad(lifecycleObject);
        m_lifecycleHook = lifecycleObject.AddComponent<MPVibrationLifecycleHook>();
    }

    private void ExecutePlayback(Action playbackAction)
    {
        try
        {
            playbackAction?.Invoke();
        }
        catch (Exception exception)
        {
            if (m_hasLoggedPlaybackError)
            {
                return;
            }

            m_hasLoggedPlaybackError = true;
            Debug.LogWarning($"[MPVibrationManager] 震动播放失败：{exception.Message}");
        }
    }

    private static HapticPatterns.PresetType ToPresetType(MPVibrationType vibrationType)
    {
        switch (vibrationType)
        {
            case MPVibrationType.Selection:
                return HapticPatterns.PresetType.Selection;
            case MPVibrationType.LightImpact:
                return HapticPatterns.PresetType.LightImpact;
            case MPVibrationType.MediumImpact:
                return HapticPatterns.PresetType.MediumImpact;
            case MPVibrationType.HeavyImpact:
                return HapticPatterns.PresetType.HeavyImpact;
            case MPVibrationType.RigidImpact:
                return HapticPatterns.PresetType.RigidImpact;
            case MPVibrationType.SoftImpact:
                return HapticPatterns.PresetType.SoftImpact;
            case MPVibrationType.Success:
                return HapticPatterns.PresetType.Success;
            case MPVibrationType.Warning:
                return HapticPatterns.PresetType.Warning;
            case MPVibrationType.Failure:
                return HapticPatterns.PresetType.Failure;
            default:
                return HapticPatterns.PresetType.Selection;
        }
    }
}

/// <summary>
/// 将 Unity 生命周期事件转发给非 MonoBehaviour 的震动控制器。
/// </summary>
internal sealed class MPVibrationLifecycleHook : MonoBehaviour
{
    private void OnApplicationFocus(bool hasFocus)
    {
        MPVibrationManager.Instance.ProcessApplicationFocus(hasFocus);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        MPVibrationManager.Instance.ProcessApplicationPause(pauseStatus);
    }

    private void OnDestroy()
    {
        MPVibrationManager.Instance.ProcessLifecycleDestroyed(this);
    }
}
