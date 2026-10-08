using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds the EnergySpawnDirector to each level (on the GameManager object) and authors its landing points under
/// "Energy Landing Points": ground points (authored core spots and a 3 m grid) that are flat, away from drops and free
/// of obstacles, spread out and seeded near the robot start (spec §4.2). Authored cores are then removed: the
/// director creates every core. Re-running rebuilds the points. Run per map change.
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
			director.landingPoints = LandingPoints(level.energyCap * 2 + 4).ToArray();
			RemoveAuthoredCores();
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log("EnergySetup: " + level.levelId + " landing points " + director.landingPoints.Length);
		}
		Debug.Log("EnergySetup: done");
	}

	static bool Ground(Vector3 from, out RaycastHit hit) {
		if (!Physics.Raycast(from, Vector3.down, out hit, 200f, ~0, QueryTriggerInteraction.Ignore))
			return false;
		return hit.collider.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) < 0 && hit.normal.y > 0.85f;
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

		var candidates = new List<Vector3>();
		foreach (Treasure t in Object.FindObjectsOfType<Treasure>()) {
			RaycastHit hit;
			Vector3 top = t.transform.position + Vector3.up * 2f;
			if (Ground(top, out hit) && Valid(top, hit))
				candidates.Add(hit.point + Vector3.up * CoreLift);
		}

		Collider[] solids = Object.FindObjectsOfType<Collider>().Where(c => !c.isTrigger).ToArray();
		Bounds area = solids[0].bounds;
		foreach (Collider c in solids)
			if (c.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) < 0)
				area.Encapsulate(c.bounds);
		for (float x = area.min.x; x <= area.max.x; x += 3f) {
			for (float z = area.min.z; z <= area.max.z; z += 3f) {
				RaycastHit hit;
				Vector3 top = new Vector3(x, area.max.y + 5f, z);
				if (Ground(top, out hit) && Valid(top, hit))
					candidates.Add(hit.point + Vector3.up * CoreLift);
			}
		}

		// Seed with the candidate closest to 12 m from the robot start (so the first core is near), then add the
		// candidates farthest from everything chosen so far.
		var chosen = new List<Vector3>();
		var rest = candidates.Distinct().ToList();
		GameObject player = GameObject.FindWithTag("Player");
		if (player != null && rest.Count > 0) {
			Vector3 start = player.transform.position;
			Vector3 seed = rest.OrderBy(c => Mathf.Abs(Vector3.Distance(c, start) - 12f)).First();
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
