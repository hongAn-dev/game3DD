using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Fixed on-screen joystick. Only the pointer that pressed it steers it (spec §7), the stick keeps its analog
/// magnitude (dead zone remapped, clamped to 1) and it resets on release, disable, focus loss and pause.
/// </summary>
public class FixedJoystick : Joystick {

	[Header("Fixed Joystick")]
	[Range(0f, 0.5f)] public float deadZone = 0.1f;

	// Pointer id that owns the stick, -1 when free.
	private int activePointer = -1;

	/// <summary>Local point inside the background (pixels from its centre) to a stick vector of magnitude 0..1.</summary>
	public static Vector2 ToInput(Vector2 local, float radius, float deadZone) {
		if (radius <= 0f)
			return Vector2.zero;
		Vector2 raw = Vector2.ClampMagnitude(local / radius, 1f);
		float magnitude = raw.magnitude;
		if (magnitude <= deadZone)
			return Vector2.zero;
		return raw / magnitude * ((magnitude - deadZone) / (1f - deadZone));
	}

	public override void OnPointerDown(PointerEventData eventData) {
		if (activePointer != -1)
			return;
		activePointer = eventData.pointerId;
		Steer(eventData);
	}

	public override void OnDrag(PointerEventData eventData) {
		if (eventData.pointerId == activePointer)
			Steer(eventData);
	}

	public override void OnPointerUp(PointerEventData eventData) {
		if (eventData.pointerId == activePointer)
			ResetInput();
	}

	void Steer(PointerEventData eventData) {
		Vector2 local;
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out local))
			return;
		float radius = background.rect.width / 2f;
		inputVector = ToInput(local, radius, deadZone);
		handle.anchoredPosition = Vector2.ClampMagnitude(local, radius) * handleLimit;
	}

	public void ResetInput() {
		activePointer = -1;
		inputVector = Vector2.zero;
		if (handle != null)
			handle.anchoredPosition = Vector2.zero;
	}

	void OnDisable() {
		ResetInput();
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
