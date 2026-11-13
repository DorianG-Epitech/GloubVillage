using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GloubShowcaseRender
{
    const string ScenePath = "Assets/Scenes/GloubShowcase.unity";

    public static void Capture()
    {
        if (!File.Exists(ScenePath))
            throw new System.Exception("Missing " + ScenePath + " — run GloubBuilder.Build first");

        EditorSceneManager.OpenScene(ScenePath);
        var cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null) throw new System.Exception("No camera in showcase scene");

        Directory.CreateDirectory("/workspace/gloub");

        Vector3 focus = new Vector3(0f, 1.00f, 0f);
        cam.fieldOfView = 24f;
        cam.allowMSAA = true;
        cam.allowHDR = true;

        // Warm up URP lighting — the first Camera.Render after a scene load
        // can come back unlit/black in batchmode.
        cam.transform.position = new Vector3(0f, 1.02f, -5.8f);
        cam.transform.LookAt(focus);
        cam.Render();
        cam.Render();

        RenderTo(cam, "/workspace/gloub/gloub_front.png");

        float ang = 32f * Mathf.Deg2Rad;
        float dist = 5.8f;
        cam.transform.position = new Vector3(Mathf.Sin(ang) * dist, 1.12f, -Mathf.Cos(ang) * dist);
        cam.transform.LookAt(focus);
        RenderTo(cam, "/workspace/gloub/gloub_34.png");

        cam.transform.position = new Vector3(0f, 1.02f, -5.8f);
        cam.transform.LookAt(focus);
        RenderTo(cam, "/workspace/gloub/gloub_front.png");

        Debug.Log("GLOUB rendered front + 3/4 views");
    }

    static void RenderTo(Camera cam, string path)
    {
        const int s = 1024;
        var rt = new RenderTexture(s, s, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        rt.Create();
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(s, s, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, s, s), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log("Wrote " + path + " bytes=" + new FileInfo(path).Length);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
    }
}
