using System.Collections.Generic;
using Gloub.AI.GOAP;
using Gloub.Village;
using UnityEngine;

namespace Gloub.AI
{
    /// <summary>
    /// Village-specific brain: decaying needs, sensed world state, goal priorities,
    /// and the concrete GOAP action set. The generic GoapAgent does the planning
    /// and execution.
    /// </summary>
    public sealed class GloubVillager : MonoBehaviour
    {
        public string DisplayName;
        public int Id;
        public bool HasFood;
        public HomeBed Home;
        public VillageController Village;
        public GloubNeeds Needs { get; private set; }
        public GoapAgent Agent { get; private set; }

        public bool IsChatting { get; private set; }
        public bool IsAvailableForChat =>
            Needs != null && !Needs.Sleeping && !IsBusyWithVitalAction;

        bool IsBusyWithVitalAction
        {
            get
            {
                var running = Agent != null ? Agent.RunningAction : null;
                if (running == null) return false;
                return running is DrinkAction || running is EatAction || running is SleepAction || running is PickFruitAction
                       || running is DreamAloneAction || running is OfferFruitAction;
            }
        }

        public string CurrentActionLabel => Agent != null ? Agent.RunningLabel : "…";

        readonly List<GoapGoal> _goals = new List<GoapGoal>(5);
        GoapGoal _drinkGoal;
        GoapGoal _eatGoal;
        GoapGoal _sleepGoal;
        GoapGoal _socialGoal;
        GoapGoal _idleGoal;

        GoapGoal _helpGoal;
        GoapGoal _stockGoal;

        public GloubPersonality Personality { get; private set; }
        public GloubVillager HelpTarget { get; private set; }
        float _helpCooldownUntil;
        System.Random _rng;
        readonly Dictionary<string, float> _noise = new Dictionary<string, float>();
        FruitBush _preferredBush;

        // --- stats for the soak report
        public readonly Dictionary<string, int> Starts = new Dictionary<string, int>();
        public readonly Dictionary<string, float> NeedSumAtStart = new Dictionary<string, float>();
        public int HelpsGiven;
        public bool LastSocialWasDream;

        public float DistanceWeight => Personality == null ? 0.12f
            : Mathf.Lerp(PersonalityTuning.DistWeightEmpath, PersonalityTuning.DistWeightLogic, Personality.T);
        public float ChatDuration => Personality == null ? 2.6f
            : Mathf.Lerp(PersonalityTuning.ChatTimeIntro, PersonalityTuning.ChatTimeExtro, Personality.S);

        GloubVillager _stickyPartner;
        float _stickyUntil;
        GloubVillager _chatPartner;

