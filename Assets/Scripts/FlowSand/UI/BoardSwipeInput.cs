using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlowSand.UI
{
    public sealed class BoardSwipeInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float MinimumSwipePixels = 40f;
        private const float MinimumSwipeInches = 0.18f;
        private const float IndicatorRadius = 58f;

        private Vector2 pointerDownPosition;
        private Vector2 indicatorOrigin;
        private int activePointerId = int.MinValue;
        private RectTransform indicator;
        private RectTransform knob;
        private float nextTriggerTime;
        private int currentDirection; // 0: None, 1: Left, 2: Right, 3: Up, 4: Down

        public Action OnSwipeLeft { get; set; }
        public Action OnSwipeRight { get; set; }
        public Action OnSwipeUp { get; set; }
        public Action OnSwipeDown { get; set; }

        private void Awake()
        {
            Texture2D circle = CreateCircleTexture();
            indicator = CreateCircle("Swipe Indicator", transform, circle, 150f, new Color(0.24f, 0.79f, 1f, 0.22f));
            knob = CreateCircle("Knob", indicator, circle, 58f, new Color(0.95f, 0.98f, 1f, 0.62f));
            indicator.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != int.MinValue)
            {
                return;
            }

            activePointerId = eventData.pointerId;
            pointerDownPosition = eventData.position;
            RectTransform boardRect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, eventData.position, eventData.pressEventCamera, out indicatorOrigin);
            indicator.anchoredPosition = indicatorOrigin;
            knob.anchoredPosition = Vector2.zero;
            indicator.gameObject.SetActive(true);
            currentDirection = 0;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            RectTransform boardRect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, eventData.position, eventData.pressEventCamera, out Vector2 localPosition);
            knob.anchoredPosition = Vector2.ClampMagnitude(localPosition - indicatorOrigin, IndicatorRadius);
        }

        private void Update()
        {
            if (activePointerId == int.MinValue)
            {
                currentDirection = 0;
                return;
            }

            Vector2 offset = knob.anchoredPosition;
            if (offset.magnitude < DeadZoneDistance())
            {
                currentDirection = 0;
                return;
            }

            int targetDirection;
            if (Mathf.Abs(offset.x) >= Mathf.Abs(offset.y))
            {
                targetDirection = offset.x < 0f ? 1 : 2;
            }
            else
            {
                targetDirection = offset.y > 0f ? 3 : 4;
            }

            if (targetDirection != currentDirection)
            {
                TriggerAction(targetDirection);
                currentDirection = targetDirection;
                nextTriggerTime = Time.time + 0.25f; // Initial DAS delay (250ms)
            }
            else
            {
                // Auto-repeat Left (1), Right (2) and Down (4)
                if (currentDirection != 3) // Skip Up (Rotate) auto-repeat to prevent accidental multiple rotations
                {
                    if (Time.time >= nextTriggerTime)
                    {
                        TriggerAction(currentDirection);
                        nextTriggerTime = Time.time + 0.12f; // Auto-repeat interval ARR (120ms)
                    }
                }
            }
        }

        private void TriggerAction(int direction)
        {
            switch (direction)
            {
                case 1: OnSwipeLeft?.Invoke(); break;
                case 2: OnSwipeRight?.Invoke(); break;
                case 3: OnSwipeUp?.Invoke(); break;
                case 4: OnSwipeDown?.Invoke(); break;
            }
        }

        private float DeadZoneDistance()
        {
            float dpiThreshold = Screen.dpi > 0f ? Screen.dpi * MinimumSwipeInches : 0f;
            return Mathf.Max(MinimumSwipePixels, dpiThreshold) * 0.5f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            activePointerId = int.MinValue;
            currentDirection = 0;
            indicator.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            activePointerId = int.MinValue;
            currentDirection = 0;
            if (indicator != null) indicator.gameObject.SetActive(false);
        }

        private static RectTransform CreateCircle(string name, Transform parent, Texture texture, float size, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(size, size);
            RawImage image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Texture2D CreateCircleTexture()
        {
            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(32f - distance));
                    pixels[(y * size) + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
