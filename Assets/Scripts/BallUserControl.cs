using System;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;

/// <summary>
/// Setup user control for the ball player.
/// </summary>
public class BallUserControl : MonoBehaviour {
	// Virtual jostick (mobile).
	public Joystick joystick;

	// Reference to the ball controller.
	private Ball ball;

	// the world-relative desired move direction, calculated from the camForward and user input.
	private Vector3 move;

	// A reference to the main camera in the scenes transform.
	private Transform cam;

	// The current forward direction of the camera.
	private Vector3 camForward;

	// Whether the jump button is currently pressed.
	private bool jump;

	/// <summary>
	/// Called when the script instance is being loaded.
	/// See https://docs.unity3d.com/ScriptReference/MonoBehaviour.Awake.html
	/// </summary>
	private void Awake() {
		// Set up the reference.
		ball = GetComponent<Ball>();


		// Get the transform of the main camera.
		if (Camera.main != null)
		{
			cam = Camera.main.transform;
		}
		else
		{
			// Use world-relative controls in this case, which may not be what the user wants, but so warned them.
			Debug.LogWarning(
				"Warning: no main camera found. Ball needs a Camera tagged \"MainCamera\", for camera-relative controls.");
		}
	}

	/// <summary>
	/// Update is called once per frame.
	/// </summary>
	private void Update() {
		if (!GameFlow.IsGameplayActive) {
			move = Vector3.zero;
			jump = false;
			return;
		}

		// Get the axis and jump input.
		float h = CrossPlatformInputManager.GetAxis("Horizontal");
		float v = CrossPlatformInputManager.GetAxis("Vertical");
		jump = CrossPlatformInputManager.GetButton("Jump");

		Vector2 picked = PickInput(new Vector2(h, v), joystick != null ? new Vector2(joystick.Horizontal, joystick.Vertical) : Vector2.zero);
		h = picked.x;
		v = picked.y;

		// Calculate move direction, keeping the analog magnitude of the stick.
		if (cam != null)
			move = CameraRelative(h, v, cam.forward, cam.right);
		else
			move = CameraRelative(h, v, Vector3.forward, Vector3.right);
	}

	/// <summary>One input source at a time: the keys when any key is held, otherwise the on-screen stick.</summary>
	public static Vector2 PickInput(Vector2 keys, Vector2 stick) {
		return keys.sqrMagnitude > 0f ? keys : stick;
	}

	/// <summary>
	/// Stick (h, v) to a horizontal world direction relative to the camera, magnitude kept and clamped to 1.
	/// </summary>
	public static Vector3 CameraRelative(float h, float v, Vector3 camForward, Vector3 camRight) {
		Vector3 forward = new Vector3(camForward.x, 0f, camForward.z).normalized;
		Vector3 right = new Vector3(camRight.x, 0f, camRight.z).normalized;
		return Vector3.ClampMagnitude(forward * v + right * h, 1f);
	}

	/// <summary>
	/// Called every fixed framerate frame, if the MonoBehaviour is enabled.
	/// See https://docs.unity3d.com/ScriptReference/MonoBehaviour.FixedUpdate.html
	/// </summary>
	private void FixedUpdate() {
		// Call the Move function of the ball controller.
		ball.Move(move, jump);
		jump = false;
	}
}