using UnityEngine;

namespace Pvz.Runtime
{
    /// <summary>
    /// Visual confirmation (concept: «визуальное и звуковое подтверждение», «VFX при завершении заказов»):
    /// a burst of confetti over the handover zone when an order or a return is done.
    /// </summary>
    public sealed class FeedbackEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem confetti;
        [SerializeField, Min(1)] private int confettiCount = 45;

        /// <summary>How many celebrations were shown (tests: particles are not visible in batch mode).</summary>
        public int Celebrations { get; private set; }

        public void Celebrate()
        {
            if (confetti == null) return;
            confetti.Emit(confettiCount);
            Celebrations++;
        }

        /// <summary>Confetti pieces flying right now (tests).</summary>
        public int ParticlesAlive => confetti != null ? confetti.particleCount : 0;

        internal void Configure(ParticleSystem particles) => confetti = particles;
    }
}
