using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor;
public class MaxPostprocessor : IPreprocessBuildWithReport
{
    public void OnPreprocessBuild(BuildReport report)
    {
#if ao_ads_max
        MaxSdk.SetVerboseLogging(EditorUserBuildSettings.development);
#endif
    }

    public int callbackOrder
    {
        get { return int.MaxValue; }
    }
}