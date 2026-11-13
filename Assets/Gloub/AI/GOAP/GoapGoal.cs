namespace Gloub.AI.GOAP
{
    /// <summary>
    /// Desired world-state fragment. Priority is computed from the agent's needs each tick
    /// (higher = more urgent). The planner searches for any plan that satisfies Desired.
    /// </summary>
    public sealed class GoapGoal
    {
        public string Name;
        public WorldState Desired = new WorldState();
        public float Priority;

        public GoapGoal(string name)
        {
            Name = name;
        }

        public override string ToString() => $"{Name}({Priority:F2})";
    }
}