        public void Setup(int id, string displayName, HomeBed home, VillageController village, int seedOffset)
        {
            Id = id;
            DisplayName = displayName;
            Home = home;
            Village = village;
            name = $"Gloub_{displayName}";

            Needs = new GloubNeeds();
            var rng = new System.Random(village.Seed * 7919 + id * 97 + seedOffset);
            _rng = new System.Random(village.Seed * 31 + id * 1013);
            Personality = GetComponent<GloubPersonality>();
            if (Personality == null)
            {
                Personality = gameObject.AddComponent<GloubPersonality>();
                var gen = GloubPersonality.Generate(8, village.Seed)[Mathf.Abs(id) % 8];
                Personality.Sociabilite = gen[0]; Personality.Imagination = gen[1];
                Personality.Rationalite = gen[2]; Personality.Organisation = gen[3];
            }
            Needs.SocialDecay *= Mathf.Lerp(PersonalityTuning.SocialDecayIntro, PersonalityTuning.SocialDecayExtro, Personality.S);
            Needs.Thirst = 0.45f + (float)rng.NextDouble() * 0.50f;
            Needs.Hunger = 0.40f + (float)rng.NextDouble() * 0.52f;
            Needs.Energy = 0.50f + (float)rng.NextDouble() * 0.45f;
            Needs.Social = 0.38f + (float)rng.NextDouble() * 0.50f;

            Agent = GetComponent<GoapAgent>();
            if (Agent == null) Agent = gameObject.AddComponent<GoapAgent>();
            Agent.DisplayName = displayName;
            Agent.AgentId = id;
            Agent.Sense = SenseWorld;
            Agent.EvaluateGoals = EvaluateGoals;
            Agent.Log = village.Log;
            Agent.Actions.Clear();
            Agent.Actions.Add(new GoToWaterAction());
            Agent.Actions.Add(new DrinkAction());
            Agent.Actions.Add(new GoToBushAction());
            Agent.Actions.Add(new PickFruitAction());
            Agent.Actions.Add(new EatAction());
            Agent.Actions.Add(new GoHomeAction());
            Agent.Actions.Add(new SleepAction());
            Agent.Actions.Add(new FindFriendAction());
            Agent.Actions.Add(new ChatAction());
            Agent.Actions.Add(new WanderAction());
            Agent.Actions.Add(new DreamAloneAction());
            Agent.Actions.Add(new ContemplateAction());
            Agent.Actions.Add(new OfferFruitAction());
            Agent.Actions.Add(new ComfortAction());
            Agent.InterruptMargin = Mathf.Lerp(PersonalityTuning.MarginSpont, PersonalityTuning.MarginOrg, Personality.J);
            Agent.BeforePlan = RollPlanNoise;
            Agent.ActionStarted = RecordStart;

            _drinkGoal = new GoapGoal("Boire");
            _drinkGoal.Desired.Set(GoapFacts.IsHydrated, 1);
            _eatGoal = new GoapGoal("Manger");
            _eatGoal.Desired.Set(GoapFacts.IsFed, 1);
            _sleepGoal = new GoapGoal("Dormir");
            _sleepGoal.Desired.Set(GoapFacts.IsRested, 1);
            _socialGoal = new GoapGoal("Socialiser");
            _socialGoal.Desired.Set(GoapFacts.IsSocial, 1);
            _idleGoal = new GoapGoal("Flâner");
            _idleGoal.Desired.Set(GoapFacts.Idled, 1);
            _goals.Clear();
            _goals.Add(_drinkGoal);
            _goals.Add(_eatGoal);
            _goals.Add(_sleepGoal);
            _goals.Add(_socialGoal);
            _goals.Add(_idleGoal);
            _helpGoal = new GoapGoal("Aider");
            _stockGoal = new GoapGoal("FaireDesRéserves");
            _stockGoal.Desired.Set(GoapFacts.HasFood, 1);
            _goals.Add(_helpGoal);
            _goals.Add(_stockGoal);
        }

        // ---------------- personality helpers ----------------

        void RollPlanNoise()
        {
            _noise.Clear();
            // Imaginative: try a different bush sometimes.
            _preferredBush = null;
            if (Village != null && _rng.NextDouble() < PersonalityTuning.OtherBushChanceImag * Personality.N)
            {
                var stocked = new List<FruitBush>();
                foreach (var b in Village.Bushes) if (b != null && b.HasFruit) stocked.Add(b);
                if (stocked.Count > 0) _preferredBush = stocked[_rng.Next(stocked.Count)];
            }
        }

        /// <summary>Per-plan cost jitter: imaginative Gloubs explore varied plans, pragmatic ones stay on the cheapest.</summary>
        public float CostNoise(string actionName)
        {
            if (Personality == null) return 0f;
            if (!_noise.TryGetValue(actionName, out var n))
            {
                n = ((float)_rng.NextDouble() * 2f - 1f) * PersonalityTuning.CostNoiseImag * Personality.N;
                _noise[actionName] = n;
            }
            return n;
        }

        public FruitBush ChosenBush()
        {
            if (_preferredBush != null && _preferredBush.HasFruit) return _preferredBush;
            return NearestStockedBush();
        }

        void RecordStart(GoapAction a)
        {
            Starts.TryGetValue(a.Name, out var c);
            Starts[a.Name] = c + 1;
            if (a is PickFruitAction && Agent.ActiveGoal == _stockGoal)
            {
                Starts.TryGetValue("Réserve", out var r);
                Starts["Réserve"] = r + 1;
            }
            float need = -1f;
            switch (a.Name)
            {
                case "Boire": need = Needs.Thirst; break;
                case "Manger": need = Needs.Hunger; break;
                case "Dormir": need = Needs.Energy; break;
                case "Discuter": case "Rêvasser": need = Needs.Social; break;
            }
            if (need >= 0f)
            {
                NeedSumAtStart.TryGetValue(a.Name, out var s);
                NeedSumAtStart[a.Name] = s + need;
            }
        }

