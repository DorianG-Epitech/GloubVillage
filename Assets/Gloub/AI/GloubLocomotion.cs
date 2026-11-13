using UnityEngine;

namespace Gloub.AI
{
    /// <summary>
    /// Procedural hop / squash on AnimRoot so walking reads on top of GloubIdle
    /// breathing and blinks. Feet stay parented to the prefab root.
    /// Runs in LateUpdate after GloubIdle writes its breath scale.
    /// </summary>
    public sealed class GloubLocomotion : MonoBehaviour
    {
        public Transform AnimRoot;
        public float HopHeight = 0.085f;
        public float HopFrequency = 5.6f;
        public float Stretch = 0.07f;

        GloubMover _mover;
        GloubVillager _villager;
        float _phase;

        public void Setup(Transform animRoot, GloubMover mover, GloubVillager villager)
        {
            AnimRoot = animRoot;
            _mover = mover;
            _villager = villager;
        }

        void LateUpdate()
        {
            if (AnimRoot == null || _mover == null) return;

            bool sleeping = _villager != null && _villager.Needs != null && _villager.Needs.Sleeping;
            float speed = _mover.HorizontalSpeed;
            bool walking = !sleeping && speed > 0.25f;

            if (walking)
            {
                _phase += Time.deltaTime * HopFrequency * Mathf.Clamp(speed / 1.6f, 0.65f, 1.35f);
                float wave = Mathf.Abs(Mathf.Sin(_phase * Mathf.PI));
                AnimRoot.localPosition = new Vector3(0f, wave * HopHeight, 0f);
                var s = AnimRoot.localScale;
                float squash = 1f - wave * Stretch * 0.5f;
                AnimRoot.localScale = new Vector3(s.x * squash, s.y * (1f + wave * Stretch), s.z * squash);
            }
            else
            {
                _phase = 0f;
                var p = AnimRoot.localPosition;
                p.x = 0f;
                p.z = 0f;
                p.y = Mathf.Lerp(p.y, sleeping ? -0.045f : 0f, Time.deltaTime * 8f);
                AnimRoot.localPosition = p;

                var r = AnimRoot.localEulerAngles;
                float targetZ = sleeping ? 12f : 0f;
                r.z = Mathf.LerpAngle(r.z, targetZ, Time.deltaTime * (sleeping ? 3f : 6f));
                AnimRoot.localEulerAngles = r;
            }
        }
    }
}
