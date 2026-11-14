using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>A key of the counter computer that switches its screens or ends the shift (T033).</summary>
    [RequireComponent(typeof(PressableButton))]
    public sealed class ComputerKey : MonoBehaviour
    {
        public enum Action { OpenSettings, Back, EndShift, MoveCounter, ApplyMove, CancelMove, MoveHatch, MoveRack }

        [SerializeField] private PvzComputer computer;
        [SerializeField] private Action action;

        private PressableButton _button;

        public Action Kind => action;

        private void Awake() => _button = GetComponent<PressableButton>();
        private void OnEnable() => _button.Pressed += Press;
        private void OnDisable() => _button.Pressed -= Press;

        public void Press()
        {
            if (computer == null) return;
            switch (action)
            {
                case Action.OpenSettings: computer.OpenSettings(); break;
                case Action.Back: computer.Back(); break;
                case Action.EndShift: computer.EndShift(); break;
                case Action.MoveCounter: computer.StartMovingCounter(); break;
                case Action.MoveHatch: computer.StartMovingHatch(); break;
                case Action.MoveRack: computer.StartMovingRack(); break;
                case Action.ApplyMove: computer.ApplyMove(); break;
                case Action.CancelMove: computer.CancelMove(); break;
            }
        }

        internal void Configure(PvzComputer pvzComputer, Action keyAction)
        {
            computer = pvzComputer;
            action = keyAction;
        }
    }
}
