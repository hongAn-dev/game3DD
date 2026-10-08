using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground the robot can stand on, on a 1 m grid, and the part of it it can roll to from the start (spec §5): a cell is
/// walkable when the top surface is a static collider with a gentle normal and lies outside every HazardVolume;
/// neighbouring cells connect when their heights differ by at most MaxClimb (the robot cannot jump).
/// </summary>
public static class WalkableGrid {

	public const float MaxClimb = 0.8f;
	const float Reach = 300f;   // flood fill stops this far from the start

	static readonly Vector2Int[] Neighbours = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

	public static bool InHazard(Vector3 point) {
		foreach (HazardVolume hazard in Object.FindObjectsOfType<HazardVolume>())
			if (hazard.GetComponent<Collider>().bounds.Contains(point))
				return true;
		return false;
	}

	// Highest static surface under (x, z); the robot and other rigidbodies are ignored.
	public static bool Ground(float x, float z, out RaycastHit hit) {
		hit = new RaycastHit();
		bool found = false;
		foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore))
			if (h.collider.attachedRigidbody == null && (!found || h.point.y > hit.point.y)) {
				hit = h;
				found = true;
			}
		return found && hit.normal.y > 0.7f && !InHazard(hit.point + Vector3.up * 0.2f);
	}

	/// <summary>Ground height of every walkable cell connected to start, keyed by cell (x, z in metres).</summary>
	public static Dictionary<Vector2Int, float> Reachable(Vector3 start) {
		var heights = new Dictionary<Vector2Int, float?>();
		var reached = new Dictionary<Vector2Int, float>();
		Vector2Int first = new Vector2Int(Mathf.RoundToInt(start.x), Mathf.RoundToInt(start.z));
		float? h = Height(first, heights);
		if (h == null)
			return reached;
		reached[first] = h.Value;
		var open = new Queue<Vector2Int>();
		open.Enqueue(first);
		while (open.Count > 0) {
			Vector2Int cell = open.Dequeue();
			foreach (Vector2Int step in Neighbours) {
				Vector2Int next = cell + step;
				if (reached.ContainsKey(next) || (next - first).magnitude > Reach)
					continue;
				float? nh = Height(next, heights);
				if (nh != null && Mathf.Abs(nh.Value - reached[cell]) <= MaxClimb) {
					reached[next] = nh.Value;
					open.Enqueue(next);
				}
			}
		}
		return reached;
	}

	static float? Height(Vector2Int cell, Dictionary<Vector2Int, float?> cache) {
		float? h;
		if (!cache.TryGetValue(cell, out h)) {
			RaycastHit hit;
			h = Ground(cell.x, cell.y, out hit) ? hit.point.y : (float?)null;
			cache[cell] = h;
		}
		return h;
	}

	public static Vector3 PlayerStart() {
		return GameObject.FindWithTag("Player").transform.position;
	}
}
