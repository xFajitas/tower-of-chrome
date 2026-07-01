using UnityEngine;
using UnityEngine.UIElements;

namespace TowerOfChrome.Unity.Views
{
    /// <summary>
    /// Lightweight hit-reaction feedback for Combat: no animated sprite art exists yet (see
    /// docs/art-credits.md), so this shakes the hit row's VisualElement itself via scheduled
    /// style.translate updates rather than a real sprite animation.
    /// </summary>
    public static class CombatFx
    {
        public static void Shake(VisualElement element, float duration = 0.3f, float magnitude = 6f)
        {
            if (element == null)
                return;

            var elapsed = 0f;
            element.schedule.Execute(() =>
            {
                elapsed += 0.016f;
                var t = Mathf.Clamp01(elapsed / duration);
                var damp = 1f - t;
                var offset = Mathf.Sin(t * Mathf.PI * 10f) * magnitude * damp;
                element.style.translate = new StyleTranslate(new Translate(offset, 0));
            }).Every(16).Until(() => elapsed >= duration);

            element.schedule.Execute(() => element.style.translate = new StyleTranslate(new Translate(0, 0)))
                .ExecuteLater((long)(duration * 1000) + 20);
        }
    }
}
