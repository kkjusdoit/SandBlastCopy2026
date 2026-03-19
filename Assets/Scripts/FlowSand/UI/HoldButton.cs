using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FlowSand.UI
{
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float repeatDelay = 0.22f;
        [SerializeField] private float repeatInterval = 0.09f;

        private bool isHeld;
        private float nextRepeatAt;

        public Action OnPressed { get; set; }
        public Action OnReleased { get; set; }
        public Action OnRepeated { get; set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            isHeld = true;
            nextRepeatAt = Time.unscaledTime + repeatDelay;
            OnPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void Update()
        {
            if (!isHeld || OnRepeated == null)
            {
                return;
            }

            if (Time.unscaledTime < nextRepeatAt)
            {
                return;
            }

            OnRepeated.Invoke();
            nextRepeatAt = Time.unscaledTime + repeatInterval;
        }

        private void Release()
        {
            if (!isHeld)
            {
                return;
            }

            isHeld = false;
            OnReleased?.Invoke();
        }
    }
}
