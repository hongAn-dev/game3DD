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

	/// <summary>The dashing boss touches the robot (flat distance within reach, same terrain level).</summary>
	public static bool TouchesDash(Vector3 boss, Vector3 robot, float reach) {
		Vector3 d = robot - boss;
		if (Mathf.Abs(d.y) > 2.5f)
			return false;
		d.y = 0f;
		return d.magnitude <= reach;
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
