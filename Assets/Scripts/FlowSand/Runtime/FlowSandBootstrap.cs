using UnityEngine;

namespace FlowSand.Runtime
{
    public static class FlowSandBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Object.FindAnyObjectByType<FlowSandGameController>() != null)
            {
                return;
            }

            GameObject controllerGo = new("FlowSandGame");
            controllerGo.AddComponent<FlowSandGameController>();
        }
    }
}
