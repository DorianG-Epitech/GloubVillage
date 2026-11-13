using UnityEngine;

public class GloubIdle : MonoBehaviour
{
    public Transform animRoot;
    public Transform leaf;
    public Transform leftEye;
    public Transform rightEye;

    [Range(0.2f, 3f)] public float breathSpeed = 1.15f;
    [Range(0f, 0.08f)] public float breathAmount = 0.02f;
    [Range(0f, 20f)] public float leafSwayDegrees = 5.5f;
    public float leafSwaySpeed = 1.35f;
    public float blinkMinGap = 2.4f;
    public float blinkMaxGap = 5.2f;
    public float blinkDuration = 0.12f;

    Quaternion _leafBase;
    Vector3 _leftEyeBase;
    Vector3 _rightEyeBase;
    float _nextBlink;
    float _blinkT = -1f;

    void Awake()
    {
        if (leaf != null) _leafBase = leaf.localRotation;
        if (leftEye != null) _leftEyeBase = leftEye.localScale;
        if (rightEye != null) _rightEyeBase = rightEye.localScale;
        _nextBlink = Time.time + Random.Range(blinkMinGap, blinkMaxGap);
    }

    // Capture-only: does not change the authored prefab. Guarantees 3 blinks in a ~12s clip.
    public void PrepareDeterministicCapture()
    {
        blinkMinGap = 3.25f;
        blinkMaxGap = 3.25f;
        _blinkT = -1f;
        _nextBlink = Time.time + 2.7f;
        if (leftEye != null) leftEye.localScale = _leftEyeBase;
        if (rightEye != null) rightEye.localScale = _rightEyeBase;
    }

    void Update()
    {
        if (animRoot != null)
        {
            float b = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
            animRoot.localScale = new Vector3(1f - b * 0.55f, 1f + b, 1f - b * 0.55f);
        }

        if (leaf != null)
        {
            float sway = Mathf.Sin(Time.time * leafSwaySpeed) * leafSwayDegrees;
            float nod = Mathf.Sin(Time.time * leafSwaySpeed * 0.7f + 0.6f) * (leafSwayDegrees * 0.35f);
            leaf.localRotation = _leafBase * Quaternion.Euler(nod, 0f, sway);
        }

        if (_blinkT < 0f && Time.time >= _nextBlink)
            _blinkT = 0f;

        if (_blinkT >= 0f)
        {
            _blinkT += Time.deltaTime;
            float u = Mathf.Clamp01(_blinkT / Mathf.Max(0.04f, blinkDuration));
            float lid = 1f - Mathf.Sin(u * Mathf.PI);
            lid = Mathf.Lerp(0.12f, 1f, lid);
            if (leftEye != null) leftEye.localScale = new Vector3(_leftEyeBase.x, _leftEyeBase.y * lid, _leftEyeBase.z);
            if (rightEye != null) rightEye.localScale = new Vector3(_rightEyeBase.x, _rightEyeBase.y * lid, _rightEyeBase.z);
            if (u >= 1f)
            {
                _blinkT = -1f;
                _nextBlink = Time.time + Random.Range(blinkMinGap, blinkMaxGap);
            }
        }
    }
}
