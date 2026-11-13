using UnityEngine;

namespace Gloub.AI
{
    /// <summary>
    /// Symmetric per-pair affinity in 0..1. Grows when two Gloubs chat or play together.
    /// </summary>
    public sealed class FriendshipSystem
    {
        float[,] _matrix;
        int _count;

        public void Init(int count, int seed)
        {
            _count = count;
            _matrix = new float[count, count];
            var rng = new System.Random(seed ^ 0x51F3);
            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    float v = 0.08f + (float)rng.NextDouble() * 0.28f;
                    _matrix[a, b] = v;
                    _matrix[b, a] = v;
                }
            }
        }

        public float Get(int a, int b)
        {
            if (_matrix == null || a == b || a < 0 || b < 0 || a >= _count || b >= _count) return 0f;
            return _matrix[a, b];
        }

        public void Add(int a, int b, float amount)
        {
            if (_matrix == null || a == b || a < 0 || b < 0 || a >= _count || b >= _count) return;
            float v = Mathf.Clamp01(_matrix[a, b] + amount);
            _matrix[a, b] = v;
            _matrix[b, a] = v;
        }

        public int BestFriendOf(int a, System.Func<int, bool> eligible)
        {
            int best = -1;
            float bestV = -1f;
            for (int b = 0; b < _count; b++)
            {
                if (b == a) continue;
                if (eligible != null && !eligible(b)) continue;
                float v = _matrix[a, b];
                if (v > bestV)
                {
                    bestV = v;
                    best = b;
                }
            }
            return best;
        }
    }
}
