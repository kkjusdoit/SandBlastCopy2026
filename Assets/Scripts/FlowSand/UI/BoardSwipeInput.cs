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
        public Action OnSwipeDownPress { get; set; }
        public Action OnSwipeDownRelease { get; set; }

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
                if (currentDirection == 4) OnSwipeDownRelease?.Invoke();
                currentDirection = 0;
                return;
            }

            Vector2 offset = knob.anchoredPosition;
            if (offset.magnitude < DeadZoneDistance())
            {
                if (currentDirection == 4) OnSwipeDownRelease?.Invoke();
                currentDirection = 0;
                return;
            }

            int targetDirection = 0;
            if (Mathf.Abs(offset.x) >= Mathf.Abs(offset.y))
            {
                targetDirection = offset.x < 0f ? 1 : 2;
            }
            else
            {
                // For vertical moves (Up/Down), enforce a higher distance threshold and strict angle constraints
                // to prevent accidental rotation triggers during left/right sliding.
                float verticalThreshold = DirectionThreshold(1.6f);
                if (offset.y > 0f)
                {
                    // UP (Rotate) requires dragging the knob significantly upwards and a steep swipe angle
                    if (offset.y >= verticalThreshold && offset.y > Mathf.Abs(offset.x) * 1.5f)
                    {
                        targetDirection = 3;
                    }
                }
                else
                {
                    // DOWN (Soft Drop) requires a much larger distance threshold and very steep downward swipe angle
                    // to prevent accidental acceleration triggers during left/right sliding.
                    float downThreshold = DirectionThreshold(2.2f);
                    if (Mathf.Abs(offset.y) >= downThreshold && Mathf.Abs(offset.y) > Mathf.Abs(offset.x) * 1.8f)
                    {
                        targetDirection = 4;
                    }
                }
            }

            if (targetDirection == 0)
            {
                if (currentDirection == 4) OnSwipeDownRelease?.Invoke();
                currentDirection = 0;
                return;
            }

            if (targetDirection != currentDirection)
            {
                // Exit old direction
                if (currentDirection == 4) OnSwipeDownRelease?.Invoke();

                // Enter new direction
                if (targetDirection == 4) OnSwipeDownPress?.Invoke();
                else TriggerAction(targetDirection);

                currentDirection = targetDirection;
                nextTriggerTime = Time.time + 0.35f; // Slower initial DAS delay (350ms) to make swipes more deliberate
            }
            else
            {
                // Auto-repeat Left (1) and Right (2)
                if (currentDirection == 1 || currentDirection == 2)
                {
                    if (Time.time >= nextTriggerTime)
                    {
                        TriggerAction(currentDirection);
                        nextTriggerTime = Time.time + 0.16f; // Slower auto-repeat interval ARR (160ms) for better precision
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
            }
        }

        private float DeadZoneDistance()
        {
            float dpiThreshold = Screen.dpi > 0f ? Screen.dpi * MinimumSwipeInches : 0f;
            return Mathf.Min(Mathf.Max(MinimumSwipePixels, dpiThreshold) * 0.5f, IndicatorRadius * 0.9f);
        }

        private float DirectionThreshold(float multiplier)
        {
            return Mathf.Min(DeadZoneDistance() * multiplier, IndicatorRadius * 0.9f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            if (currentDirection == 4) OnSwipeDownRelease?.Invoke();
            activePointerId = int.MinValue;
            currentDirection = 0;
            indicator.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (currentDirection == 4) OnSwipeDownRelease?.Invoke();
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
