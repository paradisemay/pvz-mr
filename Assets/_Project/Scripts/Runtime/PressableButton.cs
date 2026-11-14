using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Pvz.Runtime
{
    /// <summary>
    /// A button on the counter (T031, #74). Pressed by the <b>trigger</b> while a ray points at it, or by poking it with a
    /// hand or controller up close. Grip does nothing: before, buttons fired on releasing a grip, which players read as
    /// "grab instead of click". A ray whose hand already holds something (the scanner) does not press buttons, so pulling
    /// the trigger to scan never presses a button behind the box.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class PressableButton : MonoBehaviour
    {
        [SerializeField, Min(0)] private float repeatDelaySeconds = 0.35f;
        [SerializeField] private Transform cap;
        [SerializeField] private Renderer highlight;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private XRSimpleInteractable _interactable;
        private float _lastPress = float.NegativeInfinity;
        private Vector3 _capRest;
        private float _pressedUntil;
        private MaterialPropertyBlock _block;

        /// <summary>Raised once per press.</summary>
        public event Action Pressed;

        /// <summary>Who may press: a poke always; a ray only with the trigger and an empty hand.</summary>
        public enum Source { Poke, RayTrigger, RayGrip }

        public static bool Accepts(Source source, bool handHoldsSomething) => source switch
        {
            Source.Poke => true,
            Source.RayTrigger => !handHoldsSomething,
            _ => false,
        };

        public bool IsHovered => _interactable != null && _interactable.isHovered;

        private void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            if (cap == null && transform.childCount > 0) cap = transform.GetChild(0);
            if (cap != null) _capRest = cap.localPosition;
            if (highlight == null && cap != null) highlight = cap.GetComponent<Renderer>();
        }

        private void OnEnable() => _interactable.selectExited.AddListener(OnSelectExited);

        private void OnDisable()
        {
            _interactable.selectExited.RemoveListener(OnSelectExited);
            if (cap != null) cap.localPosition = _capRest;
            SetHighlight(false);
        }

        private void Update()
        {
            foreach (var hovering in _interactable.interactorsHovering)
            {
                if (hovering is XRPokeInteractor) continue; // pokes press through select
                if (!(hovering is XRBaseInputInteractor input) || !input.activateInput.ReadWasPerformedThisFrame()) continue;
                if (Accepts(Source.RayTrigger, input.hasSelection)) { PressFromInput(); break; }
            }
            SetHighlight(IsHovered);
            if (cap != null) cap.localPosition = _capRest + (Time.unscaledTime < _pressedUntil ? Vector3.down * 0.012f : Vector3.zero);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.isCanceled) return;
            var source = args.interactorObject is XRPokeInteractor ? Source.Poke : Source.RayGrip;
            if (Accepts(source, false)) PressFromInput();
        }

        /// <summary>Presses the button (also used by tests and the debug remote; no repeat guard).</summary>
        public void Press()
        {
            _pressedUntil = Time.unscaledTime + 0.15f;
            Pressed?.Invoke();
        }

        /// <summary>A press from a controller: one trigger pull or poke counts once.</summary>
        private void PressFromInput()
        {
            if (Time.unscaledTime - _lastPress < repeatDelaySeconds) return;
            _lastPress = Time.unscaledTime;
            Press();
        }

        private void SetHighlight(bool on)
        {
            if (highlight == null) return;
            _block ??= new MaterialPropertyBlock();
            highlight.GetPropertyBlock(_block);
            if (on) _block.SetColor(BaseColor, Color.Lerp(highlight.sharedMaterial != null ? highlight.sharedMaterial.color : Color.white, Color.white, 0.35f));
            else _block.Clear();
            highlight.SetPropertyBlock(_block);
        }
    }
}
