using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground the robot can stand on, on a 1 m grid, and the part of it it can roll to from the start (spec §5): a cell is
/// walkable when the top surface is a static collider with a gentle normal and stands above every HazardVolume
/// (a margin over the acid surface); neighbouring cells connect when the surface between them never steps more than
/// MaxStep per quarter metre (the 0.5 m ball cannot roll up a ledge and cannot jump) and the cells differ by at most
/// MaxClimb.
/// </summary>
public static class WalkableGrid {

	public const float MaxClimb = 0.8f;
	public const float MaxStep = 0.3f;   // per 0.25 m along the edge between two cells
	const float Reach = 300f;   // flood fill stops this far from the start

	static readonly Vector2Int[] Neighbours = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

	public static bool InHazard(Vector3 point) {
		foreach (HazardVolume hazard in Object.FindObjectsOfType<HazardVolume>())
			if (hazard.GetComponent<Collider>().bounds.Contains(point))
				return true;
		return false;
	}

	// Highest static surface under (x, z); the robot and other rigidbodies are ignored.
	static bool Top(float x, float z, out RaycastHit hit) {
		hit = new RaycastHit();
		bool found = false;
		foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore))
			if (h.collider.attachedRigidbody == null && (!found || h.point.y > hit.point.y)) {
				hit = h;
				found = true;
			}
		return found;
	}

	/// <summary>Walkable top surface under (x, z): gentle normal and at least 0.1 m above any hazard surface.</summary>
	public static bool Ground(float x, float z, out RaycastHit hit) {
		return Top(x, z, out hit) && hit.normal.y > 0.7f && !InHazard(hit.point - Vector3.up * 0.1f);
	}

	// No ledge on the way from a to b: the top surface changes by at most MaxStep every quarter metre.
	static bool Smooth(Vector2Int a, float ha, Vector2Int b, float hb) {
		float previous = ha;
		for (int i = 1; i <= 4; i++) {
			float h = hb;
			if (i < 4) {
				RaycastHit hit;
				Vector2 p = Vector2.Lerp(a, b, i / 4f);
				if (!Top(p.x, p.y, out hit))
					return false;
				h = hit.point.y;
			}
			if (Mathf.Abs(h - previous) > MaxStep)
				return false;
			previous = h;
		}
		return true;
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
				if (nh != null && Mathf.Abs(nh.Value - reached[cell]) <= MaxClimb && Smooth(cell, reached[cell], next, nh.Value)) {
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
