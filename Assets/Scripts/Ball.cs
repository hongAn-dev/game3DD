using UnityEngine;
using System;

/// <summary>
/// The Ball player class.
/// </summary>
public class Ball : MonoBehaviour {
	// The force added to the ball to move it.
	[SerializeField] private float m_MovePower = 5;

	// Whether or not to use torque to move the ball.
	[SerializeField] private bool m_UseTorque = true;

	// The maximum velocity the ball can rotate at.
	[SerializeField] private float m_MaxAngularVelocity = 25;

	// The force added to the ball when it jumps.
	[SerializeField] private float m_JumpPower = 2;

	// Horizontal speed cap (m/s); vertical speed is never clamped so falling and jumping stay physical.
	[SerializeField] private float m_MaxSpeed = 9f;

	// Rate (m/s²) at which the horizontal velocity moves towards stick × MaxSpeed when not using torque.
	[SerializeField] private float m_AccelerationRate = 14f;

	// Deceleration (m/s²) applied when the stick is released, so the ball stops instead of sliding.
	[SerializeField] private float m_Brake = 8f;

	// Base top speed (enemy speeds are tuned against it; Overdrive does not change it).
	public float MaxSpeed { get { return m_MaxSpeed; } }

	// Temporary multipliers (Overdrive). They scale the base values each frame and are never written back into them,
	// so they cannot compound.
	[System.NonSerialized] public float SpeedMultiplier = 1f;
	[System.NonSerialized] public float AccelMultiplier = 1f;

	public float CurrentMaxSpeed { get { return m_MaxSpeed * SpeedMultiplier; } }

	// The length of the ray to check if the ball is grounded.
	private const float k_GroundRayLength = 1f;
	private Rigidbody m_Rigidbody;

	/// <summary>
	/// Use this for initialization.
	/// </summary>
	private void Start() {
		m_Rigidbody = GetComponent<Rigidbody>();
		// Set the maximum angular velocity.
		GetComponent<Rigidbody>().maxAngularVelocity = m_MaxAngularVelocity;
	}

	/// <summary>
	/// Moves the ball. moveDirection keeps the analog magnitude (0..1) of the stick.
	/// </summary>
	public void Move(Vector3 moveDirection, bool jump) {
		float input = Mathf.Clamp01(moveDirection.magnitude);
		float maxSpeed = CurrentMaxSpeed;
		Vector3 velocity = m_Rigidbody.velocity;
		Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
		bool grounded = Physics.Raycast(transform.position, -Vector3.up, k_GroundRayLength);

		if (m_UseTorque && input >= 0.1f) {
			// Add torque around the axis defined by the move direction.
			m_Rigidbody.AddTorque(new Vector3(moveDirection.z, 0, -moveDirection.x)*m_MovePower);
		} else if (input >= 0.1f || grounded) {
			// The stick sets the target speed (light push = slow roll); released on the ground it brakes to rest.
			// In the air with the stick released the arc is left alone.
			Vector3 desired = input >= 0.1f ? Vector3.ClampMagnitude(new Vector3(moveDirection.x, 0f, moveDirection.z), 1f) * maxSpeed : Vector3.zero;
			float rate = (input >= 0.1f ? m_AccelerationRate * AccelMultiplier : m_Brake) * Time.fixedDeltaTime;
			m_Rigidbody.AddForce(Vector3.MoveTowards(horizontal, desired, rate) - horizontal, ForceMode.VelocityChange);
		}

		// Clamp only the horizontal part of the velocity.
		velocity = m_Rigidbody.velocity;
		horizontal = new Vector3(velocity.x, 0f, velocity.z);
		if (horizontal.magnitude > maxSpeed) {
			horizontal = horizontal.normalized * maxSpeed;
			m_Rigidbody.velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
		}

		// If on the ground and jump is pressed
		if (Physics.Raycast(transform.position, -Vector3.up, k_GroundRayLength) && jump)
		{
			// Add force in upwards.
			m_Rigidbody.AddForce(Vector3.up*m_JumpPower, ForceMode.Impulse);
		}
	}
}
