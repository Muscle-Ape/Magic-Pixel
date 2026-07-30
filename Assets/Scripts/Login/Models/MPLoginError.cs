using System;

/// <summary>
/// 登录模块统一错误结构，避免 UI 直接依赖 Unity SDK 的异常类型。
/// </summary>
public class MPLoginError
{
    /// <summary>项目内部错误码。</summary>
    public string code;
    /// <summary>可直接用于 UI 提示的错误信息。</summary>
    public string message;
    /// <summary>是否建议 UI 展示重试入口。</summary>
    public bool retryable;
    /// <summary>Unity Services 或底层服务返回的错误码。</summary>
    public int serviceErrorCode;
    /// <summary>原始异常，仅用于调试，不要直接展示给玩家。</summary>
    public Exception exception;
    /// <summary>是否属于网络、超时、维护等临时失败，不应清理本地账号资料。</summary>
    public bool isTemporary;

    /// <summary>
    /// 创建统一错误对象。
    /// </summary>
    public static MPLoginError Create(string code, string message, bool retryable = false, int serviceErrorCode = 0, Exception exception = null, bool isTemporary = false)
    {
        return new MPLoginError
        {
            code = code,
            message = message,
            retryable = retryable,
            serviceErrorCode = serviceErrorCode,
            exception = exception,
            isTemporary = isTemporary
        };
    }
}
