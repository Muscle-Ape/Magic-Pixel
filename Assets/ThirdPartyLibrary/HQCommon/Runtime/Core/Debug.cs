using UnityEngine;
using System.Diagnostics;
using UnityEngine.Internal;
using System;
using System.Text;
using System.Data;

namespace Joy
{
    public static class Debug
    {
        private static int __MAX_LOG_NUM = 100;

        private static StringBuilder[] logs = new StringBuilder[__MAX_LOG_NUM];
        private static int __head = 0;
        private static int __tail = 0;

        public delegate void ReportDelegate(string name, string stack);

        public static ReportDelegate reporter;

        static Debug()
        {
            for (int i = 0; i < logs.Length; i++)
            {
                logs[i] = new StringBuilder();
            }
        }

        private static StringBuilder GetFreeLogBuilder()
        {
            StringBuilder ret = logs[__tail];
            if ((__tail + 1) % __MAX_LOG_NUM == __head)
            {//已满，则覆盖一条
                __tail = (__tail + 1) % __MAX_LOG_NUM;
                __head = (__head + 1) % __MAX_LOG_NUM;
            }
            else
            {
                __tail = (__tail + 1) % __MAX_LOG_NUM;
            }
            ret.Length = 0;
            return ret;
        }

        public static string GetLogContext()
        {
            StringBuilder ret = new StringBuilder(10240);
            for (int i = __head; i != __tail; i = (i + 1) % __MAX_LOG_NUM)
            {
                ret.Append(logs[i].ToString()).Append("\n");
            }
            return ret.ToString();
        }

        public static void Clear()
        {
            __head = 0;
            __tail = 0;
        }

#if PUB_RELEASE
            static public bool EnableLog = false;
#else
        static public bool EnableLog = true;
#endif

        public static bool developerConsoleVisible { get { return UnityEngine.Debug.developerConsoleVisible; } set { UnityEngine.Debug.developerConsoleVisible = value; } }

        public static bool isDebugBuild { get { return UnityEngine.Debug.isDebugBuild; } }

        public static void Assert(bool condition)
        {
            if (!EnableLog) return;
            UnityEngine.Debug.Assert(condition);
        }

        public static void Assert(bool condition, string message)
        {
            if (!EnableLog) return;
            UnityEngine.Debug.Assert(condition, message);
        }

        public static void AssertFormat(bool condition, string format, params object[] args)
        {
            if (!EnableLog) return;
            UnityEngine.Debug.AssertFormat(condition, format, args);
        }
        public static void Break()
        {
            UnityEngine.Debug.Break();
        }
        public static void ClearDeveloperConsole()
        {
            UnityEngine.Debug.ClearDeveloperConsole();
        }
        public static void DebugBreak()
        {
            UnityEngine.Debug.DebugBreak();
        }

        public static void DrawLine(Vector3 start, Vector3 end)
        {
            UnityEngine.Debug.DrawLine(start, end);
        }

        public static void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            UnityEngine.Debug.DrawLine(start, end, color);
        }

        public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration)
        {
            UnityEngine.Debug.DrawLine(start, end, color, duration);
        }

        public static void DrawLine(Vector3 start, Vector3 end, [DefaultValue("Color.white")] Color color, [DefaultValue("0.0f")] float duration, [DefaultValue("true")] bool depthTest)
        {
            UnityEngine.Debug.DrawLine(start, end, color, duration, depthTest);
        }

        public static void DrawRay(Vector3 start, Vector3 dir)
        {
            UnityEngine.Debug.DrawRay(start, dir);
        }

        public static void DrawRay(Vector3 start, Vector3 dir, Color color)
        {
            UnityEngine.Debug.DrawRay(start, dir, color);
        }

        public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration)
        {
            UnityEngine.Debug.DrawRay(start, dir, color, duration);
        }

        public static void DrawRay(Vector3 start, Vector3 dir, [DefaultValue("Color.white")] Color color, [DefaultValue("0.0f")] float duration, [DefaultValue("true")] bool depthTest)
        {
            UnityEngine.Debug.DrawRay(start, dir, color, duration, depthTest);
        }

        public static void Log(object message, UnityEngine.Object context = null)
        {
            if (!EnableLog) return;
            UnityEngine.Debug.Log(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + message, context);
        }

        public static void LogFormat(string format, params object[] args)
        {
            if (!EnableLog) return;
            UnityEngine.Debug.LogFormat(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + format, args);
        }

        public static void LogError(object message, UnityEngine.Object context = null)
        {
            StringBuilder builder = GetFreeLogBuilder();
            if (builder != null)
            {
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + message);
            }
            UnityEngine.Debug.LogError("UnityError " + message, context);
        }

        public static void LogErrorFormat(string format, params object[] args)
        {
            StringBuilder content = new StringBuilder();
            content.AppendFormat(format, args);
            StringBuilder builder = GetFreeLogBuilder();
            if (builder != null)
            {
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ")).Append(content);
            }
            UnityEngine.Debug.LogError(content);
        }

        public static void LogException(Exception exception)
        {
            StringBuilder builder = GetFreeLogBuilder();
            if (builder != null)
            {
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + exception.ToString());
            }
            UnityEngine.Debug.LogException(exception);
        }

        public static void LogException(Exception exception, UnityEngine.Object context)
        {
            StringBuilder builder = GetFreeLogBuilder();
            if (builder != null)
            {
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + exception.ToString());
            }
            UnityEngine.Debug.LogException(exception, context);
        }

        public static void LogWarning(object message, UnityEngine.Object context = null)
        {
            if (!EnableLog) return;
            UnityEngine.Debug.LogWarning(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + message, context);
        }

        public static void LogWarningFormat(string format, params object[] args)
        {
            if (!EnableLog) return;
            UnityEngine.Debug.LogWarningFormat(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + format, args);
        }

        public static void LogOutput(object message, UnityEngine.Object context = null)
        {
            StringBuilder builder = GetFreeLogBuilder();
            if (builder != null)
            {
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + message);
            }
            UnityEngine.Debug.Log(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + message, context);
        }

        public static void LogOutputFormat(string format, params object[] args)
        {
            StringBuilder builder = GetFreeLogBuilder();
            if (builder != null)
            {
                builder.AppendFormat(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + format, args);
            }
            UnityEngine.Debug.LogFormat(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss ") + format, args);
        }

        public static void Report(string name, string stack)
        {
            reporter(name, stack);
        }

        public static void ResetLogQueueSize(int num)
        {
            __MAX_LOG_NUM = num;
            logs = new StringBuilder[__MAX_LOG_NUM];
            for (int i = 0; i < logs.Length; i++)
            {
                logs[i] = new StringBuilder();
            }
        }
    }
}