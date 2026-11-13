using System.Collections;
using System.IO;
using Gloub.AI;
using UnityEngine;

namespace Gloub.Village
{
    /// <summary>
    /// Play-mode video capture for GloubVillage. Same pipeline as GloubIdleRecorder:
    /// Time.captureFramerate, Camera.Render to RGB24 top-down frames, then ffmpeg.
    /// Spawned only by VillageCapture.CaptureVideo — not saved into the scene.
    /// </summary>
    public sealed class VillageVideoRecorder : MonoBehaviour
    {
        public int width = 1080;
        public int height = 1080;
        public int fps = 30;
        public float warmupSeconds = 10f;
        public float duration = 36f;
        public float startYaw = 48f;
        public float endYaw = -42f;
        public float orbitDistance = 23.6f;
        public float orbitHeight = 15.2f;
        public float orbitFov = 42f;
        public float closeDistance = 10.6f;
        public float closeHeight = 6.9f;
        public float closeFov = 36f;
        public float closeStart = 0.36f;
        public float closeEnd = 0.70f;
        public Vector3 plazaFocus = new Vector3(0f, 0.45f, 0f);
        public string framesDir = "/tmp/gloub-village-frames";

        GloubVillager _subject;
        string _subjectNote = "(none)";

        IEnumerator Start()
        {
            Application.runInBackground = true;

            var cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Finish("ERROR no camera", 1);
                yield break;
            }

            cam.allowMSAA = true;
            cam.allowHDR = true;

            // Let VillageController bake the NavMesh and wire agents.
            yield return null;
            yield return null;

            PlaceCamera(cam, 0f);
            FaceHuds(cam);
            cam.Render();
            cam.Render();

            Debug.Log("GLOUB_VILLAGE warmup " + warmupSeconds + "s (timeScale=2) before capture");
            Time.timeScale = 2f;
            yield return new WaitForSeconds(warmupSeconds);
            Time.timeScale = 1f;

            Time.captureFramerate = Mathf.Max(1, fps);

            Directory.CreateDirectory(framesDir);
            foreach (var f in Directory.GetFiles(framesDir, "frame_*.rgb"))
                File.Delete(f);
            foreach (var n in new[] { "DONE.txt", "META.txt", "ERROR.txt", "NOTES.txt" })
            {
                string p = Path.Combine(framesDir, n);
                if (File.Exists(p)) File.Delete(p);
            }

            int total = Mathf.RoundToInt(duration * fps);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            rt.Create();
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var rgb = new byte[width * height * 3];

            Debug.Log("GLOUB_VILLAGE recording frames=" + total + " res=" + width + "x" + height
                      + " fps=" + fps + " after warmup t=" + Time.time.ToString("F2"));

            var notes = new System.Text.StringBuilder();
            notes.AppendLine("warmupEnd=" + Time.time.ToString("F2"));

            for (int i = 0; i < total; i++)
            {
                float u = total <= 1 ? 0f : i / (float)(total - 1);
                PlaceCamera(cam, u);
                FaceHuds(cam);
                SaveFrame(cam, rt, tex, rgb, Path.Combine(framesDir, "frame_" + i.ToString("D5") + ".rgb"));
                if (i % 30 == 0)
                {
                    string line = "frame=" + i + " videoT=" + (i / (float)fps).ToString("F2")
                                  + " simT=" + Time.time.ToString("F2")
                                  + " subject=" + _subjectNote
                                  + " " + SnapshotLine();
                    Debug.Log("GLOUB_VILLAGE " + line);
                    notes.AppendLine(line);
                }
                yield return null;
            }

            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);

