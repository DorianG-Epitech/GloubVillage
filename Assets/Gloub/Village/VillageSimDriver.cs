using System.Collections;
using System.IO;
using Gloub.AI;
using UnityEngine;

namespace Gloub.Village
{
    /// <summary>
    /// Play-mode harness: run the village for a few simulated minutes, dump
    /// snapshots, write stills (RGB24, converted later), flag stuck agents.
    /// Activated by -gloubVillage on the command line, or when autoRun is set.
    /// Does not produce a video (capture path is kept ready).
    /// </summary>
    public sealed class VillageSimDriver : MonoBehaviour
    {
        public bool AutoRun;
        public float SimulatedSeconds = 150f;
        public float TimeScale = 2.4f;
        public string StillDir = "/tmp/gloub-village";
        public int StillSize = 1080;

        VillageController _village;
        Camera _cam;
        float[] _shotTimes = { 18f, 52f, 80f };
        int _nextShot;
        Vector3[] _lastPos;
        float[] _stillFor;
        string[] _stuckNames;
        bool _finished;

        public static bool ShouldAutoRun()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-gloubVillage") return true;
            }
            return false;
        }

        void Awake()
        {
            if (!ShouldAutoRun() && !AutoRun)
            {
                enabled = false;
                return;
            }
            Time.timeScale = TimeScale;
            // Stills at 18 / 52 / 80; a full 150s soak is kept for -gloubVillageLong.
            var args = System.Environment.GetCommandLineArgs();
            bool longRun = false;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-gloubVillageLong") longRun = true;
            if (!longRun)
            {
                SimulatedSeconds = 84f;
                _shotTimes = new[] { 18f, 52f, 80f };
            }
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-simSeconds" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s))
                { SimulatedSeconds = s; _shotTimes = new[] { 18f, s * 0.5f, s - 4f }; }
                if (args[i] == "-timeScale" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ts))
                { TimeScale = ts; Time.timeScale = ts; }
            }
        }

        void Update()
        {
            if (enabled && !_finished && Time.time > SimulatedSeconds + 10f)
                Finish();
        }

        void Start()
        {
            if (!enabled) return;
            _village = FindFirstObjectByType<VillageController>();
            _cam = Camera.main;
            Directory.CreateDirectory(StillDir);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // Wait one frame so VillageController.Start has baked the NavMesh.
            yield return null;
            yield return null;

            if (_village != null)
            {
                _lastPos = new Vector3[_village.Villagers.Count];
                _stillFor = new float[_village.Villagers.Count];
                _stuckNames = new string[_village.Villagers.Count];
                for (int i = 0; i < _village.Villagers.Count; i++)
                {
                    var v = _village.Villagers[i];
                    _lastPos[i] = v != null ? v.transform.position : Vector3.zero;
                }
            }

            float simEnd = SimulatedSeconds;
            while (Time.time < simEnd && !_finished)
            {
                WatchStuck();
                if (_nextShot < _shotTimes.Length && Time.time >= _shotTimes[_nextShot])
                {
                    CaptureStill(_nextShot + 1);
                    _nextShot++;
                }
                if (Mathf.FloorToInt(Time.time) % 20 == 0)
                {
                    // snapshot roughly every 20 sim seconds (guarded by waiting a bit)
                }
                yield return new WaitForSeconds(1.0f);
                if (_village != null && Mathf.Abs(Time.time % 20f) < 1.2f)
                    try { _village.Log(_village.DumpSnapshot()); }
                    catch (System.Exception e) { _village.Log("snapshot error: " + e.Message); }
            }

            if (_nextShot < _shotTimes.Length)
            {
                while (_nextShot < _shotTimes.Length)
                {
                    CaptureStill(_nextShot + 1);
                    _nextShot++;
                }
            }

            Finish();
        }

        void WatchStuck()
        {
            if (_village == null || _lastPos == null) return;
            for (int i = 0; i < _village.Villagers.Count; i++)
            {
                var v = _village.Villagers[i];
                if (v == null) continue;
                var pos = v.transform.position;
                float moved = Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(_lastPos[i].x, 0, _lastPos[i].z));
                bool stationaryAction = v.Needs != null && v.Needs.Sleeping
                    || v.IsChatting
                    || (v.Agent != null && v.Agent.RunningAction != null
                        && (v.Agent.RunningAction.Name == "Boire"
                            || v.Agent.RunningAction.Name == "Manger"
                            || v.Agent.RunningAction.Name == "Dormir"
                            || v.Agent.RunningAction.Name == "Cueillir"
                            || v.Agent.RunningAction.Name == "Discuter"));
                if (moved > 0.18f || stationaryAction)
                {
                    _stillFor[i] = 0f;
                    _lastPos[i] = pos;
                }
                else
                {
                    // Called about once per simulated second (WaitForSeconds is timeScale-scaled).
                    _stillFor[i] += 1f;
                    if (_stillFor[i] >= 12f)
                    {
                        _stuckNames[i] = v.DisplayName;
                        _village.Log($"[t={Time.time:F1}] STUCK? {v.DisplayName} {v.NeedsSummary()} act={v.CurrentActionLabel} — rebound");
                        if (v.Agent != null) v.Agent.ForceReplan("unstuck");
                        var npos = pos + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                        if (UnityEngine.AI.NavMesh.SamplePosition(npos, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                            npos = hit.position;
                        var agent = v.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(npos);
                        else v.transform.position = npos;
                        _stillFor[i] = 0f;
                        _lastPos[i] = npos;
                    }
                }
            }
        }

        void CaptureStill(int index)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            PoseCamera(index);
            var uis = FindObjectsByType<Gloub.AI.GloubAgentUI>(FindObjectsSortMode.None);
            for (int i = 0; i < uis.Length; i++)
                if (uis[i] != null) uis[i].FaceCamera(_cam);
            int size = StillSize;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            var prev = _cam.targetTexture;
            _cam.targetTexture = rt;
            _cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            _cam.targetTexture = prev;
            RenderTexture.active = null;

            var pixels = tex.GetPixels32();
            var bytes = new byte[size * size * 3];
            // Unity GetPixels32 is bottom-up; write top-down RGB for ffmpeg/PIL.
            int di = 0;
            for (int y = size - 1; y >= 0; y--)
            {
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    var p = pixels[row + x];
                    bytes[di++] = p.r;
                    bytes[di++] = p.g;
                    bytes[di++] = p.b;
                }
            }

            string path = Path.Combine(StillDir, $"village_{index:00}.rgb");
            File.WriteAllBytes(path, bytes);
            File.WriteAllText(path + ".meta", $"{size} {size} rgb24 t={Time.time:F1}");
            if (_village != null) _village.Log($"[t={Time.time:F1}] screenshot {path}");

            Destroy(tex);
            rt.Release();
            Destroy(rt);
        }

        void Finish()
        {
            if (_finished) return;
            _finished = true;
            if (_village != null)
            {
                _village.Log("=== FIN SIM ===");
                _village.Log(_village.DumpSnapshot());
                int plans = 0, done = 0, fails = 0;
                float minNeed = 1f;
                for (int i = 0; i < _village.Villagers.Count; i++)
                {
                    var v = _village.Villagers[i];
                    if (v == null || v.Agent == null) continue;
                    plans += v.Agent.PlansFormed;
                    done += v.Agent.ActionsCompleted;
                    fails += v.Agent.PlansFailed;
                    minNeed = Mathf.Min(minNeed, v.Needs.Thirst, v.Needs.Hunger, v.Needs.Energy, v.Needs.Social);
                }
                _village.Log($"stats plans={plans} actions={done} fails={fails} minNeed={minNeed:F2}");
                _village.Log(PersonalityTable());
                _village.FlushLog();
            }

            Time.timeScale = 1f;
            bool cli = ShouldAutoRun();
#if UNITY_EDITOR
            if (cli || Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
#else
            if (cli) Application.Quit();
#endif
        }

        string PersonalityTable()
        {
            string[] cols = { "Boire", "Manger", "Réserve", "Cueillir", "Dormir", "Discuter", "Rêvasser", "Contempler", "Flâner", "OffrirFruit", "Réconforter" };
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== PERSONNALITES ===");
            sb.Append("TABLE|nom|type|soc|ima|rat|org");
            foreach (var c in cols) sb.Append('|').Append(c);
            sb.Append("|soifMoyBoire|faimMoyManger|energieMoyDormir|socialMoyDiscuter|aides|amitieMoy");
            sb.AppendLine();
            foreach (var v in _village.Villagers)
            {
                if (v == null || v.Personality == null) continue;
                var p = v.Personality;
                sb.Append($"TABLE|{v.DisplayName}|{p.Type}|{p.Sociabilite}|{p.Imagination}|{p.Rationalite}|{p.Organisation}");
                foreach (var c in cols) { v.Starts.TryGetValue(c, out var n); sb.Append('|').Append(n); }
                sb.Append('|').Append(Avg(v, "Boire")).Append('|').Append(Avg(v, "Manger"))
                  .Append('|').Append(Avg(v, "Dormir")).Append('|').Append(Avg(v, "Discuter"));
                float aff = 0f; int n2 = 0;
                foreach (var o in _village.Villagers) if (o != null && o != v) { aff += _village.Friendship.Get(v.Id, o.Id); n2++; }
                sb.Append('|').Append(v.HelpsGiven).Append('|').Append((n2 > 0 ? aff / n2 : 0f).ToString("F2"));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static string Avg(Gloub.AI.GloubVillager v, string action)
        {
            v.Starts.TryGetValue(action, out var n);
            if (n == 0) return "-";
            v.NeedSumAtStart.TryGetValue(action, out var s);
            return (s / n).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        void PoseCamera(int index)
        {
            if (_cam == null) return;
            Vector3 pos;
            Vector3 look = new Vector3(0f, 0.35f, 0f);
            float fov = 42f;
            if (index <= 1)
            {
                pos = new Vector3(17.2f, 15.0f, -17.0f);
            }
            else if (index == 2)
            {
                pos = new Vector3(11.5f, 8.6f, -9.8f);
                look = new Vector3(0.4f, 0.5f, 0.2f);
                fov = 48f;
            }
            else
            {
                pos = new Vector3(-16.4f, 14.2f, -14.8f);
                fov = 43f;
            }
            _cam.transform.position = pos;
            _cam.transform.LookAt(look);
            _cam.fieldOfView = fov;
        }
    }
}
