using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Collections;
using UnityEngine.XR.OpenXR.Features.Meta;
#endif

namespace Pvz.Runtime
{
    /// <summary>
    /// Asks the headset for a smoother display rate. On Quest 3 the game ran at 72 Hz with GPU 23 % and CPU 14 %, so
    /// 90 Hz fits easily; 120 Hz is left alone (battery and heat for little gain in a standing game).
    /// </summary>
    public static class DisplayRate
    {
        public const float Preferred = 90f;

        /// <summary>
        /// The rate to request: <paramref name="preferred"/> if supported, otherwise the highest supported rate below it;
        /// null keeps the current one (nothing supported, or only rates above the preferred one).
        /// </summary>
        public static float? Pick(IReadOnlyList<float> supported, float preferred = Preferred)
        {
            if (supported == null) return null;
            float? best = null;
            foreach (var rate in supported)
            {
                if (Mathf.Abs(rate - preferred) < 0.5f) return rate;
                if (rate < preferred && (best == null || rate > best)) best = rate;
            }
            return best;
        }

        /// <summary>The current display refresh rate, or 0 when there is no XR display.</summary>
        public static float Current
        {
            get
            {
                var display = Display();
                return display != null && display.TryGetDisplayRefreshRate(out var rate) ? rate : 0f;
            }
        }

        private static XRDisplaySubsystem Display()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays) if (d.running) return d;
            return null;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RequestOnStart()
        {
            var go = new GameObject("DisplayRate") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Requester>();
        }

        /// <summary>
        /// Retries for a while: on a cold launch (headset waking, not worn) the OpenXR session may not list rates yet.
        /// </summary>
        private sealed class Requester : MonoBehaviour
        {
            private System.Collections.IEnumerator Start()
            {
                for (var attempt = 1; attempt <= 15; attempt++)
                {
                    if (TryRequest(out var report)) { Debug.Log($"[DisplayRate] {report}"); break; }
                    if (attempt == 15) Debug.LogWarning($"[DisplayRate] gave up: {report}");
                    yield return new WaitForSecondsRealtime(2f);
                }
                Destroy(gameObject);
            }
        }

        private static bool TryRequest(out string report)
        {
            var display = Display();
            if (display == null) { report = "no running XR display"; return false; }
            if (!display.TryGetSupportedDisplayRefreshRates(Allocator.Temp, out var rates)) { report = "no refresh rates yet"; return false; }
            var list = new List<float>(rates.ToArray());
            rates.Dispose();
            var pick = Pick(list);
            if (pick == null) { report = $"supported {string.Join(", ", list)}; keeping the current rate"; return true; }
            var ok = display.TryRequestDisplayRefreshRate(pick.Value);
            report = $"supported {string.Join(", ", list)}; requested {pick.Value} Hz: {ok}";
            return ok;
        }
#endif
    }
}