        public void AddFriendship(GloubVillager other, float baseAmount)
        {
            if (Village == null || other == null) return;
            float mult = Personality == null ? 1f
                : Mathf.Lerp(PersonalityTuning.FriendGainEmpath, PersonalityTuning.FriendGainLogic, Personality.T);
            Village.Friendship.Add(Id, other.Id, baseAmount * mult);
        }

        public void FinishHelp(string what)
        {
            HelpsGiven++;
            _helpCooldownUntil = Time.time + PersonalityTuning.HelpCooldown;
            Village?.Log($"[t={Time.time:F1}] {DisplayName} ({Personality.Type}) {what} ♡");
            HelpTarget = null;
        }

        /// <summary>A point ~1.2 m in front of this Gloub on the side of the visitor (so they don't overlap).</summary>
        public Vector3 ApproachPointFor(GloubVillager visitor)
        {
            var dir = visitor.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            return transform.position + dir.normalized * 1.2f;
        }

        public Vector3 QuietSpot()
        {
            var best = transform.position;
            float bestScore = float.NegativeInfinity;
            for (int k = 0; k < 6; k++)
            {
                var p = Village != null ? Village.RandomWanderPoint(transform.position, 1.2f) : transform.position;
                float nearest = 99f;
                foreach (var o in Village.Villagers)
                    if (o != null && o != this) nearest = Mathf.Min(nearest, Horiz(p, o.transform.position));
                if (nearest > bestScore) { bestScore = nearest; best = p; }
            }
            return best;
        }

        public Vector3 ContemplateSpot()
        {
            // Half the time sit by the campfire, otherwise a far point to watch the clouds.
            if (Village != null && Village.Plaza != null && _rng.NextDouble() < 0.5)
            {
                float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
                return Village.Plaza.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.0f;
            }
            return Village != null ? Village.RandomWanderPoint(transform.position, 1.6f) : transform.position;
        }

        GloubVillager FindNeedyFriend(out bool hungry, out float neediness)
        {
            hungry = false; neediness = 0f;
            GloubVillager best = null;
            foreach (var o in Village.Villagers)
            {
                if (o == null || o == this || o.Needs == null || o.Needs.Sleeping) continue;
                float aff = Village.Friendship.Get(Id, o.Id);
                if (aff < 0.12f) continue;
                float h = Mathf.Clamp01(PersonalityTuning.NeedyHunger - o.Needs.Hunger) * 2.5f;
                float s = Mathf.Clamp01(PersonalityTuning.NeedySocial - o.Needs.Social) * 2.5f;
                float n = Mathf.Max(h, s) * (0.6f + aff);
                if (n > neediness) { neediness = n; best = o; hungry = h >= s; }
            }
            return best;
        }

        public void BindMover(IGoapMover mover)
        {
            if (Agent != null) Agent.Mover = mover;
        }

        void Update()
        {
            if (Needs == null || Agent == null || Village == null || !Village.SimulationReady) return;
            float dt = Time.deltaTime;
            Needs.Tick(dt);
            Agent.Tick(dt);
            UpdateLookTarget();
            KeepPersonalSpace(dt);
        }

        /// <summary>While chatting/comforting, step back so two chubby bodies stand ~1.5 m apart, face to face.</summary>
        void KeepPersonalSpace(float dt)
        {
            if (!IsChatting || _chatPartner == null) return;
            var d = transform.position - _chatPartner.transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            const float wanted = 1.5f;
            if (dist >= wanted) return;
            if (dist < 0.01f) d = transform.right; // degenerate: pick a side
            var step = d.normalized * Mathf.Min(wanted - dist, 1.2f * dt);
            var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null && nav.enabled && nav.isOnNavMesh) nav.Move(step);
            else transform.position += step;
        }

        GloubMover _mover;

        void UpdateLookTarget()
        {
            if (_mover == null) _mover = GetComponent<GloubMover>();
            if (_mover == null) return;

            Vector3? look = null;
            if (IsChatting && _chatPartner != null)
            {
                look = _chatPartner.transform.position;
            }
            else
            {
                var act = Agent.RunningAction;
                if (act is DrinkAction || act is GoToWaterAction)
                {
                    var w = NearestWater();
                    if (w != null) look = w.transform.position;
                }
                else if (act is PickFruitAction || act is GoToBushAction)
                {
                    var b = BushAtFeet() ?? NearestStockedBush();
                    if (b != null) look = b.transform.position;
                }
                else if (act is EatAction)
                {
                    // Eat facing the campfire / plaza.
                    if (Village != null && Village.Plaza != null) look = Village.Plaza.transform.position;
                }
                else if (act is SleepAction || act is GoHomeAction)
                {
                    // Bed sits in front of the door: face away from the house, toward the village.
                    if (Home != null) look = Home.ApproachPoint + (Home.ApproachPoint - Home.transform.position);
                }
                else if ((act is OfferFruitAction || act is ComfortAction) && HelpTarget != null)
                {
                    look = HelpTarget.transform.position;
                }
                else if (act is ContemplateAction && Village != null && Village.Plaza != null
                         && Horiz(transform.position, Village.Plaza.transform.position) < 3.5f)
                {
                    look = Village.Plaza.transform.position; // gaze at the campfire
                }
                else if (act is FindFriendAction)
                {
                    var p = NearbyChatPartner();
                    if (p != null) look = p.transform.position;
                }
            }
            _mover.SetLookTarget(look);
        }

        WorldState SenseWorld()
        {
            var s = new WorldState();
            if (HasFood) s.Set(GoapFacts.HasFood, 1);
            if (Village.AnyFruitAvailable()) s.Set(GoapFacts.FruitAvailable, 1);

            var pos = transform.position;
            var well = NearestWater();
            if (well != null && Horiz(pos, well.ApproachPoint) <= 1.25f) s.Set(GoapFacts.AtWater, 1);
            var bush = BushAtFeet() ?? NearestStockedBush();
            if (bush != null && Horiz(pos, bush.ApproachPoint) <= 1.15f) s.Set(GoapFacts.AtBush, 1);
            if (Home != null && Horiz(pos, Home.ApproachPoint) <= 1.05f) s.Set(GoapFacts.AtHome, 1);

            var near = NearbyChatPartner();
            if (near != null) s.Set(GoapFacts.NearFriend, 1);
            return s;
        }

        List<GoapGoal> EvaluateGoals()
        {
            var P = Personality;
            // Organised act earlier (treat needs as lower), spontaneous wait until urgent.
            float shift = (P.J - 0.5f) * PersonalityTuning.ThresholdShift;
            _drinkGoal.Priority = GloubNeeds.Urgency(Needs.Thirst - shift) * 1.12f;
            _eatGoal.Priority = GloubNeeds.Urgency(Needs.Hunger - shift) * 1.00f;
            _sleepGoal.Priority = GloubNeeds.Urgency(Needs.Energy - shift) * 0.92f
                                  + (P.J > 0.5f && Needs.Energy < 0.65f ? PersonalityTuning.RegularSleepBonus * (P.J - 0.5f) * 2f : 0f);
            _socialGoal.Priority = GloubNeeds.Urgency(Needs.Social)
                                   * Mathf.Lerp(PersonalityTuning.SocialWeightIntro, PersonalityTuning.SocialWeightExtro, P.S);
            _idleGoal.Priority = 0.09f;

            // Organised: keep a fruit in stock.
            _stockGoal.Priority = (!HasFood && P.J > 0.5f && Village.AnyFruitAvailable() && Needs.Hunger < 0.85f)
                ? PersonalityTuning.StockPriority * (P.J - 0.5f) * 2f : 0f;

            // Empathic: help a friend in need. Keep the target while a help plan is running.
            bool helping = Agent.ActiveGoal == _helpGoal && Agent.RunningAction != null && HelpTarget != null;
            if (helping)
            {
                _helpGoal.Priority = Mathf.Max(_helpGoal.Priority, 0.3f);
            }
            else
            {
                _helpGoal.Priority = 0f;
                HelpTarget = null;
                if (Time.time >= _helpCooldownUntil)
                {
                    var friend = FindNeedyFriend(out bool hungry, out float need);
                    if (friend != null && need > 0.05f)
                    {
                        HelpTarget = friend;
                        _helpGoal.Desired = new WorldState();
                        _helpGoal.Desired.Set(hungry ? PFacts.HelpedFood : PFacts.HelpedMood, 1);
                        _helpGoal.Priority = (1f - P.T) * PersonalityTuning.HelpWeightEmpath * Mathf.Clamp01(need);
                    }
                }
            }
            return _goals;
        }

