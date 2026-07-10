using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class FixUnity6WebGLBug
{
    static FixUnity6WebGLBug()
    {
        string currentArgs = PlayerSettings.WebGL.emscriptenArgs;
        Debug.Log("Current WebGL Emscripten Args: " + currentArgs);

        if (!currentArgs.Contains("_malloc"))
        {
            string target = "-s EXPORTED_FUNCTIONS=";
            int index = currentArgs.IndexOf(target);
            if (index != -1)
            {
                int endOfParam = currentArgs.IndexOf(" ", index + target.Length);
                if (endOfParam == -1) endOfParam = currentArgs.Length;
                
                string functionsStr = currentArgs.Substring(index + target.Length, endOfParam - (index + target.Length));
                string newFunctionsStr = "_malloc,_free," + functionsStr;
                
                currentArgs = currentArgs.Replace(target + functionsStr, target + newFunctionsStr);
            }
            else
            {
                currentArgs += " -s EXPORTED_FUNCTIONS=_malloc,_free";
            }

            PlayerSettings.WebGL.emscriptenArgs = currentArgs;
            AssetDatabase.SaveAssets();
            Debug.Log("Updated WebGL Emscripten Args: " + currentArgs);
        }
    }
}
