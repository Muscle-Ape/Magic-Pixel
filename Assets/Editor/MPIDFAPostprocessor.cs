#if UNITY_IOS

using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// 为 iOS 导出工程补充 ATT 权限文案和系统框架。
/// </summary>
public static class MPIDFAPostprocessor
{
    /// <summary>
    /// 允许跟踪将帮助我们提供更相关的广告，并支持游戏持续更新。
    /// </summary>
    private const string TRACKING_USAGE_DESCRIPTION =
        "Allowing tracking will help us provide more relevant ads and support ongoing game updates.";

    [PostProcessBuild(10)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
            return;

        string plistPath = Path.Combine(buildPath, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("NSUserTrackingUsageDescription", TRACKING_USAGE_DESCRIPTION);
        plist.WriteToFile(plistPath);

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);
        project.AddFrameworkToProject(
            project.GetUnityFrameworkTargetGuid(),
            "AppTrackingTransparency.framework",
            true);
        project.WriteToFile(projectPath);
    }
}

#endif
