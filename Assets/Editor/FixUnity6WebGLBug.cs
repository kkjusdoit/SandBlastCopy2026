using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps malloc/free alive in Unity 6000.0 WebGL builds affected by UUM-74261.
/// </summary>
public sealed class FixUnity6WebGLBug : IPreprocessBuildWithReport
{
    private const string ExportedFunctionsSetting = "EXPORTED_FUNCTIONS=";

    public int callbackOrder => int.MaxValue;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
        {
            return;
        }

        // Auto Graphics API needs to be enabled for WebGL to ensure WebGL 2.0 is active,
        // which is required for Linear Color Space and URP shaders in WeChat Mini Game.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, true);

        string currentArgs = PlayerSettings.WebGL.emscriptenArgs ?? string.Empty;
        string updatedArgs = EnsureExportedFunctions(currentArgs, "_malloc", "_free");

        if (updatedArgs == currentArgs)
        {
            Debug.Log("[WebGL] malloc/free are already present in EXPORTED_FUNCTIONS.");
            return;
        }

        PlayerSettings.WebGL.emscriptenArgs = updatedArgs;
        Debug.Log("[WebGL] Added malloc/free to final Emscripten arguments: " + updatedArgs);
    }

    internal static string EnsureExportedFunctions(string arguments, params string[] functions)
    {
        int settingIndex = arguments.IndexOf(ExportedFunctionsSetting, StringComparison.Ordinal);
        if (settingIndex < 0)
        {
            string exports = string.Join(",", functions.Distinct());
            return (arguments + " -s EXPORTED_FUNCTIONS=" + exports).TrimStart();
        }

        int valueStart = settingIndex + ExportedFunctionsSetting.Length;
        int valueEnd = arguments.IndexOf(' ', valueStart);
        if (valueEnd < 0)
        {
            valueEnd = arguments.Length;
        }

        string currentExports = arguments.Substring(valueStart, valueEnd - valueStart);
        string[] exportedFunctions = currentExports.Split(',');
        string[] missingFunctions = functions
            .Where(function => !exportedFunctions.Contains(function))
            .Distinct()
            .ToArray();

        if (missingFunctions.Length == 0)
        {
            return arguments;
        }

        string separator = currentExports.Length == 0 ? string.Empty : ",";
        return arguments.Insert(valueEnd, separator + string.Join(",", missingFunctions));
    }
}
