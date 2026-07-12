using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using LitJson;
using WeChatWASM;

public static class FlowSandWeChatBuild
{
    private const string OutputOverrideVariable = "FLOW_SAND_WX_OUTPUT";
    private const string SplitSourceVariable = "FLOW_SAND_WASM_SPLIT_SOURCE";
    private const string HotFunctionListName = "FlowSand-Wasm-HotFunctions.txt";
    private const string DefaultWeChatExportFolder = "SandFlow";
    private const string WasmCollectionSuffix = "-WasmCollection";
    private const string MinigameLoadingProvider = "wxbd990766293b9dc4";
    private const string MinigameLoadingVersion = "1.0.16";
    private const string MinigameLoadingModuleAsset = "Assets/Editor/WeChat/minigame-loading.js";
    private const string MinigameLoadingCoverAsset = "Assets/WX-WASM-SDK-V2/Runtime/wechat-default/images/background.jpg";
    private static readonly Regex SymbolEntryRegex = new Regex(
        "\\\"\\d+\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
        RegexOptions.Compiled);
    private static readonly Regex Il2CppHashRegex = new Regex(
        "_m[0-9A-Fa-f]{40}$",
        RegexOptions.Compiled);

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

    [MenuItem("Flow Sand/Build/Generate WASM Hot Function List")]
    public static void GenerateWasmHotFunctionList()
    {
        string outputRoot = ResolveOutputRoot(WXConvertCore.config.ProjectConf.DST, collectionBuild: true);
        string miniGameRoot = Path.Combine(outputRoot, WXConvertCore.miniGameDir);
        GenerateWasmHotFunctionList(miniGameRoot);
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

            string miniGameRoot = Path.Combine(outputRoot, WXConvertCore.miniGameDir);
            IntegrateMinigameLoading(miniGameRoot);
            ValidateExport(outputRoot, collectionBuild);
            if (collectionBuild)
            {
                GenerateWasmHotFunctionList(miniGameRoot);
            }
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

        string root;
        if (string.IsNullOrWhiteSpace(configuredDestination))
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "WeChatProjects",
                DefaultWeChatExportFolder);
        }
        else
        {
            root = Path.GetFullPath(configuredDestination);
        }

        root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (root.EndsWith(WasmCollectionSuffix, StringComparison.OrdinalIgnoreCase))
        {
            root = root.Substring(0, root.Length - WasmCollectionSuffix.Length);
        }

        return collectionBuild ? root + WasmCollectionSuffix : root;
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

    private static void IntegrateMinigameLoading(string miniGameRoot)
    {
        string gameJsonPath = Path.Combine(miniGameRoot, "game.json");
        string gameJsPath = Path.Combine(miniGameRoot, "game.js");
        string utilJsPath = Path.Combine(miniGameRoot, "unity-sdk", "util.js");
        if (!File.Exists(gameJsonPath) || !File.Exists(gameJsPath) || !File.Exists(utilJsPath))
        {
            throw new FileNotFoundException("MinigameLoading integration requires game.json, game.js, and unity-sdk/util.js", miniGameRoot);
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unable to resolve Unity project root.");
        string moduleSource = Path.Combine(projectRoot, MinigameLoadingModuleAsset);
        string coverSource = Path.Combine(projectRoot, MinigameLoadingCoverAsset);
        if (!File.Exists(moduleSource) || !File.Exists(coverSource))
        {
            throw new FileNotFoundException("MinigameLoading source module or cover image is missing.");
        }

        string gameJson = File.ReadAllText(gameJsonPath);
        JsonData gameConfig;
        try
        {
            gameConfig = JsonMapper.ToObject(gameJson);
        }
        catch (Exception exception)
        {
            throw new InvalidDataException("Exported game.json is not valid JSON.", exception);
        }

        if (!gameConfig.IsObject)
        {
            throw new InvalidDataException("Exported game.json must contain a JSON object.");
        }

        if (!gameConfig.Keys.Contains("plugins"))
        {
            gameConfig["plugins"] = new JsonData();
        }
        if (!gameConfig["plugins"].IsObject)
        {
            throw new InvalidDataException("The plugins value in exported game.json must be an object.");
        }

        if (!gameConfig["plugins"].Keys.Contains("MinigameLoading"))
        {
            var pluginConfig = new JsonData();
            pluginConfig["version"] = MinigameLoadingVersion;
            pluginConfig["provider"] = MinigameLoadingProvider;
            var contexts = new JsonData();
            var context = new JsonData();
            context["type"] = "isolatedContext";
            contexts.Add(context);
            pluginConfig["contexts"] = contexts;
            gameConfig["plugins"]["MinigameLoading"] = pluginConfig;
            File.WriteAllText(gameJsonPath, JsonMapper.ToJson(gameConfig));
        }

        string gameJs = File.ReadAllText(gameJsPath);
        const string moduleImport = "import './minigame-loading';";
        if (!gameJs.Contains(moduleImport, StringComparison.Ordinal))
        {
            const string adapterImport = "import './weapp-adapter';";
            if (!gameJs.Contains(adapterImport, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Unable to find the adapter import in exported game.js.");
            }
            gameJs = gameJs.Replace(adapterImport, adapterImport + "\n" + moduleImport);
            File.WriteAllText(gameJsPath, gameJs);
        }

        string utilJs = File.ReadAllText(utilJsPath);
        const string hideLoadingCall = "GameGlobal.manager.hideLoadingPage();";
        const string destroyLoadingCall = "GameGlobal.manager.hideLoadingPage();\n            if (GameGlobal.destroyMinigameLoading) {\n                GameGlobal.destroyMinigameLoading();\n            }";
        if (!utilJs.Contains("GameGlobal.destroyMinigameLoading", StringComparison.Ordinal))
        {
            if (!utilJs.Contains(hideLoadingCall, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Unable to find WXHideLoadingPage in exported unity-sdk/util.js.");
            }
            utilJs = utilJs.Replace(hideLoadingCall, destroyLoadingCall);
            File.WriteAllText(utilJsPath, utilJs);
        }

        File.Copy(moduleSource, Path.Combine(miniGameRoot, "minigame-loading.js"), true);
        string imagesRoot = Path.Combine(miniGameRoot, "images");
        Directory.CreateDirectory(imagesRoot);
        File.Copy(coverSource, Path.Combine(imagesRoot, "minigame-loading-cover.jpg"), true);
        Debug.Log($"[FlowSand Build] Integrated MinigameLoading {MinigameLoadingVersion} into {miniGameRoot}");
    }

    private static void GenerateWasmHotFunctionList(string miniGameRoot)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unable to resolve Unity project root.");
        string templatePath = Path.Combine(projectRoot, HotFunctionListName);
        string symbolsPath = Path.Combine(miniGameRoot, "webgl.wasm.symbols.unityweb");
        string outputPath = Path.Combine(miniGameRoot, HotFunctionListName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("WASM hot function template is missing", templatePath);
        }

        if (!File.Exists(symbolsPath))
        {
            throw new FileNotFoundException("WASM symbol table is missing", symbolsPath);
        }

        string symbolJson = File.ReadAllText(symbolsPath);
        string[] symbols = SymbolEntryRegex.Matches(symbolJson)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToArray();
        if (symbols.Length == 0)
        {
            throw new InvalidDataException($"No function names were parsed from {symbolsPath}");
        }

        string[] templateFunctions = File.ReadAllLines(templatePath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (templateFunctions.Length == 0)
        {
            throw new InvalidDataException($"No function names were found in {templatePath}");
        }

        var resolved = new List<string>(templateFunctions.Length);
        var failures = new List<string>();
        foreach (string templateFunction in templateFunctions)
        {
            string stableName = Il2CppHashRegex.Replace(templateFunction, string.Empty);
            string[] matches = symbols
                .Where(symbol => symbol.Equals(templateFunction, StringComparison.Ordinal)
                    || symbol.StartsWith(stableName + "_m", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (matches.Length == 1)
            {
                resolved.Add(matches[0]);
            }
            else
            {
                failures.Add($"{stableName}: matches={matches.Length}");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidDataException(
                "Unable to resolve every WASM hot function against the current symbol table:\n" +
                string.Join("\n", failures));
        }

        string[] outputFunctions = resolved.Distinct(StringComparer.Ordinal).ToArray();
        if (outputFunctions.Length != templateFunctions.Length)
        {
            throw new InvalidDataException(
                $"Resolved hot function list contains duplicates: template={templateFunctions.Length}, " +
                $"resolved={outputFunctions.Length}");
        }

        Directory.CreateDirectory(miniGameRoot);
        File.WriteAllLines(outputPath, outputFunctions);
        File.WriteAllLines(templatePath, outputFunctions);
        Debug.Log(
            $"[FlowSand Build] Generated and validated {outputFunctions.Length} WASM hot functions. " +
            $"Upload file: {outputPath}");
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
