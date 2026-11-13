using System.Collections;
using System.IO;
using UnityEngine;

// Runtime capture helper, spawned only by GloubIdleCapture / -gloubIdle.
// Not saved into the showcase scene or prefab. Writes frame_XXXXX.rgb (RGB24, top-down).
public class GloubIdleRecorder : MonoBehaviour
{
    public int width = 1080;
    public int height = 1080;
    public int fps = 30;
    public float duration = 12f;
    public float startAngle = 32f;
    public float endAngle = -32f;
    public float distance = 5.9f;
    public float cameraHeight = 1.10f;
    public Vector3 focus = new Vector3(0f, 1.00f, 0f);
    public string framesDir = "/tmp/gloub-idle-frames";

    IEnumerator Start()
    {
        Application.runInBackground = true;
        Time.captureFramerate = Mathf.Max(1, fps);

        var idle = FindFirstObjectByType<GloubIdle>();
        var cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            Finish("ERROR no camera", 1);
            yield break;
        }

        cam.fieldOfView = 24f;
        cam.allowMSAA = true;
        cam.allowHDR = true;

        PlaceCamera(cam, startAngle);
        cam.Render();
        cam.Render();

        if (idle != null)
            idle.PrepareDeterministicCapture();
        else
            Debug.LogWarning("GLOUB_IDLE no GloubIdle in scene — recording anyway");

        Directory.CreateDirectory(framesDir);
        foreach (var f in Directory.GetFiles(framesDir, "frame_*.rgb"))
            File.Delete(f);
        foreach (var f in Directory.GetFiles(framesDir, "frame_*.png"))
            File.Delete(f);
        foreach (var n in new[] { "DONE.txt", "META.txt", "ERROR.txt" })
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

        Debug.Log("GLOUB_IDLE recording frames=" + total + " res=" + width + "x" + height + " fps=" + fps);

        for (int i = 0; i < total; i++)
        {
            float u = total <= 1 ? 0f : i / (float)(total - 1);
            float s = u * u * (3f - 2f * u);
            PlaceCamera(cam, Mathf.Lerp(startAngle, endAngle, s));
            SaveFrame(cam, rt, tex, rgb, Path.Combine(framesDir, "frame_" + i.ToString("D5") + ".rgb"));
            if (i % 30 == 0)
                Debug.Log("GLOUB_IDLE frame=" + i + " time=" + Time.time.ToString("F3"));
            yield return null;
        }

        Object.Destroy(tex);
        Object.Destroy(rt);

        var meta = "frames=" + total + "\n"
            + "fps=" + fps + "\n"
            + "duration=" + duration.ToString("F2") + "\n"
            + "width=" + width + "\n"
            + "height=" + height + "\n"
            + "startAngle=" + startAngle + "\n"
            + "endAngle=" + endAngle + "\n"
            + "format=rgb24_topdown\n"
            + "blinks_at=2.70,5.95,9.20\n";
        File.WriteAllText(Path.Combine(framesDir, "META.txt"), meta);
        File.WriteAllText(Path.Combine(framesDir, "DONE.txt"), total + "\n" + width + "x" + height + "\n");
        Debug.Log("GLOUB_IDLE done frames=" + total);
        Time.captureFramerate = 0;
        Finish("ok", 0);
    }

    void PlaceCamera(Camera cam, float angDeg)
    {
        float ang = angDeg * Mathf.Deg2Rad;
        cam.transform.position = new Vector3(
            Mathf.Sin(ang) * distance,
            cameraHeight,
            -Mathf.Cos(ang) * distance);
        cam.transform.LookAt(focus);
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
        // Unity tex (0,0) is bottom-left; write top-down RGB24 for ffmpeg/PIL.
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
        Debug.Log("GLOUB_IDLE finish reason=" + reason + " code=" + code);
        if (code != 0)
        {
            Directory.CreateDirectory("/tmp/gloub-idle-frames");
            File.WriteAllText("/tmp/gloub-idle-frames/ERROR.txt", reason);
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
}
