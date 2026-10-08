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

	// Force applied per unit of stick input when not using torque.
	[SerializeField] private float m_Acceleration = 25f;

	// Deceleration (m/s²) applied when the stick is released, so the ball stops instead of sliding.
	[SerializeField] private float m_Brake = 8f;

	public float MaxSpeed { get { return m_MaxSpeed; } }

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
	/// Update is called once per frame.
	/// </summary>
	/// <summary>
	/// Moves the ball. moveDirection keeps the analog magnitude (0..1) of the stick.
	/// </summary>
	public void Move(Vector3 moveDirection, bool jump) {
		float input = Mathf.Clamp01(moveDirection.magnitude);
		Vector3 velocity = m_Rigidbody.velocity;
		Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

		if (input < 0.1f) {
			// Brake towards rest without reversing direction.
			float step = Mathf.Min(m_Brake * Time.fixedDeltaTime, horizontal.magnitude);
			if (step > 0f)
				m_Rigidbody.AddForce(-horizontal.normalized * step, ForceMode.VelocityChange);
		} else if (m_UseTorque) {
			// Add torque around the axis defined by the move direction.
			m_Rigidbody.AddTorque(new Vector3(moveDirection.z, 0, -moveDirection.x)*m_MovePower);
		} else {
			m_Rigidbody.AddForce(moveDirection.normalized * m_Acceleration * input);
		}

		// Clamp only the horizontal part of the velocity.
		velocity = m_Rigidbody.velocity;
		horizontal = new Vector3(velocity.x, 0f, velocity.z);
		if (horizontal.magnitude > m_MaxSpeed) {
			horizontal = horizontal.normalized * m_MaxSpeed;
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
