using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Alien trees, rocks and plants per level (Quaternius Ultimate Space Kit + Kenney Nature Kit, CC0), under
/// "Decor (Generated)". Placed on walkable ground reached from the robot's start, away from the start, energy landing
/// points, enemy spawn points and hazards, and only in open ground (every cell 2 m around is walkable) so no corridor
/// is closed. Trees and large rocks get a thin static collider (the robot bumps into them, the NavMesh walks around
/// them); plants and pebbles have none. Seeded per level, rebuilt from scratch on every run.
/// Run before EnemySetup (NavMesh bake) and EnergySetup (landing points), as ChallengeBuildPipeline does.
/// </summary>
public static class DecorSetup {

	public const string RootName = "Decor (Generated)";
	const string Space = "Assets/ThirdParty/QuaterniusSpaceKit/";
	const string Kenney = "Assets/ThirdParty/KenneyNatureKit/Models/";

	struct Piece {
		public string path;
		public float height;   // target height in metres
		public bool solid;

		public Piece(string path, float height, bool solid) {
			this.path = path;
			this.height = height;
			this.solid = solid;
		}
	}

	static Piece Tree(string name, float h) { return new Piece(Space + name + ".fbx", h, true); }
	static Piece BigRock(string path, float h) { return new Piece(path, h, true); }
	static Piece Small(string path, float h) { return new Piece(path, h, false); }

	// Level themes (spec §7.2 names): acid island, abandoned mining station, lava valley, abandoned launch pad.
	static readonly Dictionary<string, Piece[][]> Themes = new Dictionary<string, Piece[][]> {
		{ "Level1", new[] {
			new[] { Tree("TreeBlob", 4.2f), Tree("TreeSpiral", 5.0f), BigRock(Kenney + "rock_largeA.fbx", 1.6f), BigRock(Kenney + "rock_tallB.fbx", 2.2f) },
			new[] { Small(Space + "Plant.fbx", 1.1f), Small(Space + "Bush.fbx", 0.9f), Small(Space + "Grass.fbx", 0.5f), Small(Kenney + "rock_smallA.fbx", 0.35f), Small(Kenney + "stone_smallB.fbx", 0.3f) } } },
		{ "Level2", new[] {
			new[] { BigRock(Space + "Rock.fbx", 2.2f), BigRock(Kenney + "rock_largeC.fbx", 1.6f), BigRock(Kenney + "rock_tallE.fbx", 2.4f), Tree("TreeLight", 4.5f) },
			new[] { Small(Kenney + "rock_smallC.fbx", 0.4f), Small(Kenney + "rock_smallFlatA.fbx", 0.25f), Small(Space + "Grass.fbx", 0.45f), Small(Kenney + "stone_smallE.fbx", 0.3f) } } },
		{ "Level3", new[] {
			new[] { Tree("TreeLava", 4.5f), Tree("TreeSpikes", 4.8f), BigRock(Space + "RockLarge.fbx", 1.8f), BigRock(Kenney + "rock_tallH.fbx", 2.6f) },
			new[] { Small(Kenney + "rock_smallA.fbx", 0.4f), Small(Kenney + "rock_smallC.fbx", 0.35f), Small(Kenney + "stone_smallE.fbx", 0.3f) } } },
		{ "Level4", new[] {
			new[] { Tree("TreeSwirl", 5.5f), Tree("TreeSpiral", 5.0f), Tree("TreeLight", 4.5f), BigRock(Space + "Rock.fbx", 2.0f), BigRock(Kenney + "rock_largeE.fbx", 1.6f) },
			new[] { Small(Space + "Plant.fbx", 1.0f), Small(Space + "Bush.fbx", 0.9f), Small(Space + "Grass.fbx", 0.5f), Small(Kenney + "stone_smallB.fbx", 0.3f) } } },
	};

	public const int SolidCount = 14, SmallCount = 30;
	public const float StartClear = 7f, PointClear = 3f, SolidSpacing = 7f, SmallSpacing = 3f;

