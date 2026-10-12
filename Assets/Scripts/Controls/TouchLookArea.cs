using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Transparent area on the right half of the mobile canvas: the pointer that starts a drag here turns the camera.
/// Other pointers (the joystick finger sliding over, a second finger) are ignored until it lifts; everything resets
/// while gameplay is not Playing.
/// </summary>
public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {

	public static TouchLookArea Current { get; private set; }

	// uGUI uses -1 for the left mouse button, so ownership is a flag, not a sentinel id.
	private bool owned;
	private int activePointer;
	private Vector2 delta;
	private Vector2 lastPosition;

	public bool Owned { get { return owned; } }

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
		// Touches report the left button; right/middle mouse drags belong to the camera's own orbit/zoom.
		if (owned || !GameFlow.IsGameplayActive || eventData.button != PointerEventData.InputButton.Left)
			return;
		owned = true;
		activePointer = eventData.pointerId;
		lastPosition = eventData.position;
	}

	public void OnDrag(PointerEventData eventData) {
		// Measured from the last position seen, not eventData.delta: the movement made before the EventSystem's
		// drag threshold was crossed is kept, so a swipe does not start with a dead zone and then jump.
		if (owned && eventData.pointerId == activePointer) {
			delta += eventData.position - lastPosition;
			lastPosition = eventData.position;
		}
	}

	public void OnPointerUp(PointerEventData eventData) {
		if (owned && eventData.pointerId == activePointer)
			ResetInput();
	}

	/// <summary>Returns the drag since the last call and clears it.</summary>
	public Vector2 ConsumeDelta() {
		Vector2 result = delta;
		delta = Vector2.zero;
		return result;
	}

	public void ResetInput() {
		owned = false;
		delta = Vector2.zero;
	}

	void OnEnable() {
		Current = this;
	}

	void Update() {
		if ((owned || delta != Vector2.zero) && !GameFlow.IsGameplayActive)
			ResetInput();
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
