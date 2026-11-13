using System.Collections.Generic;
using System.IO;
using System.Text;
using Gloub.AI;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Gloub.Village
{
    /// <summary>
    /// Scene hub: collects props, bakes the NavMesh, wires each Gloub, logs plans.
    /// </summary>
    public sealed class VillageController : MonoBehaviour
    {
        public static VillageController Instance { get; private set; }

        public int Seed = 20261111;
        public bool SimulationReady { get; private set; }

        public readonly List<WaterSource> Wells = new List<WaterSource>();
        public readonly List<FruitBush> Bushes = new List<FruitBush>();
        public readonly List<HomeBed> Homes = new List<HomeBed>();
        public readonly List<SocialSpot> SocialSpots = new List<SocialSpot>();
        public readonly List<GloubVillager> Villagers = new List<GloubVillager>();
        public FriendshipSystem Friendship { get; private set; }
        public SocialSpot Plaza { get; private set; }

        readonly StringBuilder _log = new StringBuilder(16 * 1024);
        string _logPath;

        void Awake()
        {
            Instance = this;
            Random.InitState(Seed);
            _logPath = Path.Combine(Application.dataPath, "../../../../../workspace/gloub/village_sim.log");
            try
            {
                var dir = "/workspace/gloub";
                if (Directory.Exists(dir)) _logPath = Path.Combine(dir, "village_sim.log");
            }
            catch
            {
                // keep fallback
            }
        }

        void Start()
        {
            Collect();
            BakeNavMesh();
            Friendship = new FriendshipSystem();
            Friendship.Init(Villagers.Count, Seed);

            for (int i = 0; i < Villagers.Count; i++)
            {
                var v = Villagers[i];
                if (v == null) continue;
                if (v.Home == null && Homes.Count > 0)
                    v.Home = Homes[i % Homes.Count];

                string nm = string.IsNullOrEmpty(v.DisplayName) ? ("Gloub" + i) : v.DisplayName;
                v.Setup(v.Id >= 0 ? v.Id : i, nm, v.Home, this, 11);

                var mover = v.GetComponent<GloubMover>();
                if (mover == null) mover = v.gameObject.AddComponent<GloubMover>();
                mover.Setup(20 + i * 3, this);
                v.BindMover(mover);

                var animRoot = v.transform.Find("AnimRoot");
                var loco = v.GetComponent<GloubLocomotion>();
                if (loco == null) loco = v.gameObject.AddComponent<GloubLocomotion>();
                loco.Setup(animRoot, mover, v);

                var ui = v.GetComponent<GloubAgentUI>();
                if (ui == null) ui = v.gameObject.AddComponent<GloubAgentUI>();
                ui.Setup(v);
            }

            for (int i = 0; i < Villagers.Count; i++)
            {
                var v = Villagers[i];
                if (v != null && v.Personality != null)
                    Log($"[t={Time.time:F1}] Personnalité {v.DisplayName}: {v.Personality}");
            }
            SimulationReady = true;
            Log($"[t={Time.time:F1}] Village prêt  seed={Seed}  gloubs={Villagers.Count}  puits={Wells.Count}  buissons={Bushes.Count}  maisons={Homes.Count}  nav={(NavMesh.CalculateTriangulation().vertices.Length > 0 ? "ok" : "ABSENT")}");
        }

        void Collect()
        {
            Wells.Clear();
            Bushes.Clear();
            Homes.Clear();
            SocialSpots.Clear();
            Villagers.Clear();
            Wells.AddRange(FindObjectsByType<WaterSource>(FindObjectsSortMode.None));
            Bushes.AddRange(FindObjectsByType<FruitBush>(FindObjectsSortMode.None));
            Homes.AddRange(FindObjectsByType<HomeBed>(FindObjectsSortMode.InstanceID));
            SocialSpots.AddRange(FindObjectsByType<SocialSpot>(FindObjectsSortMode.None));
            Villagers.AddRange(FindObjectsByType<GloubVillager>(FindObjectsSortMode.InstanceID));
            Villagers.Sort((a, b) => a.Id.CompareTo(b.Id));
            Plaza = SocialSpots.Count > 0 ? SocialSpots[0] : null;
        }

        void BakeNavMesh()
        {
            var surfaces = FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            if (surfaces == null || surfaces.Length == 0)
            {
                var go = GameObject.Find("Walkable");
                if (go == null) go = gameObject;
                var surface = go.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.BuildNavMesh();
                Log($"[t={Time.time:F1}] NavMeshSurface ajouté et cuit sur {go.name}");
                return;
            }

            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] != null) surfaces[i].BuildNavMesh();
            }
        }

        public WaterSource NearestWater(Vector3 from)
        {
            WaterSource best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < Wells.Count; i++)
            {
                var w = Wells[i];
                if (w == null) continue;
                float d = Horiz(from, w.ApproachPoint);
                if (d < bestD) { bestD = d; best = w; }
            }
            return best;
        }

        public FruitBush NearestStockedBush(Vector3 from)
        {
            FruitBush best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < Bushes.Count; i++)
            {
                var b = Bushes[i];
                if (b == null || !b.HasFruit) continue;
                float d = Horiz(from, b.ApproachPoint);
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        public bool AnyFruitAvailable()
        {
            for (int i = 0; i < Bushes.Count; i++)
                if (Bushes[i] != null && Bushes[i].HasFruit) return true;
            return false;
        }

        public Vector3 RandomWanderPoint(Vector3 from) => RandomWanderPoint(from, 1f);

        public Vector3 RandomWanderPoint(Vector3 from, float radiusScale)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(2.2f, 7.5f) * radiusScale;
                var p = new Vector3(from.x + Mathf.Cos(ang) * radius, from.y, from.z + Mathf.Sin(ang) * radius);
                if (Mathf.Abs(p.x) > 17f || Mathf.Abs(p.z) > 17f) continue;
                if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas))
                    return hit.position;
                return p;
            }
            return from;
        }

        public void Log(string line)
        {
            Debug.Log(line);
            _log.AppendLine(line);
        }

        public string DumpSnapshot()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"t={Time.time:F1} villagers={Villagers.Count}");
            for (int i = 0; i < Villagers.Count; i++)
            {
                var v = Villagers[i];
                if (v == null) continue;
                sb.Append(v.DisplayName).Append("  ");
                if (v.Needs == null) sb.Append("(pas initialisé)");
                else sb.Append(v.NeedsSummary());
                sb.Append("  act=")
                    .Append(v.CurrentActionLabel)
                    .Append("  plan=")
                    .Append(v.Agent != null ? v.Agent.PlanSummary : "?")
                    .AppendLine();
            }
            return sb.ToString();
        }

        public void FlushLog()
        {
            try
            {
                var path = _logPath;
                if (string.IsNullOrEmpty(path)) path = "/tmp/village_sim.log";
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, _log.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Could not flush village log: " + e.Message);
            }
        }

        void OnDestroy()
        {
            FlushLog();
            if (Instance == this) Instance = null;
        }

        static float Horiz(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
