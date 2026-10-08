using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// T0 baseline: enemy sizes, walkable area of Level1/2, top-down screenshots and a prefab override audit.
/// Writes "PROBE ..." log lines. Run with -executeMethod BaselineProbe.Run (no -nographics: it renders).
/// </summary>
public static class BaselineProbe {

	static readonly string OutDir = System.Environment.GetEnvironmentVariable("HOME") + "/Downloads/claude/work/probe";
	static readonly string[] AuditPrefabs = { "Coin", "Coin Bouncy", "Sil Coins", "Vin Coins", "Enemy - Monster", "Enemy - Crater", "WaterDeathZone" };

	public static void Run() {
		Directory.CreateDirectory(OutDir);
		EnemySize("Enemy - Monster");
		EnemySize("Enemy - Crater");
		foreach (string level in new[] { "Level1", "Level2" }) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			WalkableArea(level);
			TopDown(level, OutDir + "/" + level + "_topdown_before.png");
		}
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" }) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			Audit(level);
		}
		Debug.Log("PROBE done");
	}

	/// <summary>T4 result: connected walkable area (WalkableGrid) and top-down images of the expanded levels.</summary>
	public static void After() {
		Directory.CreateDirectory(OutDir);
		foreach (string level in new[] { "Level1", "Level2" }) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			Debug.Log("PROBE walkable after " + level + " = " + WalkableGrid.Reachable(WalkableGrid.PlayerStart()).Count + " m2 (connected to start)");
			TopDown(level, OutDir + "/" + level + "_topdown_after.png");
		}
		Debug.Log("PROBE done");
	}

	static void EnemySize(string prefabName) {
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + prefabName + ".prefab");
		GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
		// Renderer.bounds of a skinned mesh is stale outside play mode; use mesh bounds through each transform.
		Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
		foreach (Renderer r in instance.GetComponentsInChildren<Renderer>()) {
			SkinnedMeshRenderer skinned = r as SkinnedMeshRenderer;
			MeshFilter filter = r.GetComponent<MeshFilter>();
			Mesh mesh = skinned != null ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
			if (mesh == null)
				continue;
			Bounds b = mesh.bounds;
			for (int i = 0; i < 8; i++) {
				Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
				bounds.Encapsulate(r.transform.localToWorldMatrix.MultiplyPoint3x4(corner));
			}
		}
		Debug.Log("PROBE size " + prefabName + " = " + bounds.size.ToString("F2") + " (robot diameter 1.0), root scale "
			+ instance.transform.localScale.ToString("F2"));
		Object.DestroyImmediate(instance);
	}

	// Ground the robot can stand on: downward rays on a 1 m grid hitting a non-trigger collider whose normal is
	// walkable (y > 0.7) and which is not water/death zone. Counts square metres.
	static void WalkableArea(string level) {
		Collider[] terrain = Object.FindObjectsOfType<Collider>()
			.Where(c => !c.isTrigger && c.GetComponentInParent<Renderer>() != null).ToArray();
		Bounds area = terrain[0].bounds;
		foreach (Collider c in terrain)
			area.Encapsulate(c.bounds);
		int walkable = 0;
		for (float x = area.min.x; x <= area.max.x; x += 1f) {
			for (float z = area.min.z; z <= area.max.z; z += 1f) {
				RaycastHit hit;
				if (!Physics.Raycast(new Vector3(x, area.max.y + 5f, z), Vector3.down, out hit, area.size.y + 10f, ~0, QueryTriggerInteraction.Ignore))
					continue;
				if (hit.normal.y > 0.7f && hit.collider.name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) < 0)
					walkable++;
			}
		}
		Debug.Log("PROBE walkable " + level + " = " + walkable + " m2 (grid 1 m, slope < ~45 deg)");
	}

	static void TopDown(string level, string file) {
		Collider[] all = Object.FindObjectsOfType<Collider>().Where(c => !c.isTrigger).ToArray();
		Bounds area = all[0].bounds;
		foreach (Collider c in all)
			area.Encapsulate(c.bounds);
		GameObject go = new GameObject("Probe TopDown");
		Camera cam = go.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = Mathf.Max(area.extents.x, area.extents.z) * 1.05f;
		go.transform.position = new Vector3(area.center.x, area.max.y + 50f, area.center.z);
		go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		cam.farClipPlane = area.size.y + 200f;
		var rt = new RenderTexture(1024, 1024, 24);
		cam.targetTexture = rt;
		cam.Render();
		RenderTexture.active = rt;
		var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
		tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
		tex.Apply();
		File.WriteAllBytes(file, tex.EncodeToPNG());
		RenderTexture.active = null;
		Object.DestroyImmediate(go);
		Debug.Log("PROBE topdown " + level + " -> " + file);
	}

	static void Audit(string level) {
		var counts = new Dictionary<string, int>();
		var overrides = new Dictionary<string, int>();
		foreach (GameObject root in Object.FindObjectsOfType<GameObject>()) {
			if (!PrefabUtility.IsOutermostPrefabInstanceRoot(root))
				continue;
			GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root);
			if (source == null || !AuditPrefabs.Contains(source.name))
				continue;
			counts[source.name] = (counts.ContainsKey(source.name) ? counts[source.name] : 0) + 1;
			int mods = PrefabUtility.GetPropertyModifications(root).Count(m => !m.propertyPath.StartsWith("m_Local") && m.propertyPath != "m_Name" && m.propertyPath != "m_RootOrder");
			overrides[source.name] = (overrides.ContainsKey(source.name) ? overrides[source.name] : 0) + mods;
		}
		foreach (string name in AuditPrefabs)
			Debug.Log("PROBE audit " + level + " " + name + ": instances=" + (counts.ContainsKey(name) ? counts[name] : 0)
				+ " non-transform overrides=" + (overrides.ContainsKey(name) ? overrides[name] : 0));
	}
}
