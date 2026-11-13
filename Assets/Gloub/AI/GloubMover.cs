using Gloub.AI.GOAP;
using UnityEngine;
using UnityEngine.AI;

namespace Gloub.AI
{
    /// <summary>
    /// NavMesh movement with a simple steering fallback if the mesh failed to bake.
    /// </summary>
    public sealed class GloubMover : MonoBehaviour, IGoapMover
    {
        public float WalkSpeed = 2.05f;
        public float SteerAvoidRadius = 0.95f;

        NavMeshAgent _agent;
        bool _useNav;
        Vector3 _steerTarget;
        bool _steering;
        Village.VillageController _village;

        public bool UsingNavMesh => _useNav;
        public float HorizontalSpeed => _useNav && _agent != null
            ? new Vector3(_agent.velocity.x, 0f, _agent.velocity.z).magnitude
            : _steerSpeed;
        public Vector3 Position => transform.position;

        float _steerSpeed;
        Vector3 _steerDir;
        Vector3? _lookTarget;

        /// <summary>Turn rate for facing, degrees per second.</summary>
        public float TurnSpeed = 420f;

        /// <summary>The prefab's face is on local -Z, so "forward" for a Gloub is -transform.forward.</summary>
        public Vector3 FaceForward => -transform.forward;

        /// <summary>World point to face while standing still (well, bush, friend…). Null = keep heading.</summary>
        public void SetLookTarget(Vector3? worldPoint) => _lookTarget = worldPoint;

        /// <summary>Rotation that makes the face (-Z) look along dir.</summary>
        public static Quaternion FaceRotation(Vector3 dir) => Quaternion.LookRotation(-dir, Vector3.up);

        void LateUpdate()
        {
            Vector3 want = Vector3.zero;
            if (_useNav && _agent != null && _agent.enabled)
            {
                var v = _agent.velocity;
                v.y = 0f;
                if (v.sqrMagnitude > 0.04f) want = v;
                else if (_agent.hasPath && _agent.desiredVelocity.sqrMagnitude > 0.04f)
                {
                    want = _agent.desiredVelocity;
                    want.y = 0f;
                }
            }
            else if (_steering && _steerSpeed > 0.01f)
            {
                want = _steerDir;
            }

            if (want.sqrMagnitude < 0.0001f && _lookTarget.HasValue)
            {
                want = _lookTarget.Value - transform.position;
                want.y = 0f;
                if (want.sqrMagnitude < 0.01f) want = Vector3.zero;
            }

            if (want.sqrMagnitude < 0.0001f) return;
            var target = FaceRotation(want.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, TurnSpeed * Time.deltaTime);
        }

        public void Setup(int avoidPriority, Village.VillageController village)
        {
            _village = village;
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();

            _agent.radius = 0.62f; // body is ~0.65 wide; keeps chat partners apart
            _agent.height = 1.55f;
            _agent.speed = WalkSpeed;
            _agent.acceleration = 12f;
            _agent.angularSpeed = 260f;
            _agent.stoppingDistance = 0.42f;
            _agent.autoBraking = true;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            _agent.avoidancePriority = avoidPriority;
            _agent.baseOffset = 0f;
            // The Gloub prefab's face looks along local -Z; NavMeshAgent would turn +Z
            // (its back) toward travel. We rotate manually in LateUpdate instead.
            _agent.updateRotation = false;
            _agent.updateUpAxis = true;

            _useNav = _agent.isOnNavMesh;
            if (!_useNav)
            {
                // Try warping onto a nearby navmesh sample.
                if (NavMesh.SamplePosition(transform.position, out var hit, 2.5f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                    _useNav = _agent.isOnNavMesh;
                }
            }

            if (!_useNav)
            {
                _agent.enabled = false;
                Debug.LogWarning($"[GloubMover] {name} falling back to steering (no NavMesh).");
            }
        }

        public void MoveTo(Vector3 destination)
        {
            destination.y = transform.position.y;
            if (_useNav && _agent != null && _agent.enabled)
            {
                if (NavMesh.SamplePosition(destination, out var hit, 2.0f, NavMesh.AllAreas))
                    destination = hit.position;
                if (!_agent.hasPath || Horizontal(_agent.destination, destination) > 0.35f)
                    _agent.SetDestination(destination);
                return;
            }

            _steerTarget = destination;
            _steering = true;
        }

        public bool HasArrived(Vector3 destination, float radius)
        {
            if (_useNav && _agent != null && _agent.enabled)
            {
                if (Horizontal(transform.position, destination) <= radius) return true;
                if (_agent.pathPending) return false;
                if (_agent.hasPath && _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, radius * 0.7f)
                    && _agent.velocity.sqrMagnitude < 0.08f)
                    return true;
                return false;
            }

            return Horizontal(transform.position, destination) <= radius;
        }

        public void StopMoving()
        {
            _steering = false;
            _steerSpeed = 0f;
            if (_useNav && _agent != null && _agent.enabled && _agent.hasPath)
                _agent.ResetPath();
        }

        void Update()
        {
            if (_useNav || !_steering) return;

            var pos = transform.position;
            var dest = _steerTarget;
            dest.y = pos.y;
            var to = dest - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.12f)
            {
                _steerSpeed = 0f;
                return;
            }

            var dir = to / dist;
            dir = AvoidNeighbors(pos, dir);
            _steerSpeed = WalkSpeed;
            pos += dir * (WalkSpeed * Time.deltaTime);
            transform.position = pos;
            _steerDir = dir;
        }

        Vector3 AvoidNeighbors(Vector3 pos, Vector3 dir)
        {
            if (_village == null) return dir;
            var villagers = _village.Villagers;
            if (villagers == null) return dir;
            Vector3 push = Vector3.zero;
            for (int i = 0; i < villagers.Count; i++)
            {
                var other = villagers[i];
                if (other == null || other.gameObject == gameObject) continue;
                var delta = pos - other.transform.position;
                delta.y = 0f;
                float d = delta.magnitude;
                if (d < 0.001f || d > SteerAvoidRadius) continue;
                push += delta.normalized * (SteerAvoidRadius - d);
            }
            if (push.sqrMagnitude < 0.0001f) return dir;
            return (dir + push * 1.4f).normalized;
        }

        static float Horizontal(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
