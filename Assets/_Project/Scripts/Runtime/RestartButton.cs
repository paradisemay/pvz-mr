using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// "New shift" button: press it (trigger or poke) to reload the scene. It arms itself a moment after
    /// appearing so a hand that happens to be over it does not restart the shift by accident.
    /// </summary>
    [RequireComponent(typeof(PressableButton))]
    public sealed class RestartButton : MonoBehaviour
    {
        [SerializeField, Min(0)] private float armDelaySeconds = 1f;

        private PressableButton _button;
        private float _armedAt;

        private void Awake() => _button = GetComponent<PressableButton>();

        private void OnEnable()
        {
            _armedAt = Time.unscaledTime + armDelaySeconds;
            _button.Pressed += OnPressed;
        }

        private void OnDisable() => _button.Pressed -= OnPressed;

        private void OnPressed()
        {
            if (Time.unscaledTime < _armedAt) return;
            Restart();
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }
}
