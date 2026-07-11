using System.Collections;
using TMPro;
using UnityEngine;

namespace FlowSand.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class ComboPopup : MonoBehaviour
    {
        private const float EnterDuration = 0.12f;
        private const float HoldDuration = 0.42f;
        private const float ExitDuration = 0.26f;

        private TMP_Text label;
        private Coroutine animationRoutine;
        private Vector3 restingScale;
        private float currentHoldDuration = HoldDuration;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
            restingScale = transform.localScale;
        }

        public void Show(int combo)
        {
            ShowMessage($"连消 ×{combo}", HoldDuration);
        }

        public void ShowMessage(string message, float holdDuration = HoldDuration)
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            label.text = message;
            currentHoldDuration = Mathf.Max(0f, holdDuration);
            gameObject.SetActive(true);
            animationRoutine = StartCoroutine(Animate());
        }

        public void Hide()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            label.alpha = 0f;
            transform.localScale = restingScale;
            gameObject.SetActive(false);
        }

        private IEnumerator Animate()
        {
            float startedAt = Time.unscaledTime;
            label.alpha = 0f;
            transform.localScale = restingScale * 0.72f;

            while (true)
            {
                float elapsed = Time.unscaledTime - startedAt;
                if (elapsed < EnterDuration)
                {
                    float progress = EaseOutBack(elapsed / EnterDuration);
                    label.alpha = Mathf.Clamp01(elapsed / (EnterDuration * 0.65f));
                    transform.localScale = Vector3.LerpUnclamped(restingScale * 0.72f, restingScale, progress);
                }
                else if (elapsed < EnterDuration + currentHoldDuration)
                {
                    label.alpha = 1f;
                    transform.localScale = restingScale;
                }
                else
                {
                    float exitProgress = Mathf.Clamp01((elapsed - EnterDuration - currentHoldDuration) / ExitDuration);
                    label.alpha = 1f - exitProgress;
                    transform.localScale = Vector3.LerpUnclamped(restingScale, restingScale * 1.08f, exitProgress);
                    if (exitProgress >= 1f)
                    {
                        break;
                    }
                }

                yield return null;
            }

            animationRoutine = null;
            gameObject.SetActive(false);
        }

        private static float EaseOutBack(float value)
        {
            float shifted = Mathf.Clamp01(value) - 1f;
            return 1f + (2.70158f * shifted * shifted * shifted) + (1.70158f * shifted * shifted);
        }
    }
}
