using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// "New shift" button: select it (grab/poke/trigger) to reload the scene. It arms itself a moment after
    /// appearing so a hand that happens to be over it does not restart the shift by accident.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class RestartButton : MonoBehaviour
    {
        [SerializeField, Min(0)] private float armDelaySeconds = 1f;

        private XRSimpleInteractable _interactable;
        private float _armedAt;

        private void Awake() => _interactable = GetComponent<XRSimpleInteractable>();

        private void OnEnable()
        {
            _armedAt = Time.unscaledTime + armDelaySeconds;
            _interactable.selectExited.AddListener(OnReleased);
        }

        private void OnDisable() => _interactable.selectExited.RemoveListener(OnReleased);

        private void OnReleased(SelectExitEventArgs args)
        {
            if (args.isCanceled || Time.unscaledTime < _armedAt) return;
            Restart();
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }
}
