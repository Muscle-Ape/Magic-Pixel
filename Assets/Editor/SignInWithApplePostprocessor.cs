#if UNITY_IOS

using AppleAuth.Editor;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// 为 iOS 导出工程添加 Sign in with Apple 所需的 Capability、Entitlement 与系统框架。
/// </summary>
public static class SignInWithApplePostprocessor
{
    private const string EntitlementsFileName = "Entitlements.entitlements";

    [PostProcessBuild(1)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject project = new PBXProject();
        project.ReadFromString(File.ReadAllText(projectPath));

        ProjectCapabilityManager capabilityManager = new ProjectCapabilityManager(
            projectPath,
            EntitlementsFileName,
            null,
            project.GetUnityMainTargetGuid());

        capabilityManager.AddSignInWithAppleWithCompatibility();
        capabilityManager.WriteToFile();
    }
}

#endif
