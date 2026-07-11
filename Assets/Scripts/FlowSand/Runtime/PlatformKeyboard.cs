using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace FlowSand.Runtime
{
    /// <summary>
    /// Bridges WeChat PC keyboard events to Unity-style key polling.
    /// </summary>
    internal sealed class PlatformKeyboard : IDisposable
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        private static readonly Dictionary<string, KeyCode> WebKeyCodes = new(StringComparer.Ordinal)
        {
            { "ArrowLeft", KeyCode.LeftArrow },
            { "ArrowRight", KeyCode.RightArrow },
            { "ArrowUp", KeyCode.UpArrow },
            { "ArrowDown", KeyCode.DownArrow },
            { "Enter", KeyCode.Return },
            { "NumpadEnter", KeyCode.KeypadEnter },
            { "Space", KeyCode.Space },
            { "KeyA", KeyCode.A },
            { "KeyD", KeyCode.D },
            { "KeyP", KeyCode.P },
            { "KeyS", KeyCode.S },
            { "KeyW", KeyCode.W },
        };

        private readonly HashSet<KeyCode> heldKeys = new();
        private readonly HashSet<KeyCode> pressedKeys = new();
        private readonly Action<OnKeyDownListenerResult> keyDownHandler;
        private readonly Action<OnKeyDownListenerResult> keyUpHandler;
#endif

        public PlatformKeyboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            keyDownHandler = HandleKeyDown;
            keyUpHandler = HandleKeyUp;
            WX.OnKeyDown(keyDownHandler);
            WX.OnKeyUp(keyUpHandler);
#endif
        }

        public bool GetKeyDown(KeyCode key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return pressedKeys.Contains(key);
#else
            return Input.GetKeyDown(key);
#endif
        }

        public bool GetKey(KeyCode key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return heldKeys.Contains(key);
#else
            return Input.GetKey(key);
#endif
        }

        public void EndFrame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            pressedKeys.Clear();
#endif
        }

        public void Dispose()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WX.OffKeyDown(keyDownHandler);
            WX.OffKeyUp(keyUpHandler);
            heldKeys.Clear();
            pressedKeys.Clear();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void HandleKeyDown(OnKeyDownListenerResult result)
        {
            if (!TryGetKeyCode(result.code, out KeyCode key))
            {
                return;
            }

            if (heldKeys.Add(key))
            {
                pressedKeys.Add(key);
            }
        }

        private void HandleKeyUp(OnKeyDownListenerResult result)
        {
            if (TryGetKeyCode(result.code, out KeyCode key))
            {
                heldKeys.Remove(key);
            }
        }

        private static bool TryGetKeyCode(string code, out KeyCode key)
        {
            key = KeyCode.None;
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            if (!WebKeyCodes.TryGetValue(code, out KeyCode mappedKey))
            {
                return false;
            }

            key = mappedKey;
            return true;
        }
#endif
    }
}
