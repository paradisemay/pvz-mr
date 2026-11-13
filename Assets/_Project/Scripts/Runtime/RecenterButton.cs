using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// Start-menu button «Поставить стойку перед собой»: moves the whole play area (counter, boxes, customer path)
    /// so it stands in front of where the player is now. Useful when the headset placed it into furniture.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class RecenterButton : MonoBehaviour
    {
        [SerializeField] private MrSessionSetup session;

        private XRSimpleInteractable _interactable;

        private void Awake() => _interactable = GetComponent<XRSimpleInteractable>();
        private void OnEnable() => _interactable.selectExited.AddListener(OnReleased);
        private void OnDisable() => _interactable.selectExited.RemoveListener(OnReleased);

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!args.isCanceled) Press();
        }

        public void Press()
        {
            if (session != null) session.AlignPlayAreaToPlayer();
        }

        internal void Configure(MrSessionSetup mrSession) => session = mrSession;
    }
}
