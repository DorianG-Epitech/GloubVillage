using UnityEngine;

namespace Gloub.AI
{
    /// <summary>
    /// World-space name, four need bars, current action label, and a chat heart/bubble.
    /// Built from quads + TextMesh so Camera.Render captures it (OnGUI would not).
    /// </summary>
    public sealed class GloubAgentUI : MonoBehaviour
    {
        static readonly Color[] BarColors =
        {
            new Color(0.30f, 0.62f, 0.95f),
            new Color(0.95f, 0.55f, 0.22f),
            new Color(0.95f, 0.82f, 0.28f),
            new Color(0.95f, 0.42f, 0.62f)
        };

        GloubVillager _villager;
        Transform _billboard;
        TextMesh _nameText;
        TextMesh _actionText;
        TextMesh _bubbleText;
        Transform[] _fills;
        GameObject _bubble;

        public void Setup(GloubVillager villager)
        {
            _villager = villager;
            Build();
        }

        void Build()
        {
            var hud = new GameObject("AgentHUD");
            hud.transform.SetParent(transform, false);
            hud.transform.localPosition = new Vector3(0f, 2.32f, 0f);
            _billboard = hud.transform;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            _nameText = MakeText(hud.transform, "Name", new Vector3(0f, 0.30f, 0f), 0.12f, new Color(0.18f, 0.14f, 0.16f));
            _nameText.font = font;
            _nameText.text = _villager != null ? _villager.DisplayName : name;

            _actionText = MakeText(hud.transform, "Action", new Vector3(0f, 0.16f, 0f), 0.09f, new Color(0.32f, 0.22f, 0.28f));
            _actionText.font = font;
            _actionText.text = "";

            _fills = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float y = 0.04f - i * 0.07f;
                var bg = MakeQuad(hud.transform, "BarBg" + i, new Vector3(0f, y, 0.01f), new Vector3(0.82f, 0.05f, 1f), new Color(0.15f, 0.12f, 0.14f, 0.7f));
                var fill = MakeQuad(hud.transform, "Bar" + i, new Vector3(-0.41f, y, 0f), new Vector3(0.82f, 0.042f, 1f), BarColors[i]);
                _fills[i] = fill.transform;
                _fills[i].localPosition = new Vector3(-0.31f, y, 0f);
            }

            _bubble = new GameObject("Bubble");
            _bubble.transform.SetParent(hud.transform, false);
            _bubble.transform.localPosition = new Vector3(0.42f, 0.34f, 0f);
            _bubbleText = MakeText(_bubble.transform, "Heart", Vector3.zero, 0.14f, new Color(0.92f, 0.28f, 0.42f));
            _bubbleText.font = font;
            _bubbleText.text = "♡";
            _bubble.SetActive(false);
        }

        void LateUpdate()
        {
            if (_billboard != null && Camera.main != null)
            {
                var cam = Camera.main.transform;
                _billboard.rotation = Quaternion.LookRotation(_billboard.position - cam.position, Vector3.up);
            }

            if (_villager == null || _villager.Needs == null) return;
            var n = _villager.Needs;
            SetFill(0, n.Thirst);
            SetFill(1, n.Hunger);
            SetFill(2, n.Energy);
            SetFill(3, n.Social);

            if (_nameText != null) _nameText.text = _villager.Personality != null
                ? _villager.DisplayName + " · " + _villager.Personality.Type
                : _villager.DisplayName;
            if (_actionText != null) _actionText.text = _villager.CurrentActionLabel;

            bool chatting = _villager.IsChatting;
            if (_bubble != null && _bubble.activeSelf != chatting)
                _bubble.SetActive(chatting);
            if (chatting && _bubbleText != null)
            {
                _bubbleText.text = (Mathf.FloorToInt(Time.time * 3f) % 2 == 0) ? "♡" : "♥";
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
                _bubble.transform.localScale = Vector3.one * pulse;
            }
        }

        const float BarWidth = 0.82f;
        const float BarHeight = 0.042f;

        void SetFill(int index, float amount)
        {
            amount = Mathf.Clamp01(amount);
            var t = _fills[index];
            t.localScale = new Vector3(Mathf.Max(0.001f, BarWidth * amount), BarHeight, 1f);
            var p = t.localPosition;
            p.x = -BarWidth * 0.5f + (BarWidth * amount) * 0.5f;
            t.localPosition = p;
        }

        public void FaceCamera(Camera cam)
        {
            if (_billboard == null || cam == null) return;
            _billboard.rotation = Quaternion.LookRotation(_billboard.position - cam.transform.position, Vector3.up);
        }

        static TextMesh MakeText(Transform parent, string name, Vector3 localPos, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            var tm = go.AddComponent<TextMesh>();
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = size;
            tm.fontSize = 64;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            tm.text = "";
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            return tm;
        }

        static GameObject MakeQuad(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mr.sharedMaterial = mat;
            return go;
        }
    }
}
