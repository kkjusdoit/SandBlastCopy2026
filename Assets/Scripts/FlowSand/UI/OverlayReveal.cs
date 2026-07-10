using System.Collections;
using UnityEngine;

namespace FlowSand.UI
{
    [DisallowMultipleComponent]
    public sealed class OverlayReveal : MonoBehaviour
    {
        [SerializeField] private float duration = 0.18f;
        [SerializeField, Range(0.8f, 1f)] private float startScale = 0.94f;

        private CanvasGroup canvasGroup;
        private Coroutine revealRoutine;
        private Vector3 restingScale;

        public bool AnimationsEnabled { get; set; } = true;

        private void Awake()
        {
            restingScale = transform.localScale;
            canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        public void Show()
        {
            gameObject.SetActive(true);

            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
            }

            if (!AnimationsEnabled || duration <= 0f)
            {
                canvasGroup.alpha = 1f;
                transform.localScale = restingScale;
                return;
            }

            revealRoutine = StartCoroutine(Reveal());
        }

        public void Hide()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }

            canvasGroup.alpha = 1f;
            transform.localScale = restingScale;
            gameObject.SetActive(false);
        }

        private IEnumerator Reveal()
        {
            float startedAt = Time.unscaledTime;
            canvasGroup.alpha = 0f;
            transform.localScale = restingScale * startScale;

            while (true)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 4f);
                canvasGroup.alpha = eased;
                transform.localScale = Vector3.LerpUnclamped(restingScale * startScale, restingScale, eased);

                if (progress >= 1f)
                {
                    break;
                }

                yield return null;
            }

            revealRoutine = null;
        }
    }
}
