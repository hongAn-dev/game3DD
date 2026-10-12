using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Transparent area on the right half of the mobile canvas: the pointer that starts a drag here turns the camera.
/// Movement joystick and other configured UI regions are excluded even when transparent canvas graphics overlap.
/// Other pointers (the joystick finger sliding over, a second finger) are ignored until it lifts; everything resets
/// while gameplay is not Playing.
/// </summary>
public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler,
	IEndDragHandler, ICancelHandler {

	public static TouchLookArea Current { get; private set; }

	[Header("Input exclusion")]
	[Tooltip("Optional UI regions that must never start a camera swipe (for example an action pad).")]
	[SerializeField] RectTransform[] excludedRegions = new RectTransform[0];
	[Tooltip("When enabled, the active FixedJoystick region is excluded automatically.")]
	[SerializeField] bool excludeMovementJoystick = true;

	// uGUI uses -1 for the left mouse button, so ownership is a flag, not a sentinel id.
	private bool owned;
	private int activePointer;
	private Vector2 delta;
	private Vector2 lastPosition;

	public bool Owned { get { return owned; } }

	/// <summary>Returns true when a screen point is inside a movement or explicitly excluded UI region.</summary>
	public bool IsScreenPositionExcluded(Vector2 screenPosition, Camera eventCamera) {
		if (excludeMovementJoystick && FixedJoystick.Current != null &&
			Contains(FixedJoystick.Current.InputRegion, screenPosition, eventCamera))
			return true;
		if (excludedRegions != null)
			for (int i = 0; i < excludedRegions.Length; i++)
				if (Contains(excludedRegions[i], screenPosition, eventCamera))
					return true;
		return false;
	}

	static bool Contains(RectTransform region, Vector2 screenPosition, Camera eventCamera) {
		return region != null && region.gameObject.activeInHierarchy &&
			RectTransformUtility.RectangleContainsScreenPoint(region, screenPosition, eventCamera);
	}

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
		if (owned || !GameFlow.IsGameplayActive || eventData.button != PointerEventData.InputButton.Left ||
			PointerStartsOnExcludedControl(eventData))
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

	public void OnEndDrag(PointerEventData eventData) {
		if (owned && eventData.pointerId == activePointer)
			ResetInput();
	}

	public void OnCancel(BaseEventData eventData) {
		PointerEventData pointer = eventData as PointerEventData;
		if (pointer == null || !owned || pointer.pointerId == activePointer)
			ResetInput();
	}

	bool PointerStartsOnExcludedControl(PointerEventData eventData) {
		if (IsScreenPositionExcluded(eventData.position, eventData.pressEventCamera))
			return true;

		// Normally uGUI sends the event to the top-most raycast target, so a joystick or button will
		// never reach this component. Keep the check here for overlapping transparent graphics and for
		// synthetic/native events that provide the original raycast target explicitly.
		GameObject source = eventData.pointerPressRaycast.gameObject;
		if (source == null)
			source = eventData.pointerCurrentRaycast.gameObject;
		if (source == null)
			source = eventData.pointerEnter;
		if (source == null || source == gameObject)
			return false;
		return source.GetComponentInParent<Joystick>() != null || source.GetComponentInParent<Selectable>() != null;
	}

	/// <summary>Returns the drag since the last call and clears it.</summary>
	public Vector2 ConsumeDelta() {
		Vector2 result = delta;
		delta = Vector2.zero;
		return result;
	}

	public void ResetInput() {
		owned = false;
		activePointer = 0;
		delta = Vector2.zero;
		lastPosition = Vector2.zero;
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
