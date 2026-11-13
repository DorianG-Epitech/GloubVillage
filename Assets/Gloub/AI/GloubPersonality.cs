using UnityEngine;

namespace Gloub.AI
{
    /// <summary>
    /// Four MBTI-like traits, 0..100. Serialized so they can be edited in the Inspector;
    /// VillageBuilder seeds distinct values per Gloub.
    /// </summary>
    public sealed class GloubPersonality : MonoBehaviour
    {
        [Tooltip("0 = Introverti, 100 = Extraverti")]
        [Range(0, 100)] public int Sociabilite = 50;
        [Tooltip("0 = Pragmatique, 100 = Imaginatif")]
        [Range(0, 100)] public int Imagination = 50;
        [Tooltip("0 = Empathique, 100 = Logique")]
        [Range(0, 100)] public int Rationalite = 50;
        [Tooltip("0 = Spontané, 100 = Organisé")]
        [Range(0, 100)] public int Organisation = 50;

        public float S => Sociabilite / 100f;
        public float N => Imagination / 100f;
        public float T => Rationalite / 100f;
        public float J => Organisation / 100f;

        public string Type =>
            (Sociabilite >= 50 ? "E" : "I") +
            (Imagination >= 50 ? "N" : "S") +
            (Rationalite >= 50 ? "T" : "F") +
            (Organisation >= 50 ? "J" : "P");

        public override string ToString() =>
            $"{Type} soc={Sociabilite} ima={Imagination} rat={Rationalite} org={Organisation}";

        /// <summary>Seeded, distinct personalities: 8 different MBTI types, values inside each half.</summary>
        public static int[][] Generate(int count, int seed)
        {
            var rng = new System.Random(seed ^ 0x6A7B);
            var types = new int[16];
            for (int i = 0; i < 16; i++) types[i] = i;
            for (int i = 15; i > 0; i--) { int j = rng.Next(i + 1); (types[i], types[j]) = (types[j], types[i]); }
            var result = new int[count][];
            for (int k = 0; k < count; k++)
            {
                int bits = types[k % 16];
                result[k] = new int[4];
                for (int t = 0; t < 4; t++)
                {
                    bool high = ((bits >> t) & 1) == 1;
                    result[k][t] = high ? rng.Next(58, 97) : rng.Next(4, 43);
                }
            }
            return result;
        }
    }

    /// <summary>Tunables for how traits bend the GOAP. All lerps go from trait=0 to trait=100.</summary>
    public static class PersonalityTuning
    {
        // Sociabilité
        public const float SocialDecayIntro = 0.65f, SocialDecayExtro = 1.45f;   // x base social decay
        public const float SocialWeightIntro = 0.60f, SocialWeightExtro = 0.95f; // Socialiser goal weight
        public const float ChatCostIntro = 2.2f, ChatCostExtro = 0.8f;           // Discuter cost
        public const float ChatTimeIntro = 1.8f, ChatTimeExtro = 4.2f;           // seconds
        public const float DreamCostIntro = 0.9f, DreamCostExtro = 5.5f;         // Rêvasser (recharge alone) cost
        public const float DreamRepeatPenalty = 1.6f;                            // introverts still chat after dreaming
        public const float DreamRestoreIntro = 0.50f, DreamRestoreExtro = 0.12f; // social restored by dreaming

        // Imagination
        public const float WanderRadiusPrag = 0.7f, WanderRadiusImag = 1.7f;     // x wander radius
        public const float ContemplateCostPrag = 3.4f, ContemplateCostImag = 0.9f;
        public const float CostNoiseImag = 1.3f;                                 // ± random cost jitter per plan
        public const float OtherBushChanceImag = 0.6f;                           // pick a non-nearest bush

        // Rationalité
        public const float DistWeightEmpath = 0.05f, DistWeightLogic = 0.18f;    // cost per metre
        public const float FriendGainEmpath = 1.7f, FriendGainLogic = 0.8f;      // x friendship gain
        public const float HelpWeightEmpath = 1.4f;                              // Aider goal weight * (1-T)
        public const float NeedyHunger = 0.58f, NeedySocial = 0.50f;             // a friend below this is "in need"
        public const float HelpCooldown = 18f;

        // Organisation
        public const float ThresholdShift = 0.36f;   // organised act this much earlier (need value), spontaneous later
        public const float MarginSpont = 0.08f, MarginOrg = 0.38f; // replan interrupt margin
        public const float StockPriority = 0.38f;    // organised keep a fruit in stock
        public const float RegularSleepBonus = 0.12f;
    }
}
