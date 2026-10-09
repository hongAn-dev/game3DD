using UnityEngine;

public enum AttackKind { Strike, Slam, Dash }

/// <summary>Geometry of the guardians' attacks (spec §4.3), kept pure so it can be tested on its own.</summary>
public static class BossAttacks {

	/// <summary>Centre of the slam circle: reach metres in front of the boss, on the ground under it.</summary>
	public static Vector3 SlamCentre(Vector3 boss, Vector3 forward, float reach) {
		forward.y = 0f;
		return boss + forward.normalized * reach;
	}

	/// <summary>The robot's centre is inside the circle (flat) and on the same terrain level (within 2 m height).</summary>
	public static bool InSlam(Vector3 centre, float radius, Vector3 robot) {
		Vector3 d = robot - centre;
		if (Mathf.Abs(d.y) > 2f)
			return false;
		d.y = 0f;
		return d.magnitude <= radius;
	}

	/// <summary>
	/// The dashing boss, moving from 'from' to 'to' this frame, touches the robot: flat distance from the robot to that
	/// segment within reach (strip half-width + robot radius), same terrain level.
	/// </summary>
	public static bool TouchesDash(Vector3 from, Vector3 to, Vector3 robot, float reach) {
		if (Mathf.Abs(robot.y - to.y) > 2.5f)
			return false;
		Vector2 a = new Vector2(from.x, from.z), b = new Vector2(to.x, to.z), p = new Vector2(robot.x, robot.z);
		Vector2 ab = b - a;
		float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
		return Vector2.Distance(p, a + ab * t) <= reach;
	}

	/// <summary>Line of sight from just above the slam centre to the robot, ignoring moving bodies (robot, enemies).</summary>
	public static bool Clear(Vector3 from, Vector3 to, RaycastHit[] buffer) {
		int count = Physics.RaycastNonAlloc(from, to - from, buffer, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < count; i++)
			if (buffer[i].collider.attachedRigidbody == null)
				return false;
		return true;
	}
}
