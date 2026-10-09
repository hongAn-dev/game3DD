using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Adds the EnergySpawnDirector to each level (on the GameManager object) and authors its landing points under
/// "Energy Landing Points" (spec §5.2): points on a 3 m grid over ground the robot can roll to from the start
/// (WalkableGrid) that are flat, away from drops and hazards, free of obstacles and have a complete path on the
/// level's baked NavMesh (run EnemySetup first), at least 12/20/28/36 per level, spread out and seeded about 7 m (by
/// path) from the start. Authored cores are removed: the director creates every core. Re-run after map changes.
/// </summary>
public static class EnergySetup {

	const float CoreLift = 0.4f;   // core centre above the ground (crystal is 0.6 tall)

	[MenuItem("Tools/Robo Lac Loi/Apply Energy Setup")]
	public static void Apply() {
		GameObject core = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
		Material marker = AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/RoboLacLoi/Materials/CoreGlow.mat");
		foreach (LevelConfig level in LevelCatalog.All) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			GameManager manager = Object.FindObjectOfType<GameManager>();
			EnergySpawnDirector director = manager.GetComponent<EnergySpawnDirector>();
			if (director == null)
				director = manager.gameObject.AddComponent<EnergySpawnDirector>();
			director.corePrefab = core;
			director.markerMaterial = marker;
			// Scenes keep serialized values, so every timing/distance is written here (spec §5.2), not left to defaults.
			director.markerDuration = 0.35f;
			director.dropDuration = 0.5f;
			director.dropHeight = 4f;
			director.staleDelay = 12f;
			director.minPlayerDistance = 3f;
			director.minSpacing = 2f;
			director.firstCorePath = new Vector2(4f, 10f);
			director.nearPath = new Vector2(6f, 18f);
			director.intervalOverride = -1f;
			NavMeshLoader loader = manager.GetComponent<NavMeshLoader>();
			if (loader == null || loader.data == null)
				throw new System.Exception("EnergySetup: " + level.levelId + " has no baked NavMesh; run Apply Enemy Setup first");
			NavMeshDataInstance nav = NavMesh.AddNavMeshData(loader.data);
			try {
				director.landingPoints = LandingPoints(PointTarget(level.order)).ToArray();
			} finally {
				nav.Remove();
			}
			if (director.landingPoints.Length < PointTarget(level.order))
				Debug.LogWarning("EnergySetup: " + level.levelId + " has only " + director.landingPoints.Length + " safe landing points (target " + PointTarget(level.order) + ")");
			RemoveAuthoredCores();
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log("EnergySetup: " + level.levelId + " landing points " + director.landingPoints.Length);
		}
		Debug.Log("EnergySetup: done");
	}

	/// <summary>Spec §5.2 landing point targets: L1 12, L2 20, L3 28, L4 36.</summary>
	public static int PointTarget(int order) {
		return new[] { 12, 20, 28, 36 }[Mathf.Clamp(order, 1, 4) - 1];
	}

	// NavMesh path length from the start to point, or -1 when there is no complete path.
	static float PathFromStart(Vector3 start, Vector3 point) {
		NavMeshHit from, to;
		if (!NavMesh.SamplePosition(start, out from, 3f, NavMesh.AllAreas) || !NavMesh.SamplePosition(point, out to, 1.5f, NavMesh.AllAreas))
			return -1f;
		var path = new NavMeshPath();
		if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
			return -1f;
		return EnergySpawnDirector.PathLength(path);
	}

	static Vector3 StartGround() {
		Vector3 start = WalkableGrid.PlayerStart();
		RaycastHit hit;
		return WalkableGrid.Ground(start.x, start.z, out hit) ? hit.point : start;
	}

	static bool Ground(Vector3 from, out RaycastHit hit) {
		return WalkableGrid.Ground(from.x, from.z, out hit);
	}

	// Flat, at least 2.5 m from any drop on every side, and nothing solid within 0.7 m above the ground.
	static bool Valid(Vector3 point, RaycastHit hit) {
		foreach (Vector3 side in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right }) {
			RaycastHit near;
			if (!Ground(point + side * 2.5f + Vector3.up * 50f, out near) || Mathf.Abs(near.point.y - hit.point.y) > 1.5f)
				return false;
		}
		foreach (Collider c in Physics.OverlapSphere(hit.point + Vector3.up * 0.9f, 0.7f, ~0, QueryTriggerInteraction.Ignore))
			if (c != hit.collider && c.GetComponentInParent<Rigidbody>() == null)
				return false;
		return true;
	}

	static List<Transform> LandingPoints(int count) {
		GameObject old = GameObject.Find("Energy Landing Points");
		if (old != null)
			Object.DestroyImmediate(old);
		Transform root = new GameObject("Energy Landing Points").transform;

		// Only ground the robot can roll to from the start and enemies/paths can reach, sampled every 3 m.
		Vector3 start = StartGround();
		var candidates = new List<Vector3>();
		var pathFromStart = new Dictionary<Vector3, float>();
		foreach (Vector2Int cell in WalkableGrid.Reachable(WalkableGrid.PlayerStart()).Keys) {
			if (cell.x % 3 != 0 || cell.y % 3 != 0)
				continue;
			RaycastHit hit;
			Vector3 top = new Vector3(cell.x, 0f, cell.y);
			if (!Ground(top, out hit) || !Valid(top, hit))
				continue;
			Vector3 point = hit.point + Vector3.up * CoreLift;
			float length = PathFromStart(start, hit.point);
			if (length < 0f || pathFromStart.ContainsKey(point))
				continue;
			candidates.Add(point);
			pathFromStart[point] = length;
		}

		// Seed with the candidate about 7 m by path from the start (the first core goes 4-10 m away), then add the
		// candidates farthest from everything chosen so far.
		var chosen = new List<Vector3>();
		var rest = candidates.ToList();
		if (rest.Count > 0) {
			Vector3 seed = rest.OrderBy(c => Mathf.Abs(pathFromStart[c] - 7f)).First();
			chosen.Add(seed);
			rest.Remove(seed);
		}
		while (chosen.Count < count && rest.Count > 0) {
			int best = 0;
			float bestDistance = -1f;
			for (int i = 0; i < rest.Count; i++) {
				float nearest = chosen.Count == 0 ? float.MaxValue : chosen.Min(c => Vector3.Distance(c, rest[i]));
				if (nearest > bestDistance) {
					bestDistance = nearest;
					best = i;
				}
			}
			chosen.Add(rest[best]);
			rest.RemoveAt(best);
		}

		var points = new List<Transform>();
		for (int i = 0; i < chosen.Count; i++) {
			Transform point = new GameObject("Landing Point " + i).transform;
			point.SetParent(root, false);
			point.position = chosen[i];
			points.Add(point);
		}
		return points;
	}

	// Cores inside prefab instances (Sil/Vin Coins groups) need the instance unpacked before they can be removed;
	// groups left empty go too.
	static void RemoveAuthoredCores() {
		foreach (Treasure t in Object.FindObjectsOfType<Treasure>()) {
			GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
			if (root != null && root != t.gameObject)
				PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
			Transform parent = t.transform.parent;
			Object.DestroyImmediate(t.gameObject);
			while (parent != null && parent.childCount == 0 && parent.GetComponents<Component>().Length == 1) {
				Transform up = parent.parent;
				Object.DestroyImmediate(parent.gameObject);
				parent = up;
			}
		}
	}
}
