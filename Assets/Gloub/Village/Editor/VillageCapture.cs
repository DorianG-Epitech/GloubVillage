using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gloub.Village.Editor
{
    /// <summary>
    /// Play-mode harness entry points. Video uses the same RGB24 + captureFramerate
    /// path as GloubIdleRecorder (VillageVideoRecorder).
    /// </summary>
    public static class VillageCapture
    {
        public static void BuildAndSimulate()
        {
            VillageBuilder.BuildAndSimulate();
        }

        /// <summary>Rebuild the scene (spawn yaw) and run the facing verification stills.</summary>
        public static void CaptureFacing()
        {
            VillageBuilder.Build();
            EditorSceneManager.OpenScene(VillageBuilder.ScenePath);
            var go = new GameObject("VillageFacingCheck");
            go.AddComponent<VillageFacingCheck>();
            EditorApplication.isPlaying = true;
        }

        public static void CaptureVideo()
        {
            if (!System.IO.File.Exists(VillageBuilder.ScenePath))
                throw new System.Exception("Missing " + VillageBuilder.ScenePath);

            EditorSceneManager.OpenScene(VillageBuilder.ScenePath);
            if (Object.FindFirstObjectByType<VillageVideoRecorder>() == null)
            {
                var go = new GameObject("VillageVideoRecorder");
                go.AddComponent<VillageVideoRecorder>();
            }

            Debug.Log("GLOUB_VILLAGE starting play mode video capture");
            EditorApplication.isPlaying = true;
        }
    }
}
