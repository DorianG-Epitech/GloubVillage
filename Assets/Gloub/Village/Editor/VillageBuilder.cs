using Gloub.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace Gloub.Village.Editor
{
    public static class VillageBuilder
    {
        public const string ScenePath = "Assets/Scenes/GloubVillage.unity";
        public const string PrefabPath = "Assets/Gloub/Gloub.prefab";

        static readonly string[] Names =
        {
            "Glim", "Nuage", "Pépin", "Lune", "Mochi", "Kiki", "Bleuet", "Pompom"
        };

        static readonly float[] HueShifts =
        {
            0.00f, 0.035f, -0.03f, 0.06f, -0.05f, 0.02f, 0.08f, -0.015f
        };

        [MenuItem("Gloub/Build Village Scene")]
        public static void Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("Missing Gloub prefab at " + PrefabPath);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.88f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.60f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.33f, 0.28f);
            RenderSettings.fog = false;
            RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.40f, 0.45f);
            RenderSettings.skybox = null;

            var grass = Lit(new Color(0.46f, 0.62f, 0.40f), 0.18f);
            var dirt = Lit(new Color(0.62f, 0.52f, 0.38f), 0.22f);
            var stone = Lit(new Color(0.62f, 0.62f, 0.64f), 0.28f);
            var water = Lit(new Color(0.28f, 0.52f, 0.78f), 0.85f);
            var wood = Lit(new Color(0.45f, 0.30f, 0.18f), 0.22f);
            var leaf = Lit(new Color(0.34f, 0.58f, 0.30f), 0.25f);
            var fruit = Lit(new Color(0.86f, 0.32f, 0.30f), 0.40f);
            var fruitB = Lit(new Color(0.92f, 0.55f, 0.22f), 0.40f);
            var roof = Lit(new Color(0.78f, 0.38f, 0.30f), 0.30f);
            var flame = Unlit(new Color(1f, 0.55f, 0.18f));
            var flame2 = Unlit(new Color(1f, 0.82f, 0.35f));
            var crate = Lit(new Color(0.55f, 0.40f, 0.24f), 0.22f);

            Color[] houseWalls =
            {
                new Color(0.93f, 0.88f, 0.78f),
                new Color(0.90f, 0.80f, 0.78f),
                new Color(0.82f, 0.88f, 0.80f),
                new Color(0.86f, 0.84f, 0.90f),
                new Color(0.93f, 0.86f, 0.72f),
                new Color(0.88f, 0.90f, 0.84f),
                new Color(0.90f, 0.82f, 0.72f),
                new Color(0.84f, 0.88f, 0.90f)
            };

            var walkable = new GameObject("Walkable");
            var ground = Prim(PrimitiveType.Plane, "Ground", walkable.transform, Vector3.zero, new Vector3(4.2f, 1f, 4.2f), grass);
            ground.isStatic = true;

            var surface = walkable.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

            var props = new GameObject("VillageProps");
            MakePlaza(props.transform, dirt, wood, flame, flame2);
            MakeWell(props.transform, new Vector3(-9.2f, 0f, 3.4f), stone, water, wood);
            Vector3[] bushPos =
            {
                new Vector3(9.4f, 0f, 5.6f),
                new Vector3(-8.2f, 0f, -6.8f),
                new Vector3(7.6f, 0f, -8.4f),
                new Vector3(-1.5f, 0f, 10.2f)
            };
            for (int i = 0; i < bushPos.Length; i++)
                MakeBush(props.transform, bushPos[i], leaf, i % 2 == 0 ? fruit : fruitB);

            MakeCrate(props.transform, new Vector3(3.1f, 0f, -3.4f), crate, wood);

            var homesRoot = new GameObject("Homes").transform;
            homesRoot.SetParent(props.transform, false);
            var homes = new HomeBed[8];
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f + 18f;
                var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                var pos = dir * 13.2f;
                homes[i] = MakeHouse(homesRoot, pos, -dir, houseWalls[i], roof, wood, i);
            }

            // Soft dirt paths from the plaza toward the well, bushes and a few houses.
            var paths = new GameObject("Paths").transform;
            paths.SetParent(props.transform, false);
            MakePath(paths, Vector3.zero, new Vector3(-9.2f, 0f, 3.4f), dirt);
            MakePath(paths, Vector3.zero, bushPos[0], dirt);
            MakePath(paths, Vector3.zero, bushPos[1], dirt);
            MakePath(paths, Vector3.zero, homes[0].transform.position * 0.55f, dirt);
            MakePath(paths, Vector3.zero, homes[3].transform.position * 0.55f, dirt);

            var controllerGo = new GameObject("VillageController");
            var controller = controllerGo.AddComponent<VillageController>();
            controller.Seed = 20261111;

            var simGo = new GameObject("VillageSimDriver");
            simGo.AddComponent<VillageSimDriver>();

            var villagers = new GameObject("Villagers").transform;
            var traits = GloubPersonality.Generate(Names.Length, controller.Seed);
            for (int i = 0; i < Names.Length; i++)
            {
                float ang = i * 45f;
                var spawn = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 4.1f, 0f, Mathf.Cos(ang * Mathf.Deg2Rad) * 4.1f);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, villagers);
                instance.transform.position = spawn;
                instance.transform.rotation = Quaternion.Euler(0f, ang, 0f); // face (-Z) toward the plaza
                float scale = 0.96f + (i % 5) * 0.022f;
                instance.transform.localScale = Vector3.one * scale;
                TintBody(instance, HueShifts[i]);

                var pers = instance.AddComponent<GloubPersonality>();
                pers.Sociabilite = traits[i][0];
                pers.Imagination = traits[i][1];
                pers.Rationalite = traits[i][2];
                pers.Organisation = traits[i][3];
                var villager = instance.AddComponent<GloubVillager>();
                villager.Id = i;
                villager.DisplayName = Names[i];
                villager.Home = homes[i];
                villager.Village = controller;
            }

            MakeLights();
            MakeCamera();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved village scene " + ScenePath);
        }

        [MenuItem("Gloub/Build Village and Simulate")]
        public static void BuildAndSimulate()
        {
            Build();
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        static void MakeLights()
        {
            var key = new GameObject("KeyLight");
            var k = key.AddComponent<Light>();
            k.type = LightType.Directional;
            k.color = new Color(1f, 0.96f, 0.88f);
            k.intensity = 1.12f;
            k.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.LookRotation(new Vector3(0.38f, -0.86f, 0.30f));

            var fill = new GameObject("FillLight");
            var f = fill.AddComponent<Light>();
            f.type = LightType.Directional;
            f.color = new Color(0.72f, 0.82f, 0.95f);
            f.intensity = 0.32f;
            f.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.LookRotation(new Vector3(-0.55f, -0.30f, -0.55f));

            var rim = new GameObject("RimLight");
            var r = rim.AddComponent<Light>();
            r.type = LightType.Directional;
            r.color = new Color(0.95f, 0.90f, 0.85f);
            r.intensity = 0.22f;
            r.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.LookRotation(new Vector3(-0.2f, 0.15f, 0.9f));
        }

        static void MakeCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.70f, 0.84f, 0.92f);
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 90f;
            cam.allowHDR = true;
            go.transform.position = new Vector3(17.2f, 15.0f, -17.0f);
            go.transform.LookAt(new Vector3(0f, 0.35f, 0f));
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = false;
            go.AddComponent<AudioListener>();
        }

        static void MakePlaza(Transform parent, Material dirt, Material wood, Material flame, Material flame2)
        {
            var root = new GameObject("Plaza");
            root.transform.SetParent(parent, false);
            Prim(PrimitiveType.Cylinder, "PlazaDisk", root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(5.6f, 0.02f, 5.6f), dirt);

            var fire = new GameObject("Campfire");
            fire.transform.SetParent(root.transform, false);
            fire.transform.localPosition = Vector3.zero;
            var log1 = Prim(PrimitiveType.Cylinder, "LogA", fire.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.22f, 0.55f, 0.22f), wood);
            log1.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var log2 = Prim(PrimitiveType.Cylinder, "LogB", fire.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.22f, 0.55f, 0.22f), wood);
            log2.transform.localRotation = Quaternion.Euler(0f, 60f, 90f);
            Prim(PrimitiveType.Sphere, "Flame", fire.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.38f, 0.55f, 0.38f), flame);
            Prim(PrimitiveType.Sphere, "FlameCore", fire.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.20f, 0.28f, 0.20f), flame2);

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(fire.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1f, 0.62f, 0.28f);
            pl.intensity = 1.35f;
            pl.range = 9f;
            pl.shadows = LightShadows.None;

            var obs = fire.AddComponent<NavMeshObstacle>();
            obs.carving = true;
            obs.shape = NavMeshObstacleShape.Capsule;
            obs.radius = 0.55f;
            obs.height = 0.8f;

            var spot = root.AddComponent<SocialSpot>();
            var approach = new GameObject("Approach").transform;
            approach.SetParent(root.transform, false);
            approach.localPosition = new Vector3(1.6f, 0f, 0.4f);
            spot.Approach = approach;
        }

        static void MakeWell(Transform parent, Vector3 pos, Material stone, Material water, Material wood)
        {
            var root = new GameObject("Well");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Prim(PrimitiveType.Cylinder, "Stone", root.transform, new Vector3(0f, 0.32f, 0f), new Vector3(1.7f, 0.32f, 1.7f), stone);
            Prim(PrimitiveType.Cylinder, "Water", root.transform, new Vector3(0f, 0.40f, 0f), new Vector3(1.15f, 0.05f, 1.15f), water);
            Prim(PrimitiveType.Cylinder, "Rim", root.transform, new Vector3(0f, 0.66f, 0f), new Vector3(1.78f, 0.05f, 1.78f), stone);
            var postL = Prim(PrimitiveType.Cylinder, "PostL", root.transform, new Vector3(-0.55f, 1.05f, 0f), new Vector3(0.12f, 0.42f, 0.12f), wood);
            var postR = Prim(PrimitiveType.Cylinder, "PostR", root.transform, new Vector3(0.55f, 1.05f, 0f), new Vector3(0.12f, 0.42f, 0.12f), wood);
            Prim(PrimitiveType.Cube, "Roof", root.transform, new Vector3(0f, 1.48f, 0f), new Vector3(1.5f, 0.08f, 0.7f), wood);

            var obs = root.AddComponent<NavMeshObstacle>();
            obs.carving = true;
            obs.shape = NavMeshObstacleShape.Capsule;
            obs.center = new Vector3(0f, 0.4f, 0f);
            obs.radius = 1.05f;
            obs.height = 1.1f;

            var src = root.AddComponent<WaterSource>();
            var approach = new GameObject("Approach").transform;
            approach.SetParent(root.transform, false);
            approach.localPosition = new Vector3(1.45f, 0f, 0.2f);
            src.Approach = approach;
        }

        static void MakeBush(Transform parent, Vector3 pos, Material leaf, Material fruitMat)
        {
            var root = new GameObject("FruitBush");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Prim(PrimitiveType.Sphere, "FoliageA", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.95f, 0.85f, 0.95f), leaf);
            Prim(PrimitiveType.Sphere, "FoliageB", root.transform, new Vector3(0.28f, 0.38f, 0.18f), new Vector3(0.62f, 0.55f, 0.60f), leaf);
            Prim(PrimitiveType.Sphere, "FoliageC", root.transform, new Vector3(-0.22f, 0.32f, -0.16f), new Vector3(0.50f, 0.48f, 0.52f), leaf);

            var f1 = Prim(PrimitiveType.Sphere, "Fruit0", root.transform, new Vector3(0.42f, 0.58f, 0.16f), Vector3.one * 0.28f, fruitMat);
            var f2 = Prim(PrimitiveType.Sphere, "Fruit1", root.transform, new Vector3(-0.30f, 0.62f, 0.28f), Vector3.one * 0.26f, fruitMat);

            var obs = root.AddComponent<NavMeshObstacle>();
            obs.carving = true;
            obs.shape = NavMeshObstacleShape.Capsule;
            obs.center = new Vector3(0f, 0.4f, 0f);
            obs.radius = 0.55f;
            obs.height = 0.9f;

            var bush = root.AddComponent<FruitBush>();
            var approach = new GameObject("Approach").transform;
            approach.SetParent(root.transform, false);
            approach.localPosition = new Vector3(0.95f, 0f, 0.15f);
            bush.Approach = approach;
            bush.Configure(2, 16f, new[] { f1.transform, f2.transform });
        }

        static void MakeCrate(Transform parent, Vector3 pos, Material crate, Material wood)
        {
            var root = new GameObject("FoodStorage");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Prim(PrimitiveType.Cube, "Box", root.transform, new Vector3(0f, 0.38f, 0f), new Vector3(1.05f, 0.75f, 0.85f), crate);
            Prim(PrimitiveType.Cube, "Lid", root.transform, new Vector3(0f, 0.78f, 0f), new Vector3(1.12f, 0.08f, 0.92f), wood);
            var obs = root.AddComponent<NavMeshObstacle>();
            obs.carving = true;
            obs.shape = NavMeshObstacleShape.Box;
            obs.center = new Vector3(0f, 0.4f, 0f);
            obs.size = new Vector3(1.2f, 0.9f, 1.0f);
        }

        static HomeBed MakeHouse(Transform parent, Vector3 pos, Vector3 faceDir, Color wallColor, Material roofMat, Material wood, int index)
        {
            var root = new GameObject("House_" + index);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude < 0.001f) faceDir = Vector3.back;
            root.transform.rotation = Quaternion.LookRotation(faceDir.normalized);

            var wall = Lit(wallColor, 0.22f);
            Prim(PrimitiveType.Cube, "Walls", root.transform, new Vector3(0f, 0.78f, 0f), new Vector3(1.75f, 1.55f, 1.75f), wall);
            Prim(PrimitiveType.Cube, "RoofL", root.transform, new Vector3(0f, 1.68f, -0.38f), new Vector3(2.05f, 0.12f, 1.2f), roofMat)
                .transform.localRotation = Quaternion.Euler(32f, 0f, 0f);
            Prim(PrimitiveType.Cube, "RoofR", root.transform, new Vector3(0f, 1.68f, 0.38f), new Vector3(2.05f, 0.12f, 1.2f), roofMat)
                .transform.localRotation = Quaternion.Euler(-32f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Door", root.transform, new Vector3(0f, 0.55f, 0.90f), new Vector3(0.42f, 1.05f, 0.08f), wood);
            var bed = Prim(PrimitiveType.Cube, "Bed", root.transform, new Vector3(0f, 0.12f, 1.58f), new Vector3(0.95f, 0.16f, 0.72f),
                Lit(new Color(0.78f, 0.52f, 0.52f), 0.35f));
            Prim(PrimitiveType.Cube, "Pillow", bed.transform, new Vector3(0f, 0.7f, -0.28f), new Vector3(0.45f, 0.55f, 0.22f),
                Lit(new Color(0.92f, 0.88f, 0.80f), 0.4f));

            var obs = root.AddComponent<NavMeshObstacle>();
            obs.carving = true;
            obs.shape = NavMeshObstacleShape.Box;
            obs.center = new Vector3(0f, 0.8f, 0f);
            obs.size = new Vector3(1.95f, 1.7f, 1.95f);

            var home = root.AddComponent<HomeBed>();
            home.OwnerId = index;
            var approach = new GameObject("Approach").transform;
            approach.SetParent(root.transform, false);
            approach.localPosition = new Vector3(0f, 0f, 1.58f);
            home.Approach = approach;
            return home;
        }

        static void MakePath(Transform parent, Vector3 a, Vector3 b, Material dirt)
        {
            var mid = (a + b) * 0.5f;
            mid.y = 0.012f;
            var dir = b - a;
            dir.y = 0f;
            var go = Prim(PrimitiveType.Cube, "Path", parent, mid, new Vector3(1.2f, 0.025f, dir.magnitude), dirt);
            if (dir.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(dir.normalized);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static void TintBody(GameObject instance, float hueShift)
        {
            if (Mathf.Abs(hueShift) < 0.0001f) return;
            string[] parts =
            {
                "AnimRoot/Body", "AnimRoot/ArmL", "AnimRoot/ArmR",
                "AnimRoot/HandL", "AnimRoot/HandR", "AnimRoot/ShoulderL", "AnimRoot/ShoulderR",
                "LeftFoot", "RightFoot"
            };
            Material tinted = null;
            for (int i = 0; i < parts.Length; i++)
            {
                var t = instance.transform.Find(parts[i]);
                if (t == null) continue;
                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;
                if (tinted == null)
                {
                    tinted = new Material(mr.sharedMaterial);
                    var c = tinted.HasProperty("_BaseColor") ? tinted.GetColor("_BaseColor") : tinted.color;
                    Color.RGBToHSV(c, out float h, out float s, out float v);
                    h = Mathf.Repeat(h + hueShift, 1f);
                    var nc = Color.HSVToRGB(h, Mathf.Clamp01(s), v);
                    if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", nc);
                    tinted.color = nc;
                }
                mr.sharedMaterial = tinted;
            }
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
            return go;
        }

        static Material Lit(Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        static Material Unlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }
    }
}
