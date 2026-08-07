
using System.Collections.Generic;
using UnityEditor;

[InitializeOnLoad]
public class AOAdsDefineSymbols
{
    static AOAdsDefineSymbols()
    {
        var defineSymbol = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

        string[] scriptDefines = { "ao_ads", "ao_ads_max"};

        foreach (var scriptDefine in scriptDefines)
        {
            var symbols = new HashSet<string>(defineSymbol.Split(";"));

            if (!symbols.Contains(scriptDefine))
            {
                defineSymbol += $";{scriptDefine}";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, defineSymbol);
            }
        }
    }
}