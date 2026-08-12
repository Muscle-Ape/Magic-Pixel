using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Editor-only helper tools for the MagicPixel custom level Cloud Code module.
/// </summary>
public static class MPCustomLevelCloudCodeSetupTools
{
    private const string DotnetPathEditorPrefsKey = "DotnetPath";
    private const string DefaultWindowsDotnetPath = @"C:\Program Files\dotnet\dotnet.exe";
    private const string DefaultMacDotnetPath = "/usr/local/bin/dotnet";
    private const string DefaultMacShareDotnetPath = "/usr/local/share/dotnet/dotnet";
    private const string DefaultHomebrewDotnetPath = "/opt/homebrew/bin/dotnet";
    private const string DefaultLinuxDotnetPath = "/usr/bin/dotnet";
    private const string ModuleReferencePath = "Assets/CloudCode/MagicPixelCustomLevelPublish.ccmr";
    private const string SolutionRelativePath = "CloudCodeModules/MagicPixelCustomLevelPublish/MagicPixelCustomLevelPublish.sln";

    /// <summary>
    /// Unity started from the macOS GUI may not inherit the shell PATH. Keep the
    /// Cloud Code package preference on an absolute SDK executable path.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void ScheduleDotnetPathValidation()
    {
        EditorApplication.delayCall += EnsureDotnetPathConfigured;
    }

    /// <summary>
    /// Writes the .NET path used by Unity's Cloud Code package.
    /// </summary>
    [MenuItem("MagicPixel/Cloud Code/Set .NET Path")]
    public static void SetDotnetPathMenu()
    {
        SetDotnetPath(ResolveDefaultDotnetPath());
    }

    /// <summary>
    /// Command line entry used by Unity batchmode.
    /// Example:
    /// Unity.exe -batchmode -projectPath "..." -executeMethod MPCustomLevelCloudCodeSetupTools.SetDotnetPathFromCommandLine -dotnetPath "C:\Program Files\dotnet\dotnet.exe" -quit
    /// </summary>
    public static void SetDotnetPathFromCommandLine()
    {
        SetDotnetPath(GetCommandLineValue("-dotnetPath", ResolveDefaultDotnetPath()));
    }

    /// <summary>
    /// Builds the C# Cloud Code module locally.
    /// </summary>
    [MenuItem("MagicPixel/Cloud Code/Build Custom Level Module")]
    public static void BuildCustomLevelModule()
    {
        SetDotnetPath(ResolveDefaultDotnetPath());
        RunDotnetBuild();
    }

    /// <summary>
    /// Opens the Deployment window and selects the custom level module reference.
    /// </summary>
    [MenuItem("MagicPixel/Cloud Code/Open Custom Level Module In Deployment")]
    public static void OpenCustomLevelModuleInDeployment()
    {
        SetDotnetPath(ResolveDefaultDotnetPath());
        Deployments.Instance.DeploymentWindow.OpenWindow();
        EditorApplication.delayCall += SelectCustomLevelModule;
    }

    /// <summary>
    /// Deploys the custom level C# module through Unity's Deployment package.
    /// Unity must be signed in, linked to a Cloud Project, and have the target environment selected.
    /// </summary>
    [MenuItem("MagicPixel/Cloud Code/Deploy Custom Level Module")]
    public static void DeployCustomLevelModuleMenu()
    {
        Deployments.Instance.DeploymentWindow.OpenWindow();
        EditorApplication.delayCall += DeployCustomLevelModuleDelayed;
    }

    /// <summary>
    /// Command line entry for deployment. This exits Unity with 0 on success and 1 on failure.
    /// </summary>
    public static void DeployCustomLevelModuleFromCommandLine()
    {
        Deployments.Instance.DeploymentWindow.OpenWindow();
        EditorApplication.delayCall += DeployCustomLevelModuleAndExitDelayed;
    }

    private static void EnsureDotnetPathConfigured()
    {
        try
        {
            string configuredPath = EditorPrefs.GetString(DotnetPathEditorPrefsKey, string.Empty);
            string resolvedPath = ResolveDotnetPath(configuredPath);
            if (string.Equals(configuredPath, resolvedPath, StringComparison.Ordinal))
            {
                return;
            }

            EditorPrefs.SetString(DotnetPathEditorPrefsKey, resolvedPath);
            Debug.Log($"[MagicPixel] Cloud Code .NET path automatically fixed: {resolvedPath}");
        }
        catch (FileNotFoundException exception)
        {
            Debug.LogWarning($"[MagicPixel] {exception.Message}");
        }
    }

    private static void SetDotnetPath(string dotnetPath)
    {
        dotnetPath = ResolveDotnetPath(dotnetPath);

        EditorPrefs.SetString(DotnetPathEditorPrefsKey, dotnetPath);
        Debug.Log($"[MagicPixel] Cloud Code .NET path set to: {dotnetPath}");
    }

    private static string ResolveDefaultDotnetPath()
    {
        return ResolveDotnetPath(EditorPrefs.GetString(DotnetPathEditorPrefsKey, string.Empty));
    }

    private static string ResolveDotnetPath(string configuredPath)
    {
        foreach (string candidate in GetDotnetPathCandidates(configuredPath))
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
            {
                continue;
            }

            return Path.GetFullPath(candidate);
        }

        throw new FileNotFoundException(
            "Failed to locate a .NET SDK executable. Install the .NET SDK, or configure its absolute path in Preferences > Cloud Code Modules > .NET development environment.");
    }

    private static IEnumerable<string> GetDotnetPathCandidates(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) &&
            !string.Equals(configuredPath, "dotnet", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(configuredPath, "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            yield return configuredPath.Trim().Trim('"');
        }

        string executableName = Application.platform == RuntimePlatform.WindowsEditor ? "dotnet.exe" : "dotnet";
        string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            yield return Path.Combine(dotnetRoot.Trim().Trim('"'), executableName);
        }

#if UNITY_EDITOR_WIN
        yield return DefaultWindowsDotnetPath;
#elif UNITY_EDITOR_OSX
        yield return DefaultMacDotnetPath;
        yield return DefaultHomebrewDotnetPath;
        yield return DefaultMacShareDotnetPath;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", executableName);
#elif UNITY_EDITOR_LINUX
        yield return DefaultLinuxDotnetPath;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", executableName);
#endif

        string pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            yield break;
        }

        foreach (string directory in pathValue.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return Path.Combine(directory.Trim().Trim('"'), executableName);
            }
        }
    }

    private static string GetCommandLineValue(string key, string defaultValue)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return defaultValue;
    }

    private static void RunDotnetBuild()
    {
        string solutionPath = Path.GetFullPath(Path.Combine(GetProjectRoot(), SolutionRelativePath));
        string dotnetPath = ResolveDefaultDotnetPath();

        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException("Cloud Code solution was not found.", solutionPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = dotnetPath,
            Arguments = $"build \"{solutionPath}\" --configuration Release",
            WorkingDirectory = GetProjectRoot(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start dotnet build process.");
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output))
            {
                Debug.Log(output);
            }

            if (process.ExitCode != 0)
            {
                Debug.LogError(error);
                throw new InvalidOperationException($"dotnet build failed with exit code {process.ExitCode}.");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning(error);
            }
        }

        Debug.Log("[MagicPixel] Custom level Cloud Code module build finished.");
    }

    private static void SelectCustomLevelModule()
    {
        try
        {
            IDeploymentWindow window = Deployments.Instance.DeploymentWindow;
            List<IDeploymentItem> items = GetCustomLevelDeploymentItems(window);
            window.ClearSelection();
            window.Select(items);
            window.ClearChecked();
            window.Check(items);
            Debug.Log("[MagicPixel] Custom level Cloud Code module selected in Deployment window.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MagicPixel] Failed to select Cloud Code module in Deployment window: {exception.Message}");
        }
    }

    private static async void DeployCustomLevelModuleDelayed()
    {
        await DeployCustomLevelModuleAsync(exitOnFinish: false);
    }

    private static async void DeployCustomLevelModuleAndExitDelayed()
    {
        bool success = await DeployCustomLevelModuleAsync(exitOnFinish: true);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static async Task<bool> DeployCustomLevelModuleAsync(bool exitOnFinish)
    {
        try
        {
            SetDotnetPath(GetCommandLineValue("-dotnetPath", ResolveDefaultDotnetPath()));
            AssetDatabase.Refresh();
            RunDotnetBuild();

            IDeploymentWindow window = Deployments.Instance.DeploymentWindow;
            List<IDeploymentItem> items = GetCustomLevelDeploymentItems(window);
            window.ClearSelection();
            window.Select(items);
            window.ClearChecked();
            window.Check(items);

            DeploymentResult<IDeploymentItem> result = await window.Deploy(items);
            Debug.Log($"[MagicPixel] Custom level Cloud Code module deployed. Deployed items: {result.Deployed.Count}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MagicPixel] Cloud Code module deployment failed: {exception}");
            if (!exitOnFinish)
            {
                Debug.LogError("[MagicPixel] Check Unity sign-in, project linking, Services environment, and Cloud Code/Cloud Save enablement.");
            }

            return false;
        }
    }

    private static List<IDeploymentItem> GetCustomLevelDeploymentItems(IDeploymentWindow window)
    {
        List<IDeploymentItem> items = window.GetFromFiles(new[] { ModuleReferencePath })
            .Where(item => item != null)
            .ToList();

        if (items.Count == 0)
        {
            throw new InvalidOperationException($"Deployment item was not found for {ModuleReferencePath}.");
        }

        return items;
    }

    private static string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }
}
