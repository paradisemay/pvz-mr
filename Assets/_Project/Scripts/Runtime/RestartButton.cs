using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>"New shift" button: grab/poke/select it to reload the scene.</summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class RestartButton : MonoBehaviour
    {
        private XRSimpleInteractable _interactable;

        private void Awake() => _interactable = GetComponent<XRSimpleInteractable>();
        private void OnEnable() => _interactable.selectEntered.AddListener(OnSelected);
        private void OnDisable() => _interactable.selectEntered.RemoveListener(OnSelected);

        private void OnSelected(SelectEnterEventArgs args) => Restart();

        public void Restart()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }
    }
}
