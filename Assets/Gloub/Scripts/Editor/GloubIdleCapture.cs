using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor entry: Unity -batchmode -executeMethod GloubIdleCapture.Capture -gloubIdle
// (no -quit; the recorder exits the editor when the clip is written)
public static class GloubIdleCapture
{
    const string ScenePath = "Assets/Scenes/GloubShowcase.unity";

    public static void Capture()
    {
        if (!System.IO.File.Exists(ScenePath))
            throw new System.Exception("Missing " + ScenePath);

        EditorSceneManager.OpenScene(ScenePath);
        if (Object.FindFirstObjectByType<GloubIdleRecorder>() == null)
        {
            var go = new GameObject("GloubIdleRecorder");
            go.AddComponent<GloubIdleRecorder>();
        }

        Debug.Log("GLOUB_IDLE starting play mode capture");
        EditorApplication.isPlaying = true;
    }
}
