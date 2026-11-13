using UnityEngine;

namespace Gloub.AI
{
    /// <summary>
    /// Per-Gloub vitals in 0..1 (1 = satisfied). Each need decays over time;
    /// energy regenerates while Sleeping is true.
    /// </summary>
    public sealed class GloubNeeds
    {
        public float Thirst = 0.72f;
        public float Hunger = 0.72f;
        public float Energy = 0.78f;
        public float Social = 0.70f;

        public float ThirstDecay = 1f / 92f;
        public float HungerDecay = 1f / 125f;
        public float EnergyDecay = 1f / 155f;
        public float SocialDecay = 1f / 105f;
        public float SleepRecover = 0.30f;

        public bool Sleeping;

        public void Tick(float dt)
        {
            Thirst = Mathf.Clamp01(Thirst - ThirstDecay * dt);
            Hunger = Mathf.Clamp01(Hunger - HungerDecay * dt);
            Social = Mathf.Clamp01(Social - SocialDecay * dt);
            if (Sleeping) Energy = Mathf.Clamp01(Energy + SleepRecover * dt);
            else Energy = Mathf.Clamp01(Energy - EnergyDecay * dt);
        }

        public void RestoreThirst(float amount) => Thirst = Mathf.Clamp01(Thirst + amount);
        public void RestoreHunger(float amount) => Hunger = Mathf.Clamp01(Hunger + amount);
        public void RestoreSocial(float amount) => Social = Mathf.Clamp01(Social + amount);

        /// <summary>Urgency in roughly 0..1.7; low until the need drops under ~0.55, then rises sharply.</summary>
        public static float Urgency(float value)
        {
            float deficit = 1f - Mathf.Clamp01(value);
            if (value > 0.58f) return deficit * 0.28f;
            float t = Mathf.InverseLerp(0.58f, 0.02f, value);
            return 0.28f + Mathf.Pow(t, 1.35f) * 1.45f;
        }
    }
}
