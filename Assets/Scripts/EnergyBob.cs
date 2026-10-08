using UnityEngine;

/// <summary>
/// Slow spin and gentle bob for an energy crystal's visual child; the trigger on the root stays still (spec §4.1).
/// </summary>
public class EnergyBob : MonoBehaviour {

	public float degreesPerSecond = 90f;
	public float bobHeight = 0.08f;
	public float bobFrequency = 1f;

	private Vector3 basePosition;
	private float phase;

	void Start() {
		basePosition = transform.localPosition;
		phase = Random.value * Mathf.PI * 2f;
	}

	void Update() {
		transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
		Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
		float offset = Mathf.Sin(phase + Time.time * bobFrequency * Mathf.PI * 2f) * bobHeight / Mathf.Max(0.0001f, parentScale.y);
		transform.localPosition = basePosition + Vector3.up * offset;
	}
}
