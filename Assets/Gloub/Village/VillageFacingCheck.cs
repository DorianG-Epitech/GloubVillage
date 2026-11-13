using System.Collections;
using System.IO;
using System.Text;
using Gloub.AI;
using UnityEngine;

namespace Gloub.Village
{
    /// <summary>
    /// Verification harness for Gloub facing. Logs face·velocity for walkers and
    /// captures close stills: walking (camera ahead of the walker), drinking at the
    /// well, and a chat pair (camera perpendicular to the pair). Spawned only by
    /// VillageCapture.CaptureFacing.
    /// </summary>
    public sealed class VillageFacingCheck : MonoBehaviour
    {
        public int size = 1080;
        public string outDir = "/tmp/gloub-facing";
        public float maxSimSeconds = 70f;

        readonly StringBuilder _log = new StringBuilder();
        int _walkShots, _wellShot, _chatShot;
        double _dotSum;
        int _dotCount, _dotNegative;

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Directory.CreateDirectory(outDir);
            foreach (var f in Directory.GetFiles(outDir)) File.Delete(f);
            var cam = Camera.main;
            yield return null;
            yield return null;
            Time.captureFramerate = 30;

            while (Time.time < maxSimSeconds && (_walkShots < 2 || _wellShot < 1 || _chatShot < 1))
            {
                var village = VillageController.Instance;
                if (village != null && Time.time > 2f)
                {
                    SampleDots(village);
                    TryShots(village, cam);
                }
                yield return null;
            }

            _log.AppendLine($"dot(face,velocity) samples={_dotCount} mean={(_dotCount > 0 ? _dotSum / _dotCount : 0):F3} negative={_dotNegative}");
            _log.AppendLine($"shots walk={_walkShots} well={_wellShot} chat={_chatShot} t={Time.time:F1}");
            File.WriteAllText(Path.Combine(outDir, "facing.log"), _log.ToString());
            Debug.Log("GLOUB_FACING " + _log.ToString().Replace("\n", " || "));
            Time.captureFramerate = 0;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        void SampleDots(VillageController village)
        {
            foreach (var v in village.Villagers)
            {
                var mover = v.GetComponent<GloubMover>();
                var agent = v.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (mover == null || agent == null || !agent.enabled) continue;
                var vel = agent.velocity; vel.y = 0f;
                if (vel.magnitude < 1.2f) continue;
                float d = Vector3.Dot(mover.FaceForward.normalized, vel.normalized);
                _dotSum += d; _dotCount++;
                if (d < 0f) _dotNegative++;
            }
        }

        void TryShots(VillageController village, Camera cam)
        {
            foreach (var v in village.Villagers)
            {
                var mover = v.GetComponent<GloubMover>();
                var agent = v.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (mover == null || agent == null) continue;
                var act = v.Agent != null ? v.Agent.RunningAction : null;
                var pos = v.transform.position;

                if (_walkShots < 2 && Time.time > 6f + _walkShots * 9f)
                {
                    var vel = agent.velocity; vel.y = 0f;
                    if (vel.magnitude > 1.5f && Vector3.Dot(mover.FaceForward, vel.normalized) > 0.9f)
                    {
                        // Camera ahead of the walker, 35° to the side: sees the face if facing is right.
                        var dir = Quaternion.Euler(0f, 35f, 0f) * vel.normalized;
                        Shoot(cam, pos + dir * 3.6f + Vector3.up * 1.9f, pos + Vector3.up * 0.85f, 34f,
                              $"facing_walk_{_walkShots + 1}", v, $"walking dot={Vector3.Dot(mover.FaceForward, vel.normalized):F2}");
                        _walkShots++;
                        return;
                    }
                }

                if (_wellShot < 1 && act is DrinkAction)
                {
                    var well = v.NearestWater();
                    if (well == null) continue;
                    var toWell = well.transform.position - pos; toWell.y = 0f;
                    float d = Vector3.Dot(mover.FaceForward, toWell.normalized);
                    if (d < 0.9f) continue; // wait until turned
                    var side = Vector3.Cross(Vector3.up, toWell.normalized);
                    var camPos = pos + side * 4.6f + toWell.normalized * 0.4f + Vector3.up * 2.3f;
                    Shoot(cam, camPos, Vector3.Lerp(pos, well.transform.position, 0.45f) + Vector3.up * 0.8f, 40f,
                          "facing_well", v, $"drinking dot(face,toWell)={d:F2}");
                    _wellShot++;
                    return;
                }

                if (_chatShot < 1 && act is ChatAction && v.IsChatting)
                {
                    var partner = v.NearbyChatPartner();
                    if (partner == null || !partner.IsChatting) continue;
                    var pm = partner.GetComponent<GloubMover>();
                    var ab = partner.transform.position - pos; ab.y = 0f;
                    float da = Vector3.Dot(mover.FaceForward, ab.normalized);
                    float db = pm != null ? Vector3.Dot(pm.FaceForward, -ab.normalized) : 0f;
                    if (da < 0.85f || db < 0.85f) continue;
                    var mid = (pos + partner.transform.position) * 0.5f;
                    var side = Vector3.Cross(Vector3.up, ab.normalized);
                    Shoot(cam, mid + side * 4.0f + Vector3.up * 2.0f, mid + Vector3.up * 0.85f, 38f,
                          "facing_chat", v, $"chat {v.DisplayName}<->{partner.DisplayName} dotA={da:F2} dotB={db:F2}");
                    _chatShot++;
                    return;
                }
            }
        }

        void Shoot(Camera cam, Vector3 camPos, Vector3 look, float fov, string name, GloubVillager v, string note)
        {
            cam.transform.position = camPos;
            cam.transform.LookAt(look);
            cam.fieldOfView = fov;
            foreach (var ui in FindObjectsByType<GloubAgentUI>(FindObjectsSortMode.None)) ui.FaceCamera(cam);

            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply(false);
            RenderTexture.active = null;
            cam.targetTexture = prev;
            var px = tex.GetPixels32();
            var rgb = new byte[size * size * 3];
            int o = 0;
            for (int y = size - 1; y >= 0; y--)
                for (int x = 0; x < size; x++)
                {
                    var p = px[y * size + x];
                    rgb[o++] = p.r; rgb[o++] = p.g; rgb[o++] = p.b;
                }
            File.WriteAllBytes(Path.Combine(outDir, name + ".rgb"), rgb);
            Destroy(tex);
            rt.Release();
            Destroy(rt);
            string line = $"{name} t={Time.time:F1} {v.DisplayName} act={v.CurrentActionLabel} {note}";
            _log.AppendLine(line);
            Debug.Log("GLOUB_FACING " + line);
        }
    }
}
