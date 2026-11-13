using UnityEngine;

namespace Gloub.Village
{
    /// <summary>
    /// Fruit bush with a small stock that depletes when picked and regrows over time.
    /// Agents reserve a fruit on pick-start so two planners cannot claim the last berry.
    /// </summary>
    public sealed class FruitBush : VillageSpot
    {
        public int MaxFruit = 2;
        public float RegenSeconds = 16f;
        [SerializeField] int fruit = 2;
        [SerializeField] Transform[] fruitVisuals;

        public int Fruit => fruit;
        public bool HasFruit => fruit > 0;

        float _regen;

        public void Configure(int maxFruit, float regenSeconds, Transform[] visuals)
        {
            MaxFruit = maxFruit;
            RegenSeconds = regenSeconds;
            fruitVisuals = visuals;
            fruit = maxFruit;
            _regen = 0f;
            RefreshVisuals();
        }

        void Awake()
        {
            if (fruit <= 0) fruit = MaxFruit;
            RefreshVisuals();
        }

        public bool TryPick()
        {
            if (fruit <= 0) return false;
            fruit--;
            _regen = 0f;
            RefreshVisuals();
            return true;
        }

        void Update()
        {
            if (fruit >= MaxFruit) return;
            _regen += Time.deltaTime;
            if (_regen >= RegenSeconds)
            {
                _regen = 0f;
                fruit++;
                RefreshVisuals();
            }
        }

        void RefreshVisuals()
        {
            if (fruitVisuals == null) return;
            for (int i = 0; i < fruitVisuals.Length; i++)
            {
                if (fruitVisuals[i] != null)
                    fruitVisuals[i].gameObject.SetActive(i < fruit);
            }
        }
    }
}
