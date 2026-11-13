using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GloubBuilder
{
    const string Root = "Assets/Gloub";
    const string MeshDir = Root + "/Meshes";
    const string MatDir = Root + "/Materials";
    const string PrefabPath = Root + "/Gloub.prefab";
    const string ScenePath = "Assets/Scenes/GloubShowcase.unity";

    // --- Proportions (world units). Face looks toward -Z (camera at -Z). ---
    // Fitted to the concept: chubby bean (wide through the top), not a pointy pear.
    const float BodyBottomY = 0.16f;
    const float BodyH = 1.50f;
    const float EyeRadius = 0.038f;
    const float EyeX = 0.202f;
    const float EyeY = 1.015f;
    const float MouthY = 0.955f;
    const float MouthW = 0.152f;
    const float MouthAmp = 0.0185f;
    const float MouthRadius = 0.0120f;
    const float HandY = 0.665f;
    const float HandX = 0.150f;
    const float HandR = 0.094f;
    const float ShoulderY = 0.75f;

    public static void Build()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Gloub/Meshes"));
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Gloub/Materials"));
        if (!AssetDatabase.IsValidFolder("Assets/Gloub"))
            AssetDatabase.CreateFolder("Assets", "Gloub");
        if (!AssetDatabase.IsValidFolder(MeshDir))
            AssetDatabase.CreateFolder(Root, "Meshes");
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder(Root, "Materials");

        var body = SaveMesh(BuildBody(), MeshDir + "/Body.asset");
        var foot = SaveMesh(BuildEllipsoid(0.155f, 0.112f, 0.168f, 18, 24), MeshDir + "/Foot.asset");
        var armL = SaveMesh(BuildArm(-1f), MeshDir + "/ArmL.asset");
        var armR = SaveMesh(BuildArm(1f), MeshDir + "/ArmR.asset");
        var paw = SaveMesh(BuildSphere(HandR, 16, 20), MeshDir + "/Paw.asset");
        var shoulder = SaveMesh(BuildSphere(0.094f, 14, 18), MeshDir + "/Shoulder.asset");
        var stem = SaveMesh(BuildStem(), MeshDir + "/Stem.asset");
        var leaf = SaveMesh(BuildLeaf(0.33f, 0.178f, 0.072f), MeshDir + "/Leaf.asset");
        var eye = SaveMesh(BuildSphere(EyeRadius, 18, 24), MeshDir + "/Eye.asset");
        var highlight = SaveMesh(BuildSphere(EyeRadius * 0.22f, 10, 14), MeshDir + "/EyeHighlight.asset");
        var mouth = SaveMesh(BuildMouth(), MeshDir + "/Mouth.asset");

        var blue = MakeLit("M_GloubBody", new Color(0.60f, 0.80f, 0.91f), 0.25f, 0f);
        var green = MakeLit("M_GloubLeaf", new Color(0.60f, 0.70f, 0.46f), 0.32f, 0f);
        var stemMat = MakeLit("M_GloubStem", new Color(0.46f, 0.58f, 0.34f), 0.28f, 0f);
        var blackGloss = MakeLit("M_GloubEye", new Color(0.04f, 0.04f, 0.045f), 0.88f, 0.05f);
        var blackMatte = MakeLit("M_GloubMouth", new Color(0.07f, 0.07f, 0.075f), 0.18f, 0f);
        var whiteUnlit = MakeUnlit("M_GloubHighlight", Color.white);

        var root = new GameObject("Gloub");
        var anim = new GameObject("AnimRoot");
        anim.transform.SetParent(root.transform, false);

        MakePart("Body", anim.transform, Vector3.zero, Quaternion.identity, body, blue);
        MakePart("ArmL", anim.transform, Vector3.zero, Quaternion.identity, armL, blue);
        MakePart("ArmR", anim.transform, Vector3.zero, Quaternion.identity, armR, blue);
        ArmJoints(-1f, out Vector3 shL, out Vector3 hdL);
        ArmJoints(1f, out Vector3 shR, out Vector3 hdR);
        MakePart("ShoulderL", anim.transform, shL, Quaternion.identity, shoulder, blue);
        MakePart("ShoulderR", anim.transform, shR, Quaternion.identity, shoulder, blue);
        var pawL = MakePart("HandL", anim.transform, hdL, Quaternion.identity, paw, blue);
        var pawR = MakePart("HandR", anim.transform, hdR, Quaternion.identity, paw, blue);
        pawL.transform.localScale = new Vector3(1.12f, 0.92f, 1.08f);
        pawR.transform.localScale = new Vector3(1.12f, 0.92f, 1.08f);

        var sprout = new GameObject("Sprout");
        sprout.transform.SetParent(anim.transform, false);
        MakePart("Stem", sprout.transform, Vector3.zero, Quaternion.identity, stem, stemMat);
        var leafGo = MakePart("Leaf", sprout.transform, LeafPosition(), LeafRotation(), leaf, green);

        var lEye = MakePart("LeftEye", anim.transform, EyePos(-1f), Quaternion.identity, eye, blackGloss);
        var rEye = MakePart("RightEye", anim.transform, EyePos(1f), Quaternion.identity, eye, blackGloss);
        MakePart("Highlight", lEye.transform, EyeHighlightLocal(), Quaternion.identity, highlight, whiteUnlit);
        MakePart("Highlight", rEye.transform, EyeHighlightLocal(), Quaternion.identity, highlight, whiteUnlit);
        MakePart("Mouth", anim.transform, MouthPos(), Quaternion.identity, mouth, blackMatte);

        MakePart("LeftFoot", root.transform, new Vector3(-0.195f, 0.110f, -0.055f), Quaternion.Euler(10f, -8f, 0f), foot, blue);
        MakePart("RightFoot", root.transform, new Vector3(0.195f, 0.110f, -0.055f), Quaternion.Euler(10f, 8f, 0f), foot, blue);

        var idle = root.AddComponent<GloubIdle>();
        idle.animRoot = anim.transform;
        idle.leaf = leafGo.transform;
        idle.leftEye = lEye.transform;
        idle.rightEye = rEye.transform;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        BuildShowcaseScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Gloub prefab saved: " + PrefabPath);
    }

    public static void BuildAndRender()
    {
        Build();
        GloubShowcaseRender.Capture();
    }

    static void BuildShowcaseScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.92f, 0.88f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.78f, 0.74f, 0.68f);
        RenderSettings.ambientGroundColor = new Color(0.62f, 0.58f, 0.52f);
        RenderSettings.fog = false;
        RenderSettings.subtractiveShadowColor = new Color(0.55f, 0.48f, 0.40f);

        var cream = new Color(0.988f, 0.925f, 0.835f);

        var floorMat = MakeLit("M_GloubFloor", new Color(0.88f, 0.82f, 0.73f), 0.10f, 0f);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0f, 0f, 0f);
        floor.transform.localScale = new Vector3(8f, 1f, 8f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

        var keyGo = new GameObject("KeyLight");
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.97f, 0.92f);
        key.intensity = 1.05f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.32f;
        key.shadowBias = 0.04f;
        keyGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.40f, -0.88f, 0.30f));

        var fillGo = new GameObject("FillLight");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.82f, 0.88f, 1f);
        fill.intensity = 0.42f;
        fill.shadows = LightShadows.None;
        fillGo.transform.rotation = Quaternion.LookRotation(new Vector3(-0.55f, -0.28f, 0.18f));

        var rimGo = new GameObject("RimLight");
        var rim = rimGo.AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.color = new Color(1f, 0.98f, 0.94f);
        rim.intensity = 0.18f;
        rim.shadows = LightShadows.None;
        rimGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.05f, -0.15f, -0.9f));

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = cream;
        cam.fieldOfView = 24f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 40f;
        cam.allowMSAA = true;
        camGo.transform.position = new Vector3(0f, 1.02f, -5.8f);
        camGo.transform.LookAt(new Vector3(0f, 1.00f, 0f));
        camGo.AddComponent<AudioListener>();
        var add = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (add != null) add.renderPostProcessing = false;

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Saved showcase scene " + ScenePath);
    }

    static GameObject MakePart(string name, Transform parent, Vector3 localPos, Quaternion localRot, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;
        return go;
    }

    static Vector3 EyePos(float side)
    {
        float z = FrontZ(EyeX * side, EyeY) - EyeRadius * 0.90f;
        return new Vector3(EyeX * side, EyeY, z);
    }

    static Vector3 EyeHighlightLocal()
    {
        return new Vector3(0.012f, 0.016f, -EyeRadius * 0.78f);
    }

    static Vector3 MouthPos()
    {
        return new Vector3(0f, MouthY, FrontZ(0f, MouthY) - 0.003f);
    }

    static Vector3 LeafPosition()
    {
        StemPoint(1f, out Vector3 p, out Vector3 tan);
        return p - tan.normalized * 0.025f;
    }

    static Quaternion LeafRotation()
    {
        StemPoint(1f, out _, out Vector3 tan);
        // Leaf mesh: length along +Y, width along +X, thickness along +Z.
        // Continue the stem, present the broad face to the camera (-Z) with a slight tilt.
        Vector3 up = tan.normalized;
        Vector3 face = new Vector3(0.08f, 0.55f, -1f).normalized;
        Vector3 right = Vector3.Cross(up, face).normalized;
        if (right.sqrMagnitude < 0.01f) right = Vector3.right;
        face = Vector3.Cross(right, up).normalized;
        return Quaternion.LookRotation(face, up);
    }

    // -------- profile / body --------
    static float BodyRadius(float t)
    {
        t = Mathf.Clamp01(t);
        float h = t * BodyH;
        const float rx = 0.658f;
        const float botRy = 0.38f;
        const float topRx = 0.575f;
        const float topRy = 0.50f;
        if (h <= botRy)
        {
            float nk = (botRy - h) / botRy;
            return rx * Mathf.Sqrt(Mathf.Max(0f, 1f - nk * nk));
        }
        float topStart = BodyH - topRy;
        if (h >= topStart)
        {
            float nk = (h - topStart) / topRy;
            return topRx * Mathf.Sqrt(Mathf.Max(0f, 1f - nk * nk));
        }
        float u = (h - botRy) / (topStart - botRy);
        float ub = Mathf.Pow(u, 1.28f);
        float s = ub * ub * (3f - 2f * ub);
        return Mathf.Lerp(rx, topRx, s);
    }

    static float BodyY(float t) => BodyBottomY + t * BodyH;

    static float FrontZ(float x, float y)
    {
        float t = Mathf.Clamp01((y - BodyBottomY) / BodyH);
        float r = BodyRadius(t);
        float inner = r * r - x * x;
        if (inner < 0f) return 0f;
        return -Mathf.Sqrt(inner);
    }

    static Mesh BuildBody()
    {
        const int stacks = 80;
        const int slices = 80;
        var verts = new List<Vector3>();
        var nrm = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();
        const float dt = 0.0015f;

        for (int i = 0; i <= stacks; i++)
        {
            float t = i / (float)stacks;
            float y = BodyY(t);
            float r = BodyRadius(t);
            float rp = (BodyRadius(Mathf.Min(1f, t + dt)) - BodyRadius(Mathf.Max(0f, t - dt))) / (2f * dt);
            for (int j = 0; j <= slices; j++)
            {
                float a = j / (float)slices * Mathf.PI * 2f;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);
                verts.Add(new Vector3(r * ca, y, r * sa));
                // Outward normal of the surface of revolution (see derivation in comments historically).
                var n = new Vector3(BodyH * ca, -rp, BodyH * sa);
                if (n.sqrMagnitude < 1e-8f) n = (t < 0.5f) ? Vector3.down : Vector3.up;
                nrm.Add(n.normalized);
                uv.Add(new Vector2(j / (float)slices, t));
            }
        }

        int stride = slices + 1;
        for (int i = 0; i < stacks; i++)
        {
            for (int j = 0; j < slices; j++)
            {
                int a0 = i * stride + j;
                int b0 = a0 + stride;
                tris.Add(a0); tris.Add(b0); tris.Add(a0 + 1);
                tris.Add(a0 + 1); tris.Add(b0); tris.Add(b0 + 1);
            }
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetNormals(nrm);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 PushOutside(Vector3 p, float margin)
    {
        float r = BodyRadius(Mathf.Clamp01((p.y - BodyBottomY) / BodyH));
        var xz = new Vector3(p.x, 0f, p.z);
        float d = xz.magnitude;
        if (d < 1e-5f) return p;
        if (d < r + margin)
        {
            float s = (r + margin) / d;
            return new Vector3(p.x * s, p.y, p.z * s);
        }
        return p;
    }

    static void ArmJoints(float side, out Vector3 shoulder, out Vector3 hand)
    {
        ArmPath(side, out shoulder, out hand, out _);
    }

    static void ArmPath(float side, out Vector3 shoulder, out Vector3 hand, out List<Vector3> path)
    {
        float sy = ShoulderY;
        float sr = BodyRadius((sy - BodyBottomY) / BodyH);
        float sAng = 48f * Mathf.Deg2Rad;
        float sOff = sr + 0.006f;
        shoulder = new Vector3(side * sOff * Mathf.Sin(sAng), sy, -sOff * Mathf.Cos(sAng));
        hand = new Vector3(side * HandX, HandY, FrontZ(HandX, HandY) - HandR * 0.88f);
        Vector3 mid = Vector3.Lerp(shoulder, hand, 0.40f);
        mid += new Vector3(side * 0.02f, -0.008f, -0.015f);
        mid = PushOutside(mid, 0.062f);
        shoulder = PushOutside(shoulder, 0.048f);
        hand = PushOutside(hand, 0.062f);

        path = SampleBezier(
            shoulder,
            Vector3.Lerp(shoulder, mid, 0.55f),
            Vector3.Lerp(mid, hand, 0.45f),
            hand,
            20);
        for (int i = 0; i < path.Count; i++)
            path[i] = PushOutside(path[i], Mathf.Lerp(0.062f, 0.055f, i / (float)(path.Count - 1)));
        shoulder = path[0];
        hand = path[path.Count - 1];
    }

    static Mesh BuildArm(float side)
    {
        ArmPath(side, out _, out _, out var path);
        return BuildTube(path, t => Mathf.Lerp(0.094f, 0.078f, t), 16, caps: false);
    }

    static Mesh BuildStem()
    {
        var path = new List<Vector3>();
        for (int i = 0; i <= 16; i++)
        {
            StemPoint(i / 16f, out Vector3 p, out _);
            path.Add(p);
        }
        return BuildTube(path, t => Mathf.Lerp(0.038f, 0.028f, t), 16);
    }

    static void StemPoint(float t, out Vector3 p, out Vector3 tan)
    {
        float top = BodyBottomY + BodyH;
        Vector3 a = new Vector3(0f, top - 0.04f, 0.02f);
        Vector3 b = new Vector3(0.055f, top + 0.09f, 0.03f);
        Vector3 c = new Vector3(0.16f, top + 0.175f, 0.045f);
        Vector3 d = new Vector3(0.27f, top + 0.185f, 0.055f);
        p = Bezier(a, b, c, d, t);
        float dt = 0.02f;
        Vector3 p1 = Bezier(a, b, c, d, Mathf.Clamp01(t + dt));
        Vector3 p0 = Bezier(a, b, c, d, Mathf.Clamp01(t - dt));
        tan = (p1 - p0).normalized;
    }

    static Mesh BuildLeaf(float length, float width, float thick)
    {
        const int lu = 22;
        const int lv = 14;
        var verts = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();

        // Local: +Y along length (base -> tip), +X width, +Z thickness/face.
        for (int i = 0; i <= lu; i++)
        {
            float u = i / (float)lu;
            float halfW = width * 0.5f * Mathf.Sin(Mathf.PI * u);
            if (halfW < 0.0005f) halfW = 0.0005f;
            for (int j = 0; j <= lv; j++)
            {
                float v = j / (float)lv * 2f - 1f; // -1..1
                float x = v * halfW;
                float y = (u - 0.04f) * length;
                float profile = 1f - v * v;
                float z = thick * profile * Mathf.Sin(Mathf.PI * u);
                verts.Add(new Vector3(x, y, z));
                uv.Add(new Vector2(u, (v + 1f) * 0.5f));
            }
        }

        int stride = lv + 1;
        for (int i = 0; i < lu; i++)
        {
            for (int j = 0; j < lv; j++)
            {
                int a = i * stride + j;
                int b = a + stride;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }

        // underside (thinner, opposite face) so it isn't one-sided
        int extra = verts.Count;
        int n = extra;
        for (int k = 0; k < n; k++)
        {
            var p = verts[k];
            verts.Add(new Vector3(p.x, p.y, -p.z * 0.35f));
            uv.Add(uv[k]);
        }
        int tcount = tris.Count;
        for (int k = 0; k < tcount; k += 3)
        {
            tris.Add(tris[k] + extra);
            tris.Add(tris[k + 2] + extra);
            tris.Add(tris[k + 1] + extra);
        }

        return Finalize(verts, tris, uv);
    }

    static Mesh BuildMouth()
    {
        const int samples = 40;
        var path = new List<Vector3>();
        for (int i = 0; i <= samples; i++)
        {
            float e = i / (float)samples;
            float t = Mathf.Lerp(-1f, 1f, e);
            float x = t * MouthW * 0.5f;
            // two rounded dips = 'w' / cat mouth
            float y = -MouthAmp * Mathf.Abs(Mathf.Sin(Mathf.PI * t));
            // slight upturn at the ends
            y += 0.004f * t * t;
            float z = FrontZ(x, MouthY + y) - FrontZ(0f, MouthY);
            path.Add(new Vector3(x, y, z));
        }
        return BuildTube(path, _ => MouthRadius, 8);
    }

    static Mesh BuildSphere(float radius, int stacks, int slices)
    {
        var verts = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= stacks; i++)
        {
            float v = i / (float)stacks;
            float phi = v * Mathf.PI;
            for (int j = 0; j <= slices; j++)
            {
                float u = j / (float)slices;
                float th = u * Mathf.PI * 2f;
                float x = radius * Mathf.Sin(phi) * Mathf.Cos(th);
                float y = radius * Mathf.Cos(phi);
                float z = radius * Mathf.Sin(phi) * Mathf.Sin(th);
                verts.Add(new Vector3(x, y, z));
                uv.Add(new Vector2(u, 1f - v));
            }
        }
        int stride = slices + 1;
        for (int i = 0; i < stacks; i++)
        for (int j = 0; j < slices; j++)
        {
            int a = i * stride + j;
            int b = a + stride;
            // Sphere stacks travel +Y -> -Y, opposite the body lathe, so winding is flipped vs BuildBody.
            tris.Add(a); tris.Add(a + 1); tris.Add(b);
            tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
        }
        return Finalize(verts, tris, uv);
    }

    static Mesh BuildEllipsoid(float rx, float ry, float rz, int stacks, int slices)
    {
        var m = BuildSphere(1f, stacks, slices);
        var v = m.vertices;
        for (int i = 0; i < v.Length; i++)
            v[i] = new Vector3(v[i].x * rx, v[i].y * ry, v[i].z * rz);
        m.vertices = v;
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }

    static Mesh BuildTube(IList<Vector3> path, System.Func<float, float> radius, int radial, bool caps = true)
    {
        if (path.Count < 2) return new Mesh();
        var frames = ParallelFrames(path);
        var verts = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();

        for (int i = 0; i < path.Count; i++)
        {
            float t = i / (float)(path.Count - 1);
            float r = radius(t);
            Vector3 n = frames[i][0];
            Vector3 b = frames[i][1];
            for (int j = 0; j <= radial; j++)
            {
                float a = j / (float)radial * Mathf.PI * 2f;
                Vector3 offset = (n * Mathf.Cos(a) + b * Mathf.Sin(a)) * r;
                verts.Add(path[i] + offset);
                uv.Add(new Vector2(j / (float)radial, t));
            }
        }

        int stride = radial + 1;
        for (int i = 0; i < path.Count - 1; i++)
        for (int j = 0; j < radial; j++)
        {
            int a = i * stride + j;
            int c = a + stride;
            // Winding so (path tangent) × (increasing angle) points outward.
            tris.Add(a); tris.Add(a + 1); tris.Add(c);
            tris.Add(a + 1); tris.Add(c + 1); tris.Add(c);
        }

        if (caps)
        {
            int bottomCenter = verts.Count;
            verts.Add(path[0]);
            uv.Add(new Vector2(0.5f, 0f));
            int topCenter = verts.Count;
            verts.Add(path[path.Count - 1]);
            uv.Add(new Vector2(0.5f, 1f));
            for (int j = 0; j < radial; j++)
            {
                int baseI = (path.Count - 1) * stride;
                tris.Add(bottomCenter); tris.Add(j); tris.Add(j + 1);
                tris.Add(bottomCenter); tris.Add(j + 1); tris.Add(j);
                tris.Add(topCenter); tris.Add(baseI + j + 1); tris.Add(baseI + j);
                tris.Add(topCenter); tris.Add(baseI + j); tris.Add(baseI + j + 1);
            }
        }

        return Finalize(verts, tris, uv);
    }

    static List<Vector3[]> ParallelFrames(IList<Vector3> path)
    {
        var frames = new List<Vector3[]>(path.Count);
        Vector3 tan0 = (path[1] - path[0]).normalized;
        Vector3 n = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(n, tan0)) > 0.9f) n = Vector3.right;
        Vector3 b = Vector3.Cross(tan0, n).normalized;
        n = Vector3.Cross(b, tan0).normalized;
        frames.Add(new[] { n, b });
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 prevT = (i == 1 ? tan0 : (path[i] - path[i - 1]).normalized);
            Vector3 tan = (i < path.Count - 1 ? (path[i + 1] - path[i - 1]).normalized : (path[i] - path[i - 1]).normalized);
            // rotate previous normal by the tangent change
            Vector3 axis = Vector3.Cross(prevT, tan);
            if (axis.sqrMagnitude > 1e-8f)
            {
                float ang = Vector3.Angle(prevT, tan);
                Quaternion q = Quaternion.AngleAxis(ang, axis.normalized);
                n = q * n;
                b = q * b;
            }
            frames.Add(new[] { n.normalized, b.normalized });
        }
        return frames;
    }

    static List<Vector3> SampleBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int steps)
    {
        var list = new List<Vector3>(steps + 1);
        for (int i = 0; i <= steps; i++)
            list.Add(Bezier(a, b, c, d, i / (float)steps));
        return list;
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    static float Interp(Vector2[] pts, float t)
    {
        if (t <= pts[0].x) return pts[0].y;
        if (t >= pts[pts.Length - 1].x) return pts[pts.Length - 1].y;
        int i = 0;
        while (i < pts.Length - 2 && pts[i + 1].x < t) i++;
        int i0 = Mathf.Max(0, i - 1);
        int i1 = i;
        int i2 = Mathf.Min(pts.Length - 1, i + 1);
        int i3 = Mathf.Min(pts.Length - 1, i + 2);
        float span = pts[i2].x - pts[i1].x;
        float u = (t - pts[i1].x) / Mathf.Max(1e-5f, span);
        float p0 = pts[i0].y, p1 = pts[i1].y, p2 = pts[i2].y, p3 = pts[i3].y;
        float u2 = u * u, u3 = u2 * u;
        return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
    }

    static Mesh Combine(Mesh a, Mesh b, Vector3 bPos, Quaternion bRot, Vector3 bScale)
    {
        var verts = new List<Vector3>(a.vertices);
        var nrm = new List<Vector3>(a.normals);
        var uv = new List<Vector2>(a.uv);
        var tris = new List<int>(a.triangles);
        int off = verts.Count;
        Matrix4x4 m = Matrix4x4.TRS(bPos, bRot, bScale);
        Matrix4x4 nM = m.inverse.transpose;
        var bv = b.vertices;
        var bn = b.normals;
        var bu = b.uv;
        for (int i = 0; i < bv.Length; i++)
        {
            verts.Add(m.MultiplyPoint3x4(bv[i]));
            nrm.Add(nM.MultiplyVector(i < bn.Length ? bn[i] : Vector3.up).normalized);
            uv.Add(i < bu.Length ? bu[i] : Vector2.zero);
        }
        foreach (var idx in b.triangles) tris.Add(idx + off);
        var mesh = new Mesh { name = a.name + "+" + b.name };
        mesh.SetVertices(verts);
        mesh.SetNormals(nrm);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh Finalize(List<Vector3> verts, List<int> tris, List<Vector2> uv)
    {
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh SaveMesh(Mesh mesh, string path)
    {
        mesh.name = Path.GetFileNameWithoutExtension(path);
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material MakeLit(string name, Color color, float smoothness, float metallic)
    {
        string path = MatDir + "/" + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new System.Exception("URP/Lit not found");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;
        mat.name = name;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.color = color;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MakeUnlit(string name, Color color)
    {
        string path = MatDir + "/" + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
