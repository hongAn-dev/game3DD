using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns each level's old WaterDeathZone into "Hazard Sea" (spec §5): a HazardVisual child (sea mesh with the acid
/// or lava material, no collider) and a DeathZone child (6 m thick trigger whose top is the sea surface), plus a
/// "Kill Plane" trigger 15 m under the lowest collider. Both carry the level hazard cause. Re-running updates them.
/// </summary>
public static class HazardSetup {

	const string Root = "Assets/ThirdParty/RoboLacLoi/";
	const float Thickness = 6f;

	[MenuItem("Tools/Robo Lac Loi/Apply Hazard Setup")]
	public static void Apply() {
		Texture2D noise = Noise();
		foreach (LevelConfig level in LevelCatalog.All) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			bool lava = level.hazardDeathMessage.Contains("dung nham");
			Material material = lava
				? HazardMaterial("Lava", new Color(0.55f, 0.08f, 0.02f), new Color(1f, 0.55f, 0.1f), 1.2f, noise)
				: HazardMaterial("Acid", new Color(0.36f, 0.52f, 0.06f), new Color(0.8f, 0.95f, 0.25f), 0.5f, noise);
			Transform sea = Sea();
			sea.Find("HazardVisual").GetComponent<Renderer>().sharedMaterial = material;
			foreach (HazardVolume hazard in sea.GetComponentsInChildren<HazardVolume>())
				hazard.cause = level.hazardDeathMessage;
			PlaceKillPlane(sea);
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log("HazardSetup: " + level.levelId + (lava ? " lava" : " acid"));
		}
		Debug.Log("HazardSetup: done");
	}

	static Transform Sea() {
		GameObject existing = GameObject.Find("Hazard Sea");
		if (existing != null)
			return existing.transform;

		GameObject water = GameObject.Find("WaterDeathZone");
		Mesh mesh = water.GetComponent<MeshFilter>().sharedMesh;
		Vector3 scale = water.transform.localScale;
		Transform sea = new GameObject("Hazard Sea").transform;
		sea.SetParent(water.transform.parent, false);
		sea.position = water.transform.position;

		GameObject visual = new GameObject("HazardVisual");
		visual.transform.SetParent(sea, false);
		visual.transform.localScale = new Vector3(scale.x, 1f, scale.z);
		visual.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		Bounds b = mesh.bounds;
		BoxCollider zone = NewTrigger(sea, "DeathZone");
		zone.center = new Vector3(b.center.x * scale.x, -Thickness / 2f, b.center.z * scale.z);
		zone.size = new Vector3(b.size.x * scale.x, Thickness, b.size.z * scale.z);
		NewTrigger(sea, "Kill Plane");

		Object.DestroyImmediate(water);
		return sea;
	}

	static BoxCollider NewTrigger(Transform parent, string name) {
		GameObject go = new GameObject(name);
		go.transform.SetParent(parent, false);
		BoxCollider box = go.AddComponent<BoxCollider>();
		box.isTrigger = true;
		go.AddComponent<HazardVolume>();
		return box;
	}

	// 15 m under the lowest static collider, 10 m thick, wide enough for any map.
	static void PlaceKillPlane(Transform sea) {
		float lowest = Object.FindObjectsOfType<Collider>().Where(c => !c.isTrigger && c.attachedRigidbody == null)
			.Min(c => c.bounds.min.y);
		Transform plane = sea.Find("Kill Plane");
		plane.position = new Vector3(sea.position.x, lowest - 20f, sea.position.z);
		BoxCollider box = plane.GetComponent<BoxCollider>();
		box.center = Vector3.zero;
		box.size = new Vector3(4000f, 10f, 4000f);
	}

	static Material HazardMaterial(string name, Color deep, Color foam, float glow, Texture2D noise) {
		string path = Root + "Materials/" + name + ".mat";
		Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
		if (material == null) {
			material = new Material(Shader.Find("RoboLacLoi/HazardFlow"));
			AssetDatabase.CreateAsset(material, path);
		}
		material.shader = Shader.Find("RoboLacLoi/HazardFlow");
		material.SetColor("_Color", deep);
		material.SetColor("_FoamColor", foam);
		material.SetFloat("_Glow", glow);
		material.SetTexture("_NoiseTex", noise);
		EditorUtility.SetDirty(material);
		return material;
	}

	// Tileable value noise (period 8 cells over 128 px), written once.
	static Texture2D Noise() {
		string path = Root + "Textures/HazardNoise.png";
		if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) {
			Directory.CreateDirectory(Root + "Textures");
			const int size = 128, cells = 8;
			var random = new System.Random(7);
			float[,] grid = new float[cells, cells];
			for (int x = 0; x < cells; x++)
				for (int y = 0; y < cells; y++)
					grid[x, y] = (float)random.NextDouble();
			var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
			for (int x = 0; x < size; x++) {
				for (int y = 0; y < size; y++) {
					float fx = x * cells / (float)size, fy = y * cells / (float)size;
					int x0 = (int)fx, y0 = (int)fy;
					float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
					float a = Mathf.Lerp(grid[x0, y0], grid[(x0 + 1) % cells, y0], tx);
					float c = Mathf.Lerp(grid[x0, (y0 + 1) % cells], grid[(x0 + 1) % cells, (y0 + 1) % cells], tx);
					float v = Mathf.Lerp(a, c, ty);
					tex.SetPixel(x, y, new Color(v, v, v));
				}
			}
			File.WriteAllBytes(path, tex.EncodeToPNG());
			AssetDatabase.ImportAsset(path);
			TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.wrapMode = TextureWrapMode.Repeat;
			importer.sRGBTexture = false;
			importer.SaveAndReimport();
		}
		return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
	}
}