	[MenuItem("Tools/Robo Lac Loi/Apply Decor")]
	public static void Apply() {
		foreach (LevelConfig level in LevelCatalog.All) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			int placed = Dress(level);
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log("DecorSetup: " + level.levelId + " " + placed + " pieces");
		}
	}

	static int Dress(LevelConfig level) {
		GameObject old = GameObject.Find(RootName);
		if (old != null)
			Object.DestroyImmediate(old);
		Physics.SyncTransforms();
		Piece[][] theme;
		if (!Themes.TryGetValue(level.levelId, out theme))
			return 0;
		GameObject root = new GameObject(RootName);

		Vector3 start = WalkableGrid.PlayerStart();
		Dictionary<Vector2Int, float> cells = WalkableGrid.Reachable(start);
		var keep = new List<Vector3>();   // points that must stay clear
		EnergySpawnDirector energy = Object.FindObjectOfType<EnergySpawnDirector>();
		if (energy != null)
			keep.AddRange(energy.landingPoints.Where(t => t != null).Select(t => t.position));
		EnemyDirector enemies = Object.FindObjectOfType<EnemyDirector>();
		if (enemies != null)
			keep.AddRange(enemies.spawnPoints.Where(t => t != null).Select(t => t.position));

		var rng = new System.Random(level.order * 7919);
		List<Vector2Int> order = cells.Keys.OrderBy(c => rng.Next()).ToList();
		var used = new List<Vector3>();
		int solids = 0, smalls = 0;
		foreach (Vector2Int cell in order) {
			if (solids >= SolidCount && smalls >= SmallCount)
				break;
			Vector3 point = new Vector3(cell.x, cells[cell], cell.y);
			if (Flat(point, start) < StartClear || keep.Any(k => Flat(point, k) < PointClear) || WalkableGrid.InHazard(point - Vector3.up * 0.3f))
				continue;
			bool solid = solids < SolidCount && Open(cell, cells, 2);
			if (!solid && (smalls >= SmallCount || !Open(cell, cells, 1)))
				continue;
			float spacing = solid ? SolidSpacing : SmallSpacing;
			if (used.Any(u => Flat(point, u) < spacing))
				continue;
			Piece[] set = solid ? theme[0] : theme[1];
			if (Place(set[rng.Next(set.Length)], point, (float)rng.NextDouble() * 360f, root.transform) == null)
				continue;
			used.Add(point);
			if (solid) solids++; else smalls++;
		}
		return solids + smalls;
	}

	// Every cell within r is walkable ground at a similar height: open ground, not a ridge or a bridge.
	static bool Open(Vector2Int cell, Dictionary<Vector2Int, float> cells, int r) {
		float h = cells[cell];
		for (int x = -r; x <= r; x++)
			for (int z = -r; z <= r; z++) {
				float n;
				if (!cells.TryGetValue(cell + new Vector2Int(x, z), out n) || Mathf.Abs(n - h) > 0.4f)
					return false;
			}
		return true;
	}

	static float Flat(Vector3 a, Vector3 b) {
		return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
	}

	static GameObject Place(Piece piece, Vector3 point, float yaw, Transform parent) {
		GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(piece.path);
		if (asset == null) {
			Debug.LogWarning("DecorSetup: missing " + piece.path);
			return null;
		}
		GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
		go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;   // keep the FBX's own up-axis fix
		Bounds b = EnemySetup.MeshBounds(go);
		go.transform.localScale *= piece.height / Mathf.Max(0.01f, b.size.y);
		b = EnemySetup.MeshBounds(go);
		// Feet slightly into the ground so slopes show no gap.
		go.transform.position += new Vector3(point.x - b.center.x, point.y - b.min.y - 0.05f, point.z - b.center.z);
		foreach (Collider c in go.GetComponentsInChildren<Collider>())
			Object.DestroyImmediate(c);
		foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
			r.shadowCastingMode = piece.solid ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
		if (piece.solid) {
			// A thin trunk-sized capsule: the robot bumps into it, the NavMesh is carved around it.
			CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
			b = EnemySetup.MeshBounds(go);
			float scale = go.transform.lossyScale.y;
			capsule.radius = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.22f, 0.3f, 0.9f) / scale;
			capsule.height = piece.height / scale;
			capsule.center = go.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + piece.height / 2f, b.center.z));
			GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
		} else {
			GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
		}
		return go;
	}
}
