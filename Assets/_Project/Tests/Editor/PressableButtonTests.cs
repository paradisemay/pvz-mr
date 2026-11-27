using NUnit.Framework;
using Pvz.Runtime;

namespace Pvz.Editor.Tests
{
    /// <summary>T031 (#74): buttons are pressed by the trigger or a poke, never by grip.</summary>
    public class PressableButtonTests
    {
        [Test]
        public void Trigger_PressesWithAnEmptyHand()
        {
            Assert.IsTrue(PressableButton.Accepts(PressableButton.Source.RayTrigger, handHoldsSomething: false));
        }

        [Test]
        public void Trigger_WhileHoldingTheScanner_DoesNotPress()
        {
            Assert.IsFalse(PressableButton.Accepts(PressableButton.Source.RayTrigger, handHoldsSomething: true));
        }

        [Test]
        public void Grip_NeverPresses()
        {
            Assert.IsFalse(PressableButton.Accepts(PressableButton.Source.RayGrip, false));
            Assert.IsFalse(PressableButton.Accepts(PressableButton.Source.RayGrip, true));
        }

        [Test]
        public void Poke_Presses()
        {
            Assert.IsTrue(PressableButton.Accepts(PressableButton.Source.Poke, false));
        }
    }
}
