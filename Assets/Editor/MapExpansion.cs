using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds new ground to Level1 (east strip: safe practice route, low rocks, lander debris) and Level2 (west annex behind
/// an acid channel crossed by three land bridges, machinery to circle around), spec §5. Each patch is a generated
/// flat-shaded low-poly mesh (zone ground colour, rock-coloured shore sloping under the acid) with a MeshCollider,
/// tucked under the old terrain where they overlap. Re-running rebuilds the patch and its props.
/// Order after a map change: Dress All Zones → Apply Hazard Setup → Apply Map Expansion → Apply Hazard Setup (kill plane)
/// → Apply Energy Setup → Apply Enemy Setup.
/// </summary>
public static class MapExpansion {

	class Prop {
		public string model;   // "Pack/model"
		public float x, z, size, yaw, tilt;
		public Prop(string model, float x, float z, float size, float yaw = 0f, float tilt = 0f) {
			this.model = model; this.x = x; this.z = z; this.size = size; this.yaw = yaw; this.tilt = tilt;
		}
	}

	class Patch {
		public string scene;
		public Rect area;                 // x/z extent, overlapping the old island on the attached side
		public bool attachedWest;         // which rect side touches the old island
		public Rect[] channels = new Rect[0];   // acid cut through the patch
		public Prop[] props = new Prop[0];
		public int seed;
	}

	const float Cell = 2f;
	const float Shore = 2.5f;      // width of the slope from land down under the acid
	const float Depth = 1.5f;      // shore bottom under the sea surface
	const float Blend = 5f;        // seam blend width on the attached side
	const string MeshFolder = "Assets/ThirdParty/RoboLacLoi/Models/Expansion";

	static readonly Patch[] Patches = {
		new Patch {
			scene = "Level1", area = Rect.MinMaxRect(11f, -20f, 32f, 20f), attachedWest = true, seed = 11,
			props = new[] {
				new Prop("KenneyNatureKit/stone_largeA", 22f, -12f, 1.6f, 30f),
				new Prop("KenneyNatureKit/stone_largeC", 25f, 10f, 1.4f, 120f),
				new Prop("KenneyNatureKit/stone_largeE", 27f, -7f, 1.2f, 200f),
				new Prop("KenneyNatureKit/stone_largeB", 19f, 15f, 1.5f, 75f),
				new Prop("KenneySpaceStationKit/structure", 24f, -1f, 3.5f, 25f, 18f),
				new Prop("KenneySpaceStationKit/structure-panel", 21.5f, 2.5f, 2.2f, 70f, 35f),
				new Prop("KenneySpaceStationKit/pipe-ring", 26.5f, 3f, 1.6f, 10f, 60f),
				new Prop("KenneySpaceStationKit/container-flat-open", 26f, -13f, 2.2f, 140f, 12f),
				new Prop("KenneySpaceStationKit/rocks", 21f, -5f, 1.4f, 0f),
			},
		},
		new Patch {
			scene = "Level2", area = Rect.MinMaxRect(-55f, -27f, -23f, 27f), attachedWest = false, seed = 22,
			// Channel x -31..-27 with bridges centred on z -15, 0, 15 (10 m gaps, ~5 m dry after the shore slopes).
			channels = new[] {
				Rect.MinMaxRect(-31f, -30f, -27f, -20f), Rect.MinMaxRect(-31f, -10f, -27f, -5f),
				Rect.MinMaxRect(-31f, 5f, -27f, 10f), Rect.MinMaxRect(-31f, 20f, -27f, 30f),
			},
			props = new[] {
				new Prop("KenneyCityKitIndustrial/detail-tank-large", -42f, 0f, 5f),
				new Prop("KenneyCityKitIndustrial/chimney-small", -47f, -12f, 3f),
				new Prop("KenneyCityKitIndustrial/shipping-container-a", -40f, -14f, 5f, 90f),
				new Prop("KenneyCityKitIndustrial/shipping-container-b", -38f, 13f, 5f, 0f),
				new Prop("KenneyCityKitIndustrial/detail-tank", -48f, 10f, 3f),
				new Prop("KenneyCityKitIndustrial/shipping-container-c", -46f, 20f, 4.5f, 60f),
				new Prop("KenneySurvivalKit/barrel", -35f, -6f, 1f),
				new Prop("KenneySurvivalKit/box-large", -36f, 6f, 1.4f, 20f),
			},
		},
	};

	[MenuItem("Tools/Robo Lac Loi/Apply Map Expansion")]
	public static void Apply() {
		if (!AssetDatabase.IsValidFolder(MeshFolder))
			AssetDatabase.CreateFolder("Assets/ThirdParty/RoboLacLoi/Models", "Expansion");
		foreach (Patch patch in Patches) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + patch.scene + ".unity", OpenSceneMode.Single);
			Build(patch);
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
		}
		Debug.Log("MapExpansion: done");
	}

	static void Build(Patch patch) {
		Transform environment = GameObject.Find("Environment").transform;
		Transform old = environment.Find("Expansion");
		if (old != null)
			Object.DestroyImmediate(old.gameObject);
		Physics.SyncTransforms();

		HazardVolume zone = Object.FindObjectsOfType<HazardVolume>().FirstOrDefault(h => h.name == "DeathZone");
		if (zone == null)
			throw new System.Exception("MapExpansion: " + patch.scene + " has no Hazard Sea; run Tools > Robo Lac Loi > Apply Hazard Setup first");
		float sea = zone.GetComponent<Collider>().bounds.max.y;
		float baseHeight = BaseHeight(patch, sea);

		Transform root = new GameObject("Expansion").transform;
		root.SetParent(environment, false);
		GameObject ground = new GameObject(patch.scene + " Expansion Ground");
		ground.transform.SetParent(root, false);
		ground.isStatic = true;
		Mesh mesh = BuildMesh(patch, sea, baseHeight);
		string path = MeshFolder + "/" + patch.scene + "_Expansion.asset";
		AssetDatabase.DeleteAsset(path);
		AssetDatabase.CreateAsset(mesh, path);
		ground.AddComponent<MeshFilter>().sharedMesh = mesh;
		string zones = "Assets/ThirdParty/RoboLacLoi/Materials/Zones/" + patch.scene;
		Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(zones + "_Ground.mat");
		Material rockMaterial = AssetDatabase.LoadAssetAtPath<Material>(zones + "_Rock.mat");
		if (groundMaterial == null || rockMaterial == null)
			throw new System.Exception("MapExpansion: zone materials missing for " + patch.scene + "; run Tools > Robo Lac Loi > Dress All Zones first");
		ground.AddComponent<MeshRenderer>().sharedMaterials = new[] { groundMaterial, rockMaterial };
		ground.AddComponent<MeshCollider>().sharedMesh = mesh;
		Physics.SyncTransforms();

		foreach (Prop prop in patch.props)
			PlaceProp(root, prop);
		Debug.Log("MapExpansion: " + patch.scene + " base height " + baseHeight.ToString("F2") + ", sea " + sea.ToString("F2"));
	}

	// Median height of the old terrain inside the patch rect, above the sea: the new land continues it.
	static float BaseHeight(Patch patch, float sea) {
		var heights = new List<float>();
		for (float x = patch.area.xMin; x <= patch.area.xMax; x += 1f)
			for (float z = patch.area.yMin; z <= patch.area.yMax; z += 1f) {
				float h;
				if (OldTerrain(x, z, out h) && h > sea + 0.2f)
					heights.Add(h);
			}
		heights.Sort();
		// At least 0.9 m above the acid so the terrain noise never dips under the surface.
		return Mathf.Max(heights.Count > 0 ? heights[heights.Count / 2] : sea + 1.2f, sea + 0.9f);
	}

	static bool OldTerrain(float x, float z, out float height) {
		height = 0f;
		bool found = false;
		foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 500f, z), Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore))
			if (hit.collider.name.EndsWith(" Terrain") && (!found || hit.point.y > height)) {
				height = hit.point.y;
				found = true;
			}
		return found;
	}

	static float DistanceToRect(Rect r, float x, float z) {
		float dx = Mathf.Max(r.xMin - x, 0f, x - r.xMax);
		float dz = Mathf.Max(r.yMin - z, 0f, z - r.yMax);
		return Mathf.Sqrt(dx * dx + dz * dz);
	}

	// Distance to where the land ends: the open sides of the rect and the channels.
	static float ToSea(Patch patch, float x, float z) {
		Rect a = patch.area;
		float d = Mathf.Min(z - a.yMin, a.yMax - z);
		d = Mathf.Min(d, patch.attachedWest ? a.xMax - x : x - a.xMin);
		foreach (Rect channel in patch.channels)
			d = Mathf.Min(d, DistanceToRect(channel, x, z));
		return d;
	}

	static float Height(Patch patch, float sea, float baseHeight, float x, float z) {
		float land = baseHeight + (Mathf.PerlinNoise(x * 0.07f + patch.seed, z * 0.07f) - 0.5f) * 1.0f
			+ (Mathf.PerlinNoise(x * 0.23f, z * 0.23f + patch.seed) - 0.5f) * 0.3f;
		// Within Blend metres of the attached side, ease from the old terrain height at that side into the new land,
		// so the robot rolls across the seam without a ledge.
		float edge, attached = patch.attachedWest ? x - patch.area.xMin : patch.area.xMax - x;
		if (attached < Blend && OldTerrain(patch.attachedWest ? patch.area.xMin : patch.area.xMax, z, out edge) && edge > sea)
			land = Mathf.Lerp(edge, land, Mathf.SmoothStep(0f, 1f, attached / Blend));
		float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ToSea(patch, x, z) / Shore));
		float h = Mathf.Lerp(sea - Depth, land, s);
		// Tuck under the old terrain where it is higher, so the old surface stays on top at the seam.
		float old;
		if (OldTerrain(x, z, out old) && old > h - 0.05f)
			h = Mathf.Min(h, old - 0.1f);
		return h;
	}

	static Mesh BuildMesh(Patch patch, float sea, float baseHeight) {
		int nx = Mathf.CeilToInt(patch.area.width / Cell), nz = Mathf.CeilToInt(patch.area.height / Cell);
		var random = new System.Random(patch.seed);
		var grid = new Vector3[nx + 1, nz + 1];
		for (int i = 0; i <= nx; i++) {
			for (int j = 0; j <= nz; j++) {
				bool edge = i == 0 || j == 0 || i == nx || j == nz;
				float x = patch.area.xMin + i * Cell + (edge ? 0f : (float)(random.NextDouble() - 0.5) * Cell * 0.5f);
				float z = patch.area.yMin + j * Cell + (edge ? 0f : (float)(random.NextDouble() - 0.5) * Cell * 0.5f);
				grid[i, j] = new Vector3(x, Height(patch, sea, baseHeight, x, z), z);
			}
		}
		// Flat shading: every triangle gets its own vertices. Triangles reaching into the shore use the rock material.
		var vertices = new List<Vector3>();
		var land = new List<int>();
		var shore = new List<int>();
		for (int i = 0; i < nx; i++) {
			for (int j = 0; j < nz; j++) {
				Vector3 a = grid[i, j], b = grid[i + 1, j], c = grid[i + 1, j + 1], d = grid[i, j + 1];
				bool flip = (i + j) % 2 == 0;
				foreach (Vector3[] tri in flip ? new[] { new[] { a, d, c }, new[] { a, c, b } } : new[] { new[] { a, d, b }, new[] { b, d, c } }) {
					List<int> target = tri.Min(v => v.y) < sea + 0.25f ? shore : land;
					foreach (Vector3 v in tri) {
						target.Add(vertices.Count);
						vertices.Add(v);
					}
				}
			}
		}
		var mesh = new Mesh { name = patch.scene + "_Expansion" };
		mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
		mesh.SetVertices(vertices);
		mesh.subMeshCount = 2;
		mesh.SetTriangles(land, 0);
		mesh.SetTriangles(shore, 1);
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		return mesh;
	}

	// Model scaled so its larger ground side is prop.size metres, standing on the ground below (x, z).
	static void PlaceProp(Transform root, Prop prop) {
		string[] parts = prop.model.Split('/');
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/" + parts[0] + "/Models/" + parts[1] + ".fbx");
		if (model == null)
			throw new System.Exception("MapExpansion: missing model " + prop.model);
		GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
		instance.name = parts[1];
		instance.isStatic = true;
		instance.transform.rotation = Quaternion.Euler(prop.tilt, prop.yaw, 0f);
		Bounds b = WorldBounds(instance);
		instance.transform.localScale *= prop.size / Mathf.Max(b.size.x, b.size.z, 0.01f);
		RaycastHit hit;
		float y = Physics.Raycast(new Vector3(prop.x, 500f, prop.z), Vector3.down, out hit, 1000f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
		instance.transform.position = new Vector3(prop.x, y, prop.z);
		b = WorldBounds(instance);
		// Sink a fifth of the height into the ground so tilted debris looks embedded, not floating.
		instance.transform.position += Vector3.up * (y - b.min.y - b.size.y * 0.2f);
		foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>()) {
			filter.gameObject.isStatic = true;
			filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
		}
		Physics.SyncTransforms();
	}

	static Bounds WorldBounds(GameObject go) {
		Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
		Bounds b = renderers[0].bounds;
		foreach (Renderer r in renderers)
			b.Encapsulate(r.bounds);
		return b;
	}
}
