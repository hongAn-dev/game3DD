using UnityEngine;

/// <summary>Spins a ball visual to match how far it moved (the Ending timeline only moves the position).</summary>
public class RollVisual : MonoBehaviour {

	public float radius = 0.5f;

	Vector3 last;

	void OnEnable() {
		last = transform.position;
	}

	void LateUpdate() {
		Vector3 delta = transform.position - last;
		delta.y = 0f;
		last = transform.position;
		if (delta.sqrMagnitude > 0.000001f)
			transform.Rotate(Vector3.Cross(Vector3.up, delta).normalized, delta.magnitude / radius * Mathf.Rad2Deg, Space.World);
	}
}
