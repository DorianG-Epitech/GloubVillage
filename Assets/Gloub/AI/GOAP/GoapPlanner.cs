using System.Collections.Generic;
using UnityEngine;

namespace Gloub.AI.GOAP
{
    /// <summary>
    /// Forward A* search over WorldState. Each node is a symbolic state;
    /// edges are actions whose preconditions hold and whose procedural check passes.
    /// Heuristic = number of unsatisfied goal facts (admissible when every action
    /// costs at least 1 and changes at most a handful of facts).
    /// </summary>
    public static class GoapPlanner
    {
        public const int DefaultMaxExpansions = 220;

        sealed class OpenSet
        {
            readonly List<(WorldState state, float f)> _items = new List<(WorldState, float)>(32);

            public int Count => _items.Count;

            public void Enqueue(WorldState state, float f) => _items.Add((state, f));

            public WorldState Dequeue()
            {
                int best = 0;
                float bestF = _items[0].f;
                for (int i = 1; i < _items.Count; i++)
                {
                    if (_items[i].f < bestF)
                    {
                        best = i;
                        bestF = _items[i].f;
                    }
                }
                var state = _items[best].state;
                _items.RemoveAt(best);
                return state;
            }
        }

        public static List<GoapAction> Plan(
            GoapAgent agent,
            WorldState start,
            WorldState goal,
            IList<GoapAction> actions,
            int maxExpansions = DefaultMaxExpansions)
        {
            if (start == null || goal == null || actions == null) return null;
            if (start.Satisfies(goal)) return new List<GoapAction>();

            var open = new OpenSet();
            var gScore = new Dictionary<WorldState, float>();
            var cameFrom = new Dictionary<WorldState, (WorldState prev, GoapAction action)>();
            var closed = new HashSet<WorldState>();

            open.Enqueue(start, Heuristic(start, goal));
            gScore[start] = 0f;

            int expansions = 0;
            while (open.Count > 0 && expansions < maxExpansions)
            {
                var current = open.Dequeue();
                if (!closed.Add(current)) continue;
                expansions++;

                if (current.Satisfies(goal))
                    return Reconstruct(cameFrom, current);

                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    if (!current.Satisfies(action.Preconditions)) continue;
                    if (!action.CheckProcedural(agent)) continue;

                    var next = current.WithApplied(action.Effects);
                    float tentative = gScore[current] + Mathf.Max(0.05f, action.GetCost(agent));
                    if (gScore.TryGetValue(next, out var existing) && tentative >= existing - 0.0001f)
                        continue;

                    gScore[next] = tentative;
                    cameFrom[next] = (current, action);
                    open.Enqueue(next, tentative + Heuristic(next, goal));
                }
            }

            return null;
        }

        static float Heuristic(WorldState state, WorldState goal)
        {
            int missing = 0;
            foreach (var kv in goal.Entries)
            {
                if (state.Get(kv.Key) != kv.Value) missing++;
            }
            return missing;
        }

        static List<GoapAction> Reconstruct(
            Dictionary<WorldState, (WorldState prev, GoapAction action)> cameFrom,
            WorldState current)
        {
            var plan = new List<GoapAction>(6);
            while (cameFrom.TryGetValue(current, out var step))
            {
                plan.Add(step.action);
                current = step.prev;
            }
            plan.Reverse();
            return plan;
        }
    }
}
