using UnityEngine;

/// <summary>Slow spin and gentle bob for a pickup visual child; trigger stays fixed on the root.</summary>
public class EnergyBob : MonoBehaviour {
    public float degreesPerSecond = 90f;
    public float bobHeight = 0.08f;
    public float bobFrequency = 1f;
    public bool groundRing;
    Vector3 basePosition;
    Vector3 baseScale;
    float phase;

    void Start() {
        basePosition = transform.localPosition;
        baseScale = transform.localScale;
        phase = Random.value * Mathf.PI * 2f;
    }
    void Update() {
        if (!GameFlow.IsGameplayActive) return;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        if (parentScale.y < 0.1f) return;
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
        float offset = Mathf.Sin(phase + Time.time * bobFrequency * Mathf.PI * 2f) * bobHeight / Mathf.Max(0.0001f, parentScale.y);
        transform.localPosition = basePosition + Vector3.up * offset;
        if (groundRing) transform.localScale = baseScale * (0.95f + 0.05f * Mathf.Sin(Time.time * 2.2f + phase));
    }
}