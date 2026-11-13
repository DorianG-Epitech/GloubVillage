using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Gloub.AI.GOAP
{
    public interface IGoapMover
    {
        void MoveTo(Vector3 destination);
        bool HasArrived(Vector3 destination, float radius);
        void StopMoving();
        float HorizontalSpeed { get; }
        Vector3 Position { get; }
    }

    /// <summary>
    /// Generic GOAP executor: sense world, pick the highest-priority goal,
    /// A* a plan, walk into range, perform each action over time, and replan
    /// on failure or when a clearly more urgent goal appears.
    /// </summary>
    public sealed class GoapAgent : MonoBehaviour
    {
        /// <summary>How much more urgent another goal must be to interrupt the current plan.</summary>
        public float InterruptMargin = 0.22f;
        public Action BeforePlan;
        public Action<GoapAction> ActionStarted;

        public string DisplayName;
        public int AgentId;
        public IGoapMover Mover;
        public readonly List<GoapAction> Actions = new List<GoapAction>(16);
        public Func<WorldState> Sense;
        public Func<List<GoapGoal>> EvaluateGoals;
        public Action<string> Log;

        public GoapGoal ActiveGoal { get; private set; }
        public GoapAction RunningAction { get; private set; }
        public readonly List<GoapAction> ActivePlan = new List<GoapAction>(6);

        public int PlansFormed { get; private set; }
        public int ActionsCompleted { get; private set; }
        public int PlansFailed { get; private set; }
        public float LastPlanTime { get; private set; }

        int _planIndex;
        bool _entered;
        bool _failed;
        string _failReason;
        float _moveTimer;
        float _stuckTimer;
        Vector3 _lastProgressPos;
        float _replanAt;
        readonly List<GoapGoal> _scratchGoals = new List<GoapGoal>(8);

        public string RunningLabel =>
            RunningAction != null ? (RunningAction.ShortLabel ?? RunningAction.Name) : "…";

        public string PlanSummary
        {
            get
            {
                if (ActivePlan.Count == 0) return "(aucun)";
                var sb = new StringBuilder();
                for (int i = 0; i < ActivePlan.Count; i++)
                {
                    if (i > 0) sb.Append(" -> ");
                    if (i == _planIndex) sb.Append('[');
                    sb.Append(ActivePlan[i].Name);
                    if (i == _planIndex) sb.Append(']');
                }
                return sb.ToString();
            }
        }

        public void Tick(float dt)
        {
            if (Sense == null || EvaluateGoals == null) return;

            var goals = EvaluateGoals() ?? _scratchGoals;
            GoapGoal best = null;
            for (int i = 0; i < goals.Count; i++)
            {
                if (best == null || goals[i].Priority > best.Priority) best = goals[i];
            }
            if (best == null) return;

            bool planDone = RunningAction == null && _planIndex >= ActivePlan.Count;
            bool betterGoal = ActiveGoal != null
                              && best.Name != ActiveGoal.Name
                              && best.Priority > ActiveGoal.Priority + InterruptMargin;
            bool needReplan = _failed || planDone || ActiveGoal == null || betterGoal;

            if (needReplan && Time.time >= _replanAt)
            {
                if (betterGoal && RunningAction != null && _entered)
                    RunningAction.OnExit(this);

                BeginPlan(best);
                if (ActivePlan.Count == 0)
                    _replanAt = Time.time + 0.55f;
            }

            Execute(dt);
        }

        void BeginPlan(GoapGoal goal)
        {
            if (RunningAction != null)
            {
                if (_entered) RunningAction.OnExit(this);
                RunningAction = null;
            }

            _failed = false;
            _failReason = null;
            _entered = false;
            _planIndex = 0;
            _moveTimer = 0f;
            _stuckTimer = 0f;
            ActiveGoal = goal;
            ActivePlan.Clear();

            BeforePlan?.Invoke();
            var world = Sense();
            var plan = GoapPlanner.Plan(this, world, goal.Desired, Actions);
            PlansFormed++;
            LastPlanTime = Time.time;

            if (plan != null && plan.Count > 0)
            {
                ActivePlan.AddRange(plan);
                Emit($"but={goal.Name} prio={goal.Priority:F2} plan=[{FormatPlan(plan)}]");
            }
            else
            {
                PlansFailed++;
                Emit($"but={goal.Name} prio={goal.Priority:F2} plan=ECHEC monde={world}");
            }
        }

        void Execute(float dt)
        {
            if (RunningAction == null)
            {
                if (_planIndex >= ActivePlan.Count) return;
                RunningAction = ActivePlan[_planIndex];
                RunningAction.ResetRuntime();
                _entered = false;
                _moveTimer = 0f;
                _stuckTimer = 0f;
                _lastProgressPos = transform.position;
                Emit($"début {RunningAction.Name}");
                ActionStarted?.Invoke(RunningAction);
            }

            var action = RunningAction;
            var target = action.GetTarget(this);

            if (action.RequiresInRange && Mover != null && !Mover.HasArrived(target, action.ArriveRadius))
            {
                Mover.MoveTo(target);
                _moveTimer += dt;
                WatchStuck(dt, target);
                if (_failed)
                {
                    FailPlan($"bloqué en allant à {action.Name}");
                    return;
                }
                return;
            }

            if (Mover != null) Mover.StopMoving();

            if (!_entered)
            {
                action.OnEnter(this);
                _entered = true;
                if (action.HasFailed)
                {
                    FailPlan($"{action.Name} a échoué à l'entrée");
                    return;
                }
            }

            bool done = action.Perform(this, dt);
            if (action.HasFailed)
            {
                action.OnExit(this);
                FailPlan($"{action.Name} a échoué");
                return;
            }

            if (done)
            {
                action.OnExit(this);
                ActionsCompleted++;
                Emit($"fin {action.Name}");
                _planIndex++;
                RunningAction = null;
                _entered = false;
            }
        }

        void WatchStuck(float dt, Vector3 target)
        {
            var pos = transform.position;
            float moved = Horizontal(pos, _lastProgressPos);
            float toTarget = Horizontal(pos, target);
            if (moved > 0.12f || toTarget < 0.35f)
            {
                _lastProgressPos = pos;
                _stuckTimer = 0f;
            }
            else
            {
                _stuckTimer += dt;
            }

            if (_stuckTimer > 7.5f || _moveTimer > 22f)
                _failed = true;
        }

        void FailPlan(string reason)
        {
            _failReason = reason;
            _failed = true;
            PlansFailed++;
            if (RunningAction != null && _entered) RunningAction.OnExit(this);
            RunningAction = null;
            _entered = false;
            ActivePlan.Clear();
            _planIndex = 0;
            _replanAt = Time.time + 0.5f; // back off so a failing plan can't spin every frame
            Emit($"REPLAN {reason}");
        }

        public void ForceReplan(string reason)
        {
            FailPlan(reason);
        }

        static float Horizontal(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static string FormatPlan(List<GoapAction> plan)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < plan.Count; i++)
            {
                if (i > 0) sb.Append(" -> ");
                sb.Append(plan[i].Name);
            }
            return sb.ToString();
        }

        void Emit(string message)
        {
            string line = $"[t={Time.time:F1}] {DisplayName}: {message}";
            if (Log != null) Log(line);
            else Debug.Log(line);
        }
    }
}
