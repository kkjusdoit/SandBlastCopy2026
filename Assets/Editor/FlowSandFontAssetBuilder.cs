using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
internal static class FlowSandFontAssetBuilder
{
    private const string SourceFontPath = "Assets/Resources/Fonts/NotoSansSC-FlowSand.ttf";
    private const string FontAssetPath = "Assets/Resources/Fonts/NotoSansSC-FlowSand SDF.asset";
    private const string Characters =
        "七彩流沙方块分数下一个速度最高暂停加速放置让它们散落成连接同色的左右两侧即可消除" +
        "在棋盘上滑动虚拟摇杆移动旋转开始游戏当前状态已保持按键或点击继续本局结束堆积挡住出生区域重新再来最终连" +
        "0123456789P：，。×";

    static FlowSandFontAssetBuilder()
    {
        EditorApplication.delayCall += EnsureFontAsset;
    }

    [MenuItem("Flow Sand/Build/Rebuild Runtime Font Asset")]
    private static void RebuildFontAsset()
    {
        AssetDatabase.DeleteAsset(FontAssetPath);
        EnsureFontAsset();
    }

    private static void EnsureFontAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null)
        {
            return;
        }

        string absoluteFontPath = Path.GetFullPath(SourceFontPath);
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            absoluteFontPath,
            0,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024);
        if (fontAsset == null)
        {
            Debug.LogError($"[FlowSand Font] Unable to load font face from {absoluteFontPath}.");
            return;
        }

        if (!fontAsset.TryAddCharacters(Characters, out string missingCharacters))
        {
            Object.DestroyImmediate(fontAsset);
            Debug.LogError($"[FlowSand Font] Failed to bake characters: {missingCharacters}");
            return;
        }

        fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        foreach (Texture2D atlas in fontAsset.atlasTextures)
        {
            atlas.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        }

        fontAsset.material.name = fontAsset.name + " Material";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath);
        Debug.Log($"[FlowSand Font] Generated {FontAssetPath}");
    }
}
