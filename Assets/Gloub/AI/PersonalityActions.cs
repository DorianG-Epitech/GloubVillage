using Gloub.AI.GOAP;
using UnityEngine;

namespace Gloub.AI
{
    public static class PFacts
    {
        public const string HelpedFood = "HelpedFood";
        public const string HelpedMood = "HelpedMood";
    }

    /// <summary>Introvert recharge: walk to a quiet spot and daydream alone. Satisfies IsSocial.</summary>
    public sealed class DreamAloneAction : GoapAction
    {
        float _timer;
        Vector3 _spot;
        bool _picked;

        public DreamAloneAction()
        {
            Name = "Rêvasser";
            ShortLabel = "rêvasse seul";
            Effects.Set(GoapFacts.IsSocial, 1);
            GoapFacts.SetExclusiveLocation(Effects, "");
        }

        public override float ArriveRadius => 0.9f;

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || v.Personality == null) return 5f;
            return Mathf.Lerp(PersonalityTuning.DreamCostIntro, PersonalityTuning.DreamCostExtro, v.Personality.S)
                   + v.CostNoise(Name) + (v.LastSocialWasDream ? PersonalityTuning.DreamRepeatPenalty : 0f);
        }

        public override void ResetRuntime() { base.ResetRuntime(); _timer = 0f; _picked = false; }

        public override Vector3 GetTarget(GoapAgent agent)
        {
            if (!_picked)
            {
                var v = agent.GetComponent<GloubVillager>();
                _spot = v != null ? v.QuietSpot() : agent.transform.position;
                _picked = true;
            }
            return _spot;
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null) return true;
            float total = Mathf.Lerp(PersonalityTuning.DreamRestoreIntro, PersonalityTuning.DreamRestoreExtro, v.Personality.S);
            v.Needs.RestoreSocial(total / 3.2f * dt);
            _timer += dt;
            if (_timer >= 3.2f) { v.LastSocialWasDream = true; return true; }
            return false;
        }
    }

    /// <summary>Imaginative idle: go look at the campfire / clouds somewhere. Satisfies Idled.</summary>
    public sealed class ContemplateAction : GoapAction
    {
        float _timer;
        Vector3 _spot;
        bool _picked;

        public ContemplateAction()
        {
            Name = "Contempler";
            ShortLabel = "contemple";
            Effects.Set(GoapFacts.Idled, 1);
        }

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || v.Personality == null) return 4f;
            return Mathf.Lerp(PersonalityTuning.ContemplateCostPrag, PersonalityTuning.ContemplateCostImag, v.Personality.N)
                   + v.CostNoise(Name);
        }

        public override void ResetRuntime() { base.ResetRuntime(); _timer = 0f; _picked = false; }

        public override Vector3 GetTarget(GoapAgent agent)
        {
            if (!_picked)
            {
                var v = agent.GetComponent<GloubVillager>();
                _spot = v != null ? v.ContemplateSpot() : agent.transform.position;
                _picked = true;
            }
            return _spot;
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            var v = agent.GetComponent<GloubVillager>();
            _timer += dt;
            return _timer >= Mathf.Lerp(2f, 4.5f, v != null && v.Personality != null ? v.Personality.N : 0.5f);
        }
    }

    /// <summary>Empathic: bring a picked fruit to a hungry friend.</summary>
    public sealed class OfferFruitAction : GoapAction
    {
        float _timer;

        public OfferFruitAction()
        {
            Name = "OffrirFruit";
            ShortLabel = "offre un fruit";
            Preconditions.Set(GoapFacts.HasFood, 1);
            Effects.Set(PFacts.HelpedFood, 1);
            Effects.Set(GoapFacts.HasFood, 0);
            GoapFacts.SetExclusiveLocation(Effects, GoapFacts.NearFriend);
        }

        public override float ArriveRadius => 1.4f;

        public override bool CheckProcedural(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && v.HelpTarget != null;
        }

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || v.HelpTarget == null) return 9f;
            return 1f + v.DistanceWeight * VillageActionUtil.Horizontal(agent.transform.position, v.HelpTarget.transform.position);
        }

        public override Vector3 GetTarget(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && v.HelpTarget != null ? v.HelpTarget.transform.position : agent.transform.position;
        }

        public override void ResetRuntime() { base.ResetRuntime(); _timer = 0f; }

        public override void OnEnter(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || v.HelpTarget == null || !v.HasFood) HasFailed = true;
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            _timer += dt;
            return _timer >= 1.2f;
        }

        public override void OnExit(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || HasFailed || v.HelpTarget == null || !v.HasFood) return;
            v.HasFood = false;
            v.HelpTarget.Needs.RestoreHunger(0.5f);
            v.AddFriendship(v.HelpTarget, 0.14f);
            v.FinishHelp($"offre un fruit à {v.HelpTarget.DisplayName}");
        }
    }

    /// <summary>Empathic: go comfort a lonely friend (both get social, bigger friendship gain).</summary>
    public sealed class ComfortAction : GoapAction
    {
        float _timer;
        GloubVillager _target;

        public ComfortAction()
        {
            Name = "Réconforter";
            ShortLabel = "réconforte";
            Effects.Set(PFacts.HelpedMood, 1);
            GoapFacts.SetExclusiveLocation(Effects, GoapFacts.NearFriend);
        }

        public override float ArriveRadius => 0.55f;

        public override bool CheckProcedural(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && v.HelpTarget != null && !v.HelpTarget.Needs.Sleeping;
        }

        public override float GetCost(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || v.HelpTarget == null) return 9f;
            return 1.2f + v.DistanceWeight * VillageActionUtil.Horizontal(agent.transform.position, v.HelpTarget.transform.position);
        }

        public override Vector3 GetTarget(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            return v != null && v.HelpTarget != null ? v.HelpTarget.ApproachPointFor(v) : agent.transform.position;
        }

        public override void ResetRuntime() { base.ResetRuntime(); _timer = 0f; _target = null; }

        public override void OnEnter(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            _target = v != null ? v.HelpTarget : null;
            if (_target == null) { HasFailed = true; return; }
            v.BeginChat(_target);
        }

        public override bool Perform(GoapAgent agent, float dt)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null || _target == null) { HasFailed = true; return true; }
            _target.Needs.RestoreSocial(0.14f * dt);
            v.Needs.RestoreSocial(0.05f * dt);
            _timer += dt;
            return _timer >= 2.4f;
        }

        public override void OnExit(GoapAgent agent)
        {
            var v = agent.GetComponent<GloubVillager>();
            if (v == null) return;
            v.EndChat(_target, !HasFailed);
            if (!HasFailed && _target != null)
            {
                v.AddFriendship(_target, 0.10f);
                v.FinishHelp($"réconforte {_target.DisplayName}");
            }
        }
    }
}