            var meta = "frames=" + total + "\n"
                       + "fps=" + fps + "\n"
                       + "duration=" + duration.ToString("F2") + "\n"
                       + "width=" + width + "\n"
                       + "height=" + height + "\n"
                       + "warmup=" + warmupSeconds.ToString("F2") + "\n"
                       + "startYaw=" + startYaw + "\n"
                       + "endYaw=" + endYaw + "\n"
                       + "format=rgb24_topdown\n"
                       + "subject=" + _subjectNote + "\n";
            File.WriteAllText(Path.Combine(framesDir, "META.txt"), meta);
            File.WriteAllText(Path.Combine(framesDir, "NOTES.txt"), notes.ToString());
            File.WriteAllText(Path.Combine(framesDir, "DONE.txt"), total + "\n" + width + "x" + height + "\n");
            Debug.Log("GLOUB_VILLAGE done frames=" + total + " subject=" + _subjectNote);
            Time.captureFramerate = 0;
            Finish("ok", 0);
        }

        void PlaceCamera(Camera cam, float u)
        {
            float s = u * u * (3f - 2f * u);
            float yaw = Mathf.Lerp(startYaw, endYaw, s);

            float close = 0f;
            if (u > closeStart && u < closeEnd)
            {
                float t = Mathf.InverseLerp(closeStart, closeEnd, u);
                close = Mathf.Sin(t * Mathf.PI);
            }

            if (close > 0.15f && _subject == null)
            {
                _subject = PickNamed(SubjectFromArgs()) ?? PickInteresting();
                _subjectNote = Describe(_subject);
                Debug.Log("GLOUB_VILLAGE close-up subject=" + _subjectNote);
            }

            Vector3 focus = plazaFocus;
            if (_subject != null)
            {
                var target = _subject.transform.position + Vector3.up * 0.72f;
                focus = Vector3.Lerp(plazaFocus, target, close);
            }

            float dist = Mathf.Lerp(orbitDistance, closeDistance, close);
            float height = Mathf.Lerp(orbitHeight, closeHeight, close);
            float fov = Mathf.Lerp(orbitFov, closeFov, close);

            float ang = yaw * Mathf.Deg2Rad;
            cam.transform.position = new Vector3(
                Mathf.Sin(ang) * dist,
                height,
                -Mathf.Cos(ang) * dist);
            cam.transform.LookAt(focus);
            cam.fieldOfView = fov;
        }

        /// <summary>-videoSubject Name forces the close-up subject (else the most active Gloub).</summary>
        static string SubjectFromArgs()
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-videoSubject") return a[i + 1];
            return null;
        }

        static GloubVillager PickNamed(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            foreach (var v in FindObjectsByType<GloubVillager>(FindObjectsSortMode.None))
                if (v != null && v.DisplayName == n) return v;
            return null;
        }

        static GloubVillager PickInteresting()
        {
            var all = FindObjectsByType<GloubVillager>(FindObjectsSortMode.None);
            GloubVillager best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < all.Length; i++)
            {
                var v = all[i];
                if (v == null) continue;
                float score = 0f;
                if (v.IsChatting) score += 8f;
                var act = v.Agent != null ? v.Agent.RunningAction : null;
                string name = act != null ? act.Name : "";
                if (name == "Discuter") score += 7f;
                else if (name == "Boire") score += 5.5f;
                else if (name == "Manger" || name == "Cueillir") score += 5f;
                else if (name == "Dormir") score += 3f;
                else if (name == "AllerAuPuits" || name == "AllerAuBuisson"
                         || name == "Rentrer" || name == "RejoindreUnAmi")
                    score += 1.6f;
                else if (name == "Flâner") score += 0.2f;
                // Prefer subjects nearer the plaza so the dolly stays readable.
                score -= Vector3.Distance(v.transform.position, Vector3.zero) * 0.04f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = v;
                }
            }
            return best;
        }

        static string Describe(GloubVillager v)
        {
            if (v == null) return "(none)";
            return v.DisplayName + " act=" + v.CurrentActionLabel
                   + (v.IsChatting ? " ♡" : "");
        }

        static string SnapshotLine()
        {
            var village = VillageController.Instance;
            if (village == null) return "";
            var sb = new System.Text.StringBuilder();
            var list = village.Villagers;
            for (int i = 0; i < list.Count; i++)
            {
                var v = list[i];
                if (v == null) continue;
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(v.DisplayName).Append('=').Append(v.CurrentActionLabel);
            }
            return sb.ToString();
        }

        static void FaceHuds(Camera cam)
        {
            var uis = FindObjectsByType<GloubAgentUI>(FindObjectsSortMode.None);
            for (int i = 0; i < uis.Length; i++)
            {
                if (uis[i] != null) uis[i].FaceCamera(cam);
            }
        }

        static void SaveFrame(Camera cam, RenderTexture rt, Texture2D tex, byte[] rgb, string path)
        {
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = null;
            cam.targetTexture = prev;

            var px = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;
            int o = 0;
            for (int y = h - 1; y >= 0; y--)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    var p = px[row + x];
                    rgb[o++] = p.r;
                    rgb[o++] = p.g;
                    rgb[o++] = p.b;
                }
            }
            File.WriteAllBytes(path, rgb);
        }

        static void Finish(string reason, int code)
        {
            Debug.Log("GLOUB_VILLAGE finish reason=" + reason + " code=" + code);
            if (code != 0)
            {
                Directory.CreateDirectory("/tmp/gloub-village-frames");
                File.WriteAllText("/tmp/gloub-village-frames/ERROR.txt", reason);
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
