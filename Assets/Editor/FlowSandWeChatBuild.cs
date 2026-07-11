using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WeChatWASM;

public static class FlowSandWeChatBuild
{
    private const string OutputOverrideVariable = "FLOW_SAND_WX_OUTPUT";
    private const string SplitSourceVariable = "FLOW_SAND_WASM_SPLIT_SOURCE";

    [MenuItem("Flow Sand/Build/Export WeChat Release")]
    public static void ExportRelease()
    {
        Export(collectionBuild: false);
    }

    [MenuItem("Flow Sand/Build/Export WASM Collection Package")]
    public static void ExportWasmCollection()
    {
        Export(collectionBuild: true);
    }

    [MenuItem("Flow Sand/Build/Integrate Official WASM Split Result")]
    public static void IntegrateOfficialWasmSplitResult()
    {
        string source = Environment.GetEnvironmentVariable(SplitSourceVariable);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException($"Set {SplitSourceVariable} to the official split package directory first.");
        }

        source = NormalizeMiniGameRoot(Path.GetFullPath(source));
        ValidateSplitExport(source);

        string configuredDestination = Path.GetFullPath(WXConvertCore.config.ProjectConf.DST);
        string outputRoot = Environment.GetEnvironmentVariable(OutputOverrideVariable);
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            outputRoot = configuredDestination.TrimEnd(Path.DirectorySeparatorChar) + "-WasmSplit";
        }

        string destination = Path.Combine(Path.GetFullPath(outputRoot), WXConvertCore.miniGameDir);
        CopyDirectory(source, destination);
        ValidateSplitExport(destination);
        Debug.Log($"[FlowSand Build] Integrated and validated official WASM split package at {destination}");
    }

    private static void Export(bool collectionBuild)
    {
        var config = WXConvertCore.config;
        string originalRelativeDestination = config.ProjectConf.relativeDST;
        string originalDestination = config.ProjectConf.DST;
        string originalEmscriptenArgs = PlayerSettings.WebGL.emscriptenArgs;
        bool originalDevelopBuild = config.CompileOptions.DevelopBuild;
        bool originalAutoProfile = config.CompileOptions.AutoProfile;
        bool originalScriptOnly = config.CompileOptions.ScriptOnly;
        bool originalOptimizeSize = config.CompileOptions.Il2CppOptimizeSize;
        bool originalProfilingFunctions = config.CompileOptions.profilingFuncs;
        bool originalProfilingMemory = config.CompileOptions.ProfilingMemory;
        bool originalCleanBuild = config.CompileOptions.CleanBuild;
        bool originalMonitorModal = config.CompileOptions.showMonitorSuggestModal;
        bool originalProfileStats = config.CompileOptions.enableProfileStats;
        bool originalRenderAnalysis = config.CompileOptions.enableRenderAnalysis;
        bool originalPerformanceAnalysis = config.CompileOptions.enablePerfAnalysis;

        string outputRoot = ResolveOutputRoot(originalDestination, collectionBuild);
        try
        {
            config.ProjectConf.relativeDST = outputRoot;
            config.ProjectConf.DST = outputRoot;
            config.CompileOptions.DevelopBuild = false;
            config.CompileOptions.AutoProfile = false;
            config.CompileOptions.ScriptOnly = false;
            config.CompileOptions.Il2CppOptimizeSize = true;
            config.CompileOptions.profilingFuncs = collectionBuild;
            config.CompileOptions.ProfilingMemory = false;
            config.CompileOptions.CleanBuild = true;
            config.CompileOptions.showMonitorSuggestModal = false;
            config.CompileOptions.enableProfileStats = false;
            config.CompileOptions.enableRenderAnalysis = false;
            config.CompileOptions.enablePerfAnalysis = false;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FlowSand Build] Exporting {(collectionBuild ? "WASM collection" : "release")} package to {outputRoot}");
            WXConvertCore.WXExportError result = WXConvertCore.DoExport();
            if (result != WXConvertCore.WXExportError.SUCCEED)
            {
                throw new InvalidOperationException($"WeChat export failed: {result}");
            }

            ValidateExport(outputRoot, collectionBuild);
        }
        finally
        {
            config.ProjectConf.relativeDST = originalRelativeDestination;
            config.ProjectConf.DST = originalDestination;
            config.CompileOptions.DevelopBuild = originalDevelopBuild;
            config.CompileOptions.AutoProfile = originalAutoProfile;
            config.CompileOptions.ScriptOnly = originalScriptOnly;
            config.CompileOptions.Il2CppOptimizeSize = originalOptimizeSize;
            config.CompileOptions.profilingFuncs = originalProfilingFunctions;
            config.CompileOptions.ProfilingMemory = originalProfilingMemory;
            config.CompileOptions.CleanBuild = originalCleanBuild;
            config.CompileOptions.showMonitorSuggestModal = originalMonitorModal;
            config.CompileOptions.enableProfileStats = originalProfileStats;
            config.CompileOptions.enableRenderAnalysis = originalRenderAnalysis;
            config.CompileOptions.enablePerfAnalysis = originalPerformanceAnalysis;
            PlayerSettings.WebGL.emscriptenArgs = originalEmscriptenArgs;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }

    private static string ResolveOutputRoot(string configuredDestination, bool collectionBuild)
    {
        string overridePath = Environment.GetEnvironmentVariable(OutputOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        string root = Path.GetFullPath(configuredDestination);
        return collectionBuild ? root.TrimEnd(Path.DirectorySeparatorChar) + "-WasmCollection" : root;
    }

    private static void ValidateExport(string outputRoot, bool collectionBuild)
    {
        string miniGameRoot = Path.Combine(outputRoot, WXConvertCore.miniGameDir);
        string gameJson = Path.Combine(miniGameRoot, "game.json");
        string symbols = Path.Combine(miniGameRoot, "webgl.wasm.symbols.unityweb");
        if (!File.Exists(gameJson))
        {
            throw new FileNotFoundException("WeChat game.json was not generated", gameJson);
        }

        if (!File.Exists(symbols))
        {
            throw new FileNotFoundException("External WASM symbols are required for function splitting", symbols);
        }

        string wasmRoot = Path.Combine(miniGameRoot, "wasmcode");
        string[] wasmFiles = Directory.Exists(wasmRoot)
            ? Directory.GetFiles(wasmRoot, "*.wasm*", SearchOption.AllDirectories)
            : Array.Empty<string>();
        wasmFiles = wasmFiles
            .Where(path => !path.Contains(".symbols.", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (wasmFiles.Length == 0)
        {
            throw new FileNotFoundException("No WASM code artifact was generated", miniGameRoot);
        }

        long wasmBytes = wasmFiles.Sum(path => new FileInfo(path).Length);
        long symbolBytes = new FileInfo(symbols).Length;
        Debug.Log(
            $"[FlowSand Build] Validated {(collectionBuild ? "collection" : "release")} export. " +
            $"WASM artifacts={wasmFiles.Length}, WASM bytes={wasmBytes}, symbols bytes={symbolBytes}");
    }

    private static string NormalizeMiniGameRoot(string path)
    {
        string nested = Path.Combine(path, WXConvertCore.miniGameDir);
        return File.Exists(Path.Combine(nested, "game.json")) ? nested : path;
    }

    private static void ValidateSplitExport(string miniGameRoot)
    {
        string gameJson = Path.Combine(miniGameRoot, "game.json");
        if (!File.Exists(gameJson))
        {
            throw new FileNotFoundException("Official split package is missing game.json", gameJson);
        }

        string[] javascriptFiles = Directory.GetFiles(miniGameRoot, "*.js", SearchOption.AllDirectories);
        bool hasSplitFlag = javascriptFiles.Any(path => File.ReadAllText(path).Contains("useWasmCodeSplit", StringComparison.Ordinal));
        bool hasSubWasmCompiler = javascriptFiles.Any(path => File.ReadAllText(path).Contains("compileSubWasm", StringComparison.Ordinal));
        if (!hasSplitFlag || !hasSubWasmCompiler)
        {
            throw new InvalidDataException("Package is not a completed official WASM split result: split runtime hooks are missing.");
        }

        int wasmArtifactCount = Directory.GetFiles(miniGameRoot, "*.wasm*", SearchOption.AllDirectories)
            .Count(path => !path.Contains(".symbols.", StringComparison.OrdinalIgnoreCase));
        if (wasmArtifactCount < 2)
        {
            throw new InvalidDataException("Package does not contain both core and deferred WASM artifacts.");
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
    }
}
