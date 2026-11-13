using UnityEngine;

namespace Gloub.AI.GOAP
{
    /// <summary>
    /// One operator the planner can chain. Preconditions/effects are symbolic;
    /// movement and timed performance happen at execution time.
    /// </summary>
    public abstract class GoapAction
    {
        public string Name { get; protected set; }
        public string ShortLabel { get; protected set; }
        public WorldState Preconditions { get; } = new WorldState();
        public WorldState Effects { get; } = new WorldState();
        public float BaseCost = 1f;
        public bool HasFailed { get; set; }

        public virtual bool RequiresInRange => true;
        public virtual float ArriveRadius => 0.9f;

        public virtual float GetCost(GoapAgent agent) => BaseCost;

        /// <summary>Runtime / world checks the symbolic state cannot express (e.g. a bush still has fruit).</summary>
        public virtual bool CheckProcedural(GoapAgent agent) => true;

        public virtual Vector3 GetTarget(GoapAgent agent)
        {
            return agent != null ? agent.transform.position : Vector3.zero;
        }

        public virtual void ResetRuntime()
        {
            HasFailed = false;
        }

        public virtual void OnEnter(GoapAgent agent) { }

        /// <summary>Advance the action by dt seconds. Return true when finished.</summary>
        public virtual bool Perform(GoapAgent agent, float dt) => true;

        public virtual void OnExit(GoapAgent agent) { }

        public override string ToString() => Name;
    }
}
