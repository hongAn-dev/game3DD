using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Transparent area on the right half of the mobile canvas: the pointer that starts a drag here turns the camera.
/// Other pointers (the joystick finger sliding over, a second finger) are ignored until it lifts (spec §7).
/// </summary>
public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {

	public static TouchLookArea Current { get; private set; }

	private int activePointer = -1;
	private Vector2 delta;

	/// <summary>Width of the area in screen pixels; a drag across all of it turns the camera 180 degrees.</summary>
	public float Width {
		get {
			RectTransform rect = transform as RectTransform;
			if (rect == null)
				return Screen.width * 0.5f;
			Vector3[] corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			Canvas canvas = GetComponentInParent<Canvas>();
			Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			float width = RectTransformUtility.WorldToScreenPoint(cam, corners[2]).x - RectTransformUtility.WorldToScreenPoint(cam, corners[0]).x;
			return width > 1f ? width : Screen.width * 0.5f;
		}
	}

	public void OnPointerDown(PointerEventData eventData) {
		if (activePointer == -1)
			activePointer = eventData.pointerId;
	}

	public void OnDrag(PointerEventData eventData) {
		// eventData.delta is already the distance moved this frame; no deltaTime scaling.
		if (eventData.pointerId == activePointer)
			delta += eventData.delta;
	}

	public void OnPointerUp(PointerEventData eventData) {
		if (eventData.pointerId == activePointer)
			ResetInput();
	}

	/// <summary>Returns the drag since the last call and clears it.</summary>
	public Vector2 ConsumeDelta() {
		Vector2 result = delta;
		delta = Vector2.zero;
		return result;
	}

	public void ResetInput() {
		activePointer = -1;
		delta = Vector2.zero;
	}

	void OnEnable() {
		Current = this;
	}

	void OnDisable() {
		ResetInput();
		if (Current == this)
			Current = null;
	}

	void OnApplicationFocus(bool focused) {
		if (!focused)
			ResetInput();
	}

	void OnApplicationPause(bool paused) {
		if (paused)
			ResetInput();
	}
}
