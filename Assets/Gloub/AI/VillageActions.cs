using Gloub.AI.GOAP;
using Gloub.Village;
using UnityEngine;

namespace Gloub.AI
{
    public sealed class GoToWaterAction : GoapAction
    {
        public GoToWaterAction()
        {
            Name = "AllerAuPuits";
            ShortLabel = "va boire";
            BaseCost = 1.1f;
            GoapFacts.SetExclusiveLocation(Effects, GoapFacts.AtWater);
        }

        public override float ArriveRadius => 1.15f;

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            var well = v != null ? v.NearestWater() : null;
            if (well == null) return 8f;
            return BaseCost + v.DistanceWeight * VillageActionUtil.Horizontal(agent.transform.position, well.ApproachPoint);
        }

        public override bool CheckProcedural(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && v.NearestWater() != null;
        }

        public override Vector3 GetTarget(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            var well = v != null ? v.NearestWater() : null;
            return well != null ? well.ApproachPoint : agent.transform.position;
        }
    }

    public sealed class DrinkAction : GoapAction
    {
        float _timer;

        public DrinkAction()
        {
            Name = "Boire";
            ShortLabel = "boit";
            BaseCost = 1f;
            RequiresInRangeOverride = false;
            Preconditions.Set(GoapFacts.AtWater, 1);
            Effects.Set(GoapFacts.IsHydrated, 1);
            Effects.Set(GoapFacts.AtWater, 1);
        }

        bool RequiresInRangeOverride;
        public override bool RequiresInRange => RequiresInRangeOverride;

        public override void ResetRuntime()
        {
            base.ResetRuntime();
            _timer = 0f;
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            _timer += dt;
            return _timer >= 1.7f;
        }

        public override void OnExit(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v != null) v.Needs.RestoreThirst(0.62f);
        }
    }

    public sealed class GoToBushAction : GoapAction
    {
        public GoToBushAction()
        {
            Name = "AllerAuBuisson";
            ShortLabel = "va cueillir";
            BaseCost = 1.15f;
            Preconditions.Set(GoapFacts.FruitAvailable, 1);
            GoapFacts.SetExclusiveLocation(Effects, GoapFacts.AtBush);
            Effects.Set(GoapFacts.FruitAvailable, 1);
        }

        public override float ArriveRadius => 1.05f;

        public override bool CheckProcedural(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && v.NearestStockedBush() != null;
        }

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            var bush = v != null ? v.ChosenBush() : null;
            if (bush == null) return 10f;
            return BaseCost + v.DistanceWeight * VillageActionUtil.Horizontal(agent.transform.position, bush.ApproachPoint) + v.CostNoise(Name);
        }

        public override Vector3 GetTarget(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            var bush = v != null ? v.ChosenBush() : null;
            return bush != null ? bush.ApproachPoint : agent.transform.position;
        }
    }

    public sealed class PickFruitAction : GoapAction
    {
        float _timer;
        bool _picked;
        FruitBush _bush;

        public PickFruitAction()
        {
            Name = "Cueillir";
            ShortLabel = "cueille";
            BaseCost = 1f;
            Preconditions.Set(GoapFacts.AtBush, 1);
            Preconditions.Set(GoapFacts.FruitAvailable, 1);
            Effects.Set(GoapFacts.HasFood, 1);
            Effects.Set(GoapFacts.AtBush, 1);
        }

        public override bool RequiresInRange => false;

        public override bool CheckProcedural(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && (v.HasFood || v.NearestStockedBush() != null || v.BushAtFeet() != null);
        }

        public override void ResetRuntime()
        {
            base.ResetRuntime();
            _timer = 0f;
            _picked = false;
            _bush = null;
        }

        public override void OnEnter(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null) { HasFailed = true; return; }
            if (v.HasFood) { _picked = true; return; }
            _bush = v.BushAtFeet() ?? v.ChosenBush();
            if (_bush == null || !_bush.TryPick())
            {
                HasFailed = true;
                return;
            }
            _picked = true;
            v.HasFood = true;
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            _timer += dt;
            return _timer >= 1.15f;
        }

        public override void OnExit(GoapAgent agent)
        {
            if (!_picked || HasFailed)
            {
                var v = agent.GetComponent<GloubVillager>();
                // fruit already reserved; keep it if we succeeded the pick
            }
        }
    }

    public sealed class EatAction : GoapAction
    {
        float _timer;

        public EatAction()
        {
            Name = "Manger";
            ShortLabel = "mange";
            BaseCost = 1f;
            Preconditions.Set(GoapFacts.HasFood, 1);
            Effects.Set(GoapFacts.IsFed, 1);
            Effects.Set(GoapFacts.HasFood, 0);
        }

        public override bool RequiresInRange => false;

        public override void ResetRuntime()
        {
            base.ResetRuntime();
            _timer = 0f;
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            _timer += dt;
            return _timer >= 1.9f;
        }

        public override void OnExit(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null) return;
            if (v.HasFood)
            {
                v.HasFood = false;
                v.Needs.RestoreHunger(0.58f);
            }
        }
    }

    public sealed class GoHomeAction : GoapAction
    {
        public GoHomeAction()
        {
            Name = "Rentrer";
            ShortLabel = "rentre";
            BaseCost = 1.1f;
            GoapFacts.SetExclusiveLocation(Effects, GoapFacts.AtHome);
        }

        public override float ArriveRadius => 0.95f;

        public override bool CheckProcedural(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && v.Home != null;
        }

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || v.Home == null) return 8f;
            return BaseCost + v.DistanceWeight * VillageActionUtil.Horizontal(agent.transform.position, v.Home.ApproachPoint);
        }

        public override Vector3 GetTarget(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && v.Home != null ? v.Home.ApproachPoint : agent.transform.position;
        }
    }

    public sealed class SleepAction : GoapAction
    {
        float _timer;

        public SleepAction()
        {
            Name = "Dormir";
            ShortLabel = "dort";
            BaseCost = 1f;
            Preconditions.Set(GoapFacts.AtHome, 1);
            Effects.Set(GoapFacts.IsRested, 1);
            Effects.Set(GoapFacts.AtHome, 1);
        }

        public override bool RequiresInRange => false;

        public override void ResetRuntime()
        {
            base.ResetRuntime();
            _timer = 0f;
        }

        public override void OnEnter(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v != null) v.Needs.Sleeping = true;
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            _timer += dt;
            var v = agent.GetComponent<GloubVillager>();
            if (v == null) return true;
            return _timer >= 2.2f && v.Needs.Energy >= 0.86f;
        }

        public override void OnExit(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v != null) v.Needs.Sleeping = false;
        }
    }

    public sealed class FindFriendAction : GoapAction
    {
        public FindFriendAction()
        {
            Name = "RejoindreUnAmi";
            ShortLabel = "cherche un ami";
            BaseCost = 1.2f;
            GoapFacts.SetExclusiveLocation(Effects, GoapFacts.NearFriend);
        }

        public override float ArriveRadius => 0.55f;

        public override bool CheckProcedural(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && v.ChooseChatPartner() != null;
        }

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            var p = v != null ? v.ChooseChatPartner() : null;
            if (p == null) return 8f;
            return BaseCost + v.DistanceWeight * VillageActionUtil.Horizontal(agent.transform.position, p.transform.position);
        }

        public override Vector3 GetTarget(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            var p = v != null ? v.ChooseChatPartner() : null;
            if (p != null) return p.ApproachPointFor(v);
            var plaza = v != null ? v.Village.Plaza : null;
            return plaza != null ? plaza.ApproachPoint : agent.transform.position;
        }
    }

    public sealed class ChatAction : GoapAction
    {
        float _timer;
        GloubVillager _partner;

        public ChatAction()
        {
            Name = "Discuter";
            ShortLabel = "discute";
            BaseCost = 1f;
            Preconditions.Set(GoapFacts.NearFriend, 1);
            Effects.Set(GoapFacts.IsSocial, 1);
            Effects.Set(GoapFacts.NearFriend, 1);
        }

        public override bool RequiresInRange => false;
        public override float ArriveRadius => 1.4f;

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || v.Personality == null) return BaseCost;
            return Mathf.Lerp(PersonalityTuning.ChatCostIntro, PersonalityTuning.ChatCostExtro, v.Personality.S) + v.CostNoise(Name);
        }

        public override void ResetRuntime()
        {
            base.ResetRuntime();
            _timer = 0f;
            _partner = null;
        }

        public override void OnEnter(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null) { HasFailed = true; return; }
            var wanted = v.ChooseChatPartner();
            _partner = wanted != null && VillageActionUtil.Horizontal(v.transform.position, wanted.transform.position) < 2.3f
                ? wanted : v.NearbyChatPartner();
            if (_partner == null)
            {
                HasFailed = true;
                return;
            }
            v.BeginChat(_partner);
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || _partner == null)
            {
                HasFailed = true;
                return true;
            }
            if (VillageActionUtil.Horizontal(v.transform.position, _partner.transform.position) > 2.4f)
            {
                HasFailed = true;
                return true;
            }
            v.Needs.RestoreSocial(0.16f * dt);
            _partner.Needs.RestoreSocial(0.12f * dt);
            _timer += dt;
            return _timer >= v.ChatDuration;
        }

        public override void OnExit(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v != null) v.EndChat(_partner, !HasFailed);
        }
    }

    public sealed class WanderAction : GoapAction
    {
        Vector3 _target;
        float _wait;
        bool _picked;

        public WanderAction()
        {
            Name = "Flâner";
            ShortLabel = "flâne";
            BaseCost = 1.4f;
            Effects.Set(GoapFacts.Idled, 1);
        }

        public override float ArriveRadius => 0.8f;

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return BaseCost + (v != null ? v.CostNoise(Name) : 0f);
        }

        public override void ResetRuntime()
        {
            base.ResetRuntime();
            _wait = 0f;
            _picked = false;
        }

        public override Vector3 GetTarget(GoapAgent agent)
        {
            if (!_picked)
            {
                var v = agent.GetComponent<GloubVillager>();
                _target = v != null ? v.RandomWanderPoint() : agent.transform.position;
                _picked = true;
            }
            return _target;
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            _wait += dt;
            return _wait >= 0.7f;
        }
    }

    static class VillageActionUtil
    {
        public static float Horizontal(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