        public WaterSource NearestWater()
        {
            return Village != null ? Village.NearestWater(transform.position) : null;
        }

        public FruitBush NearestStockedBush()
        {
            return Village != null ? Village.NearestStockedBush(transform.position) : null;
        }

        public FruitBush BushAtFeet()
        {
            if (Village == null || Village.Bushes == null) return null;
            FruitBush best = null;
            float bestD = 1.2f;
            var pos = transform.position;
            for (int i = 0; i < Village.Bushes.Count; i++)
            {
                var b = Village.Bushes[i];
                if (b == null) continue;
                float d = Horiz(pos, b.ApproachPoint);
                if (d < bestD)
                {
                    bestD = d;
                    best = b;
                }
            }
            return best;
        }

        public GloubVillager ChooseChatPartner()
        {
            if (Village == null) return null;
            if (_stickyPartner != null && Time.time < _stickyUntil && _stickyPartner.IsAvailableForChat)
                return _stickyPartner;

            GloubVillager best = null;
            float bestScore = float.NegativeInfinity;
            var villagers = Village.Villagers;
            for (int i = 0; i < villagers.Count; i++)
            {
                var other = villagers[i];
                if (other == null || other == this || !other.IsAvailableForChat) continue;
                float affinity = Village.Friendship.Get(Id, other.Id);
                float dist = Horiz(transform.position, other.transform.position);
                float score = affinity * 3.2f - dist * 0.06f;
                // Prefer already-nearby friends so meetings actually happen.
                if (dist < 3.5f) score += 0.35f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }

            _stickyPartner = best;
            _stickyUntil = Time.time + 4.5f;
            return best;
        }

        public GloubVillager NearbyChatPartner()
        {
            if (Village == null) return null;
            var villagers = Village.Villagers;
            GloubVillager best = null;
            float bestD = 2.1f;
            for (int i = 0; i < villagers.Count; i++)
            {
                var other = villagers[i];
                if (other == null || other == this) continue;
                float d = Horiz(transform.position, other.transform.position);
                if (d < bestD)
                {
                    bestD = d;
                    best = other;
                }
            }
            return best;
        }

        public void BeginChat(GloubVillager partner)
        {
            _chatPartner = partner;
            IsChatting = true;
            if (partner != null) partner.ReceiveChat(this);
        }

        public void ReceiveChat(GloubVillager from)
        {
            IsChatting = true;
            _chatPartner = from;
        }

        public void EndChat(GloubVillager partner, bool success)
        {
            IsChatting = false;
            if (partner != null)
            {
                partner.IsChatting = false;
                if (success && Village != null)
                    AddFriendship(partner, 0.11f);
                if (success) LastSocialWasDream = false;
            }
            _chatPartner = null;
        }

        public Vector3 RandomWanderPoint()
        {
            if (Village != null) return Village.RandomWanderPoint(transform.position,
                Personality == null ? 1f : Mathf.Lerp(PersonalityTuning.WanderRadiusPrag, PersonalityTuning.WanderRadiusImag, Personality.N));
            var p = transform.position;
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(2f, 5f);
            return new Vector3(p.x + Mathf.Cos(a) * r, p.y, p.z + Mathf.Sin(a) * r);
        }

        public string NeedsSummary()
        {
            return $"T={Needs.Thirst:F2} H={Needs.Hunger:F2} E={Needs.Energy:F2} S={Needs.Social:F2} food={(HasFood ? 1 : 0)}";
        }

        static float Horiz(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
