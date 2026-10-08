using UnityEngine;

/// <summary>
/// Third-person camera for the robot ball (spec §7): yaw/pitch kept independently of the rolling ball, input from
/// the mouse (right drag + wheel) or the touch look area, and a sphere cast that keeps it in front of obstacles.
/// Input is ignored outside FlowState.Playing; the camera still follows.
/// </summary>
public class ThirdPersonOrbitCamera : MonoBehaviour {

	public Transform target;
	public float distance = 7.8f;
	public float minDistance = 3f;
	public float maxDistance = 20f;
	public float yaw;
	public float pitch = 40f;
	public float minPitch = 15f;
	public float maxPitch = 65f;
	public float pivotHeight = 0.5f;
	public float collisionRadius = 0.25f;
	public LayerMask obstacleMask = ~(1 << 2);   // everything except "Ignore Raycast"
	public float mouseDegreesPerUnit = 3f;
	public float zoomSpeed = 0.5f;

	const string SensitivityKey = "camera_sensitivity";

	/// <summary>Look sensitivity multiplier (0.5..2), saved on the device.</summary>
	public static float Sensitivity {
		get { return Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), 0.5f, 2f); }
		set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, 0.5f, 2f)); }
	}

	private Vector2 pendingLook;

	/// <summary>
	/// New (yaw, pitch) after a look drag of deltaPixels on an area areaWidth pixels wide: the full width turns 180
	/// degrees (times sensitivity); dragging up looks down onto the robot. Pitch is clamped.
	/// </summary>
	public static Vector2 ApplyLook(float yaw, float pitch, Vector2 deltaPixels, float areaWidth, float sensitivity,
		float minPitch, float maxPitch) {
		float degreesPerPixel = 180f / Mathf.Max(1f, areaWidth) * sensitivity;
		float newYaw = Mathf.Repeat(yaw + deltaPixels.x * degreesPerPixel, 360f);
		float newPitch = Mathf.Clamp(pitch - deltaPixels.y * degreesPerPixel, minPitch, maxPitch);
		return new Vector2(newYaw, newPitch);
	}

	/// <summary>Queues a look drag in screen pixels (used by touch and tests).</summary>
	public void AddLook(Vector2 deltaPixels) {
		pendingLook += deltaPixels;
	}

	void LateUpdate() {
		if (target == null)
			return;

		if (GameFlow.IsGameplayActive) {
			ReadInput();
		} else {
			pendingLook = Vector2.zero;
			if (TouchLookArea.Current != null)
				TouchLookArea.Current.ConsumeDelta();
		}

		Vector3 pivot = target.position + Vector3.up * pivotHeight;
		Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
		Vector3 back = rotation * Vector3.back;
		transform.position = pivot + back * VisibleDistance(pivot, back);
		transform.rotation = Quaternion.LookRotation(pivot - transform.position);
	}

	void ReadInput() {
		float width = Screen.width * 0.5f;
		if (TouchLookArea.Current != null) {
			pendingLook += TouchLookArea.Current.ConsumeDelta();
			width = TouchLookArea.Current.Width;
		}
		if (pendingLook != Vector2.zero) {
			Vector2 look = ApplyLook(yaw, pitch, pendingLook, width, Sensitivity, minPitch, maxPitch);
			yaw = look.x;
			pitch = look.y;
			pendingLook = Vector2.zero;
		}

		if (Input.GetMouseButton(1)) {
			yaw = Mathf.Repeat(yaw + Input.GetAxis("Mouse X") * mouseDegreesPerUnit * Sensitivity, 360f);
			pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseDegreesPerUnit * Sensitivity, minPitch, maxPitch);
		}
		float scroll = Input.GetAxis("Mouse ScrollWheel");
		if (scroll != 0f)
			distance = Mathf.Clamp(distance * (1f - scroll * zoomSpeed), minDistance, maxDistance);
	}

	// Distance along 'back' that stays in front of the nearest obstacle, ignoring the target and triggers.
	float VisibleDistance(Vector3 pivot, Vector3 back) {
		float nearest = distance;
		foreach (RaycastHit hit in Physics.SphereCastAll(pivot, collisionRadius, back, distance, obstacleMask, QueryTriggerInteraction.Ignore)) {
			if (hit.transform == target || hit.transform.IsChildOf(target))
				continue;
			if (hit.distance > 0f && hit.distance < nearest)
				nearest = hit.distance;
		}
		return Mathf.Max(0.3f, nearest - 0.2f);
	}
}
