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
		get {
			// Cached: PlayerPrefs reads go through JNI on Android.
			if (cachedSensitivity < 0f)
				cachedSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), 0.5f, 2f);
			return cachedSensitivity;
		}
		set {
			cachedSensitivity = Mathf.Clamp(value, 0.5f, 2f);
			PlayerPrefs.SetFloat(SensitivityKey, cachedSensitivity);
		}
	}

	private Vector2 pendingLook;
	private float currentDistance = -1f;
	private float distanceVelocity;
	private readonly RaycastHit[] hits = new RaycastHit[16];
	private static float cachedSensitivity = -1f;

	/// <summary>
	/// New (yaw, pitch) after a look drag of deltaPixels on an area areaWidth pixels wide: the full width turns 180
	/// degrees (times sensitivity); dragging up lowers the pitch (the view tilts up). Pitch is clamped.
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
		// Snap in front of obstacles at once, ease back out so the view does not pump.
		float visible = VisibleDistance(pivot, back);
		if (currentDistance < 0f || visible < currentDistance)
			currentDistance = visible;
		else
			currentDistance = Mathf.SmoothDamp(currentDistance, visible, ref distanceVelocity, 0.25f);
		transform.position = pivot + back * currentDistance;
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

	// Distance along 'back' that stays in front of the nearest static obstacle. Ignores triggers, the target and
	// anything with a Rigidbody (enemies, falling scrap) so actors passing behind the ball do not yank the view.
	float VisibleDistance(Vector3 pivot, Vector3 back) {
		float nearest = distance;
		int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, back, hits, distance, obstacleMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < count; i++) {
			RaycastHit hit = hits[i];
			if (hit.collider.attachedRigidbody != null || hit.transform == target || hit.transform.IsChildOf(target))
				continue;
			if (hit.distance > 0f && hit.distance < nearest)
				nearest = hit.distance;
		}
		return Mathf.Max(0.3f, nearest - 0.2f);
	}
}
