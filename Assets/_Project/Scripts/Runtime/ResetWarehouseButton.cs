using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Pvz.Runtime
{
    /// <summary>
    /// Start-menu button «Сбросить склад»: puts the boxes back where the game first placed them and forgets the saved
    /// arrangement (when the room changed and the boxes now stand in furniture or in the way).
    /// </summary>
    [RequireComponent(typeof(PressableButton))]
    public sealed class ResetWarehouseButton : MonoBehaviour
    {
        [SerializeField] private WarehouseLayoutKeeper keeper;

        private PressableButton _button;

        private void Awake() => _button = GetComponent<PressableButton>();
        private void OnEnable() => _button.Pressed += OnPressed;
        private void OnDisable() => _button.Pressed -= OnPressed;

        private void OnPressed() => Press();

        public void Press()
        {
            if (keeper != null) keeper.ResetLayout();
        }

        internal void Configure(WarehouseLayoutKeeper layoutKeeper) => keeper = layoutKeeper;
    }
}
