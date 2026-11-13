using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// Start-menu button «Сбросить склад»: puts the boxes back where the game first placed them and forgets the saved
    /// arrangement (when the room changed and the boxes now stand in furniture or in the way).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class ResetWarehouseButton : MonoBehaviour
    {
        [SerializeField] private WarehouseLayoutKeeper keeper;

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
            if (keeper != null) keeper.ResetLayout();
        }

        internal void Configure(WarehouseLayoutKeeper layoutKeeper) => keeper = layoutKeeper;
    }
}
