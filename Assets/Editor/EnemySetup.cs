using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// T5 enemies (spec §6): rebuilds the boss ("Enemy - Monster": root scale 1, 2.4 m tall) and the creep
/// ("Enemy - Crater": BugRobot model, ~0.85 m) with EnemyBrain + NavMeshAgent and no passive damage; per level bakes a
/// NavMesh from static colliders with every HazardVolume marked not walkable (NavMeshLoader), authors "Enemy Spawn
/// Points", adds the EnemyDirector and removes placed monsters and the old SetupLevel spawners. Re-runnable.
/// </summary>
public static class EnemySetup {

	const string BossPath = "Assets/Prefabs/Enemy - Monster.prefab";
	const string CreepPath = "Assets/Prefabs/Enemy - Crater.prefab";
	const string BugModel = "Assets/ThirdParty/RoboLacLoi/Models/BugRobot.fbx";
	const string TelegraphPath = "Assets/ThirdParty/RoboLacLoi/Materials/Telegraph.mat";
	public const float BossHeight = 2.4f;
	public const float CreepLength = 0.85f;
	const float RobotRadius = 0.5f;

	[MenuItem("Tools/Robo Lac Loi/Apply Enemy Setup")]
	public static void Apply() {
		Material telegraph = TelegraphMaterial();
		BuildBoss(telegraph);
		BuildCreep(telegraph);
		foreach (LevelConfig level in LevelCatalog.All) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			RemoveOldEnemies();
			NavMeshData data = BakeNavMesh(level.levelId);
			GameManager manager = Object.FindObjectOfType<GameManager>();
			NavMeshLoader loader = manager.GetComponent<NavMeshLoader>();
			if (loader == null)
				loader = manager.gameObject.AddComponent<NavMeshLoader>();
			loader.data = data;
			EnemyDirector director = manager.GetComponent<EnemyDirector>();
			if (director == null)
				director = manager.gameObject.AddComponent<EnemyDirector>();
			director.bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
			director.creepPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreepPath);
			director.telegraphMaterial = telegraph;
			director.intervalMin = 2f;
			director.intervalMax = 3f;
			director.minPlayerDistance = 8f;
			director.spawnPoints = SpawnPoints(data).ToArray();
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log("EnemySetup: " + level.levelId + " navmesh + " + director.spawnPoints.Length + " spawn points");
		}
		Debug.Log("EnemySetup: done");
	}

	static Material TelegraphMaterial() {
		Material material = AssetDatabase.LoadAssetAtPath<Material>(TelegraphPath);
		if (material == null) {
			material = new Material(Shader.Find("Sprites/Default"));
			AssetDatabase.CreateAsset(material, TelegraphPath);
		}
		material.color = new Color(1f, 0.18f, 0.08f, 0.45f);
		EditorUtility.SetDirty(material);
		return material;
	}

	// ---------- prefabs ----------

	static void Strip(GameObject root) {
		foreach (var c in root.GetComponents<Damage>()) Object.DestroyImmediate(c);
		foreach (var c in root.GetComponents<MonoBehaviour>().Where(m => m != null && (m.GetType().Name == "MonsterChaser" || m.GetType().Name == "Chaser")).ToList())
			Object.DestroyImmediate(c);
		foreach (var c in root.GetComponents<Collider>()) Object.DestroyImmediate(c);
		Transform old = root.transform.Find("Telegraph");
		if (old != null)
			Object.DestroyImmediate(old.gameObject);
	}

	static void Body(GameObject root, float height, float radius, float centreY) {
		Rigidbody body = root.GetComponent<Rigidbody>();
		if (body == null)
			body = root.AddComponent<Rigidbody>();
		body.isKinematic = true;
		body.useGravity = false;
		CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
		capsule.direction = 1;
		capsule.radius = radius;
		capsule.height = Mathf.Max(height, radius * 2f);
		capsule.center = new Vector3(0f, Mathf.Max(centreY, capsule.height / 2f), 0f);   // never below the feet
		NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
		if (agent == null)
			agent = root.AddComponent<NavMeshAgent>();
		agent.radius = radius;
		agent.height = height;
		agent.baseOffset = 0f;
		agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
	}

	static EnemyBrain Brain(GameObject root) {
		EnemyBrain brain = root.GetComponent<EnemyBrain>();
		return brain != null ? brain : root.AddComponent<EnemyBrain>();
	}

	// Red disc on the ground covering the strike range; EnemyBrain scales its parent during the windup.
	static GameObject Telegraph(GameObject root, float range, Material material) {
		GameObject pivot = new GameObject("Telegraph");
		pivot.transform.SetParent(root.transform, false);
		pivot.transform.localPosition = Vector3.up * 0.04f;
		GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		disc.name = "Disc";
		Object.DestroyImmediate(disc.GetComponent<Collider>());
		disc.transform.SetParent(pivot.transform, false);
		disc.transform.localScale = new Vector3(range * 2f, 0.005f, range * 2f) / root.transform.localScale.x;
		disc.GetComponent<Renderer>().sharedMaterial = material;
		disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		pivot.SetActive(false);
		return pivot;
	}

	/// <summary>World-space size of the active visual from mesh bounds (skinned renderer bounds are stale in prefab contents).</summary>
	public static Bounds MeshBounds(GameObject root) {
		bool any = false;
		Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
		foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) {
			SkinnedMeshRenderer skinned = r as SkinnedMeshRenderer;
			MeshFilter filter = r.GetComponent<MeshFilter>();
			Mesh mesh = skinned != null ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
			if (mesh == null)
				continue;
			Bounds b = mesh.bounds;
			for (int i = 0; i < 8; i++) {
				Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
				Vector3 world = r.transform.localToWorldMatrix.MultiplyPoint3x4(corner);
				if (!any) {
					bounds = new Bounds(world, Vector3.zero);
					any = true;
				} else {
					bounds.Encapsulate(world);
				}
			}
		}
		return bounds;
	}

	static void BuildBoss(Material telegraph) {
		GameObject root = PrefabUtility.LoadPrefabContents(BossPath);
		try {
			Strip(root);
			root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			Transform model = root.transform.Find("Model");
			// Fold the old root scale into the model, then scale the model to the target height, feet at the root.
			model.localScale *= root.transform.localScale.x;
			model.localPosition *= root.transform.localScale.x;
			root.transform.localScale = Vector3.one;
			Bounds b = MeshBounds(root);
			model.localScale *= BossHeight / b.size.y;
			b = MeshBounds(root);
			model.localPosition += new Vector3(-(b.center.x - root.transform.position.x), -(b.min.y - root.transform.position.y), -(b.center.z - root.transform.position.z));
			b = MeshBounds(root);
			float radius = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.5f, 0.5f, 0.9f);
			Body(root, BossHeight, radius, BossHeight / 2f);
			EnemyBrain brain = Brain(root);
			// Defaults = L3 Normal; EnemyDirector applies the level profile when it spawns the boss.
			brain.speed = 6.5f;
			brain.damage = 25;
			brain.windup = EnemyProfile.BossWindup;
			brain.strike = EnemyProfile.BossStrike;
			brain.recover = EnemyProfile.BossRecover;
			brain.turnSpeed = 270f;
			brain.attackRange = radius + RobotRadius + 0.9f;
			brain.attackAngle = 50f;
			brain.acceleration = 12f;
			brain.idleClip = "Idle";
			brain.runClip = "Run";
			brain.attackClip = "Attack";
			brain.canSlam = true;
			brain.slamRadius = 2.4f;
			brain.slamReach = 1.2f;
			brain.telegraph = Telegraph(root, brain.slamRadius, telegraph);   // the slam circle at its real size
			PrefabUtility.SaveAsPrefabAsset(root, BossPath);
			Debug.Log("EnemySetup: boss " + b.size.ToString("F2") + " radius " + radius.ToString("F2") + " range " + brain.attackRange.ToString("F2"));
		} finally {
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	static void BuildCreep(Material telegraph) {
		GameObject root = PrefabUtility.LoadPrefabContents(CreepPath);
		try {
			Strip(root);
			foreach (Transform child in root.transform.Cast<Transform>().ToList())
				Object.DestroyImmediate(child.gameObject);
			foreach (var r in root.GetComponents<Renderer>()) Object.DestroyImmediate(r);
			foreach (var f in root.GetComponents<MeshFilter>()) Object.DestroyImmediate(f);
			root.transform.localScale = Vector3.one;
			root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

			GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BugModel), root.transform);
			model.name = "Model";
			Bounds b = MeshBounds(root);
			model.transform.localScale *= CreepLength / Mathf.Max(b.size.x, b.size.z);
			// The eye must look along +Z (the agent's forward).
			Transform eye = model.GetComponentsInChildren<Transform>().First(t => t.name == "Eye");
			if (eye.position.z < root.transform.position.z)
				model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
			b = MeshBounds(root);
			model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
			b = MeshBounds(root);
			if (model.GetComponent<BugWalk>() == null)
				model.AddComponent<BugWalk>();

			Body(root, Mathf.Max(b.size.y, 0.5f), 0.4f, Mathf.Max(b.size.y, 0.5f) / 2f);
			EnemyBrain brain = Brain(root);
			// Defaults = L2 Normal; EnemyDirector applies the level profile when it spawns the creep.
			brain.speed = 6f;
			brain.damage = 12;
			brain.windup = 0.60f;
			brain.strike = EnemyProfile.CreepStrike;
			brain.recover = 0.95f;
			brain.turnSpeed = 300f;
			brain.attackRange = 0.4f + RobotRadius + 0.5f;
			brain.attackAngle = 45f;
			brain.acceleration = 14f;
			brain.idleClip = brain.runClip = brain.attackClip = "";
			brain.telegraph = Telegraph(root, brain.attackRange, telegraph);
			PrefabUtility.SaveAsPrefabAsset(root, CreepPath);
			Debug.Log("EnemySetup: creep " + b.size.ToString("F2") + " range " + brain.attackRange.ToString("F2"));
		} finally {
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	// ---------- levels ----------

	static void RemoveOldEnemies() {
		GameObject monsters = GameObject.Find("Monsters");
		if (monsters != null)
			Object.DestroyImmediate(monsters);
		foreach (EnemyBrain placed in Object.FindObjectsOfType<EnemyBrain>()) {
			GameObject instance = PrefabUtility.GetOutermostPrefabInstanceRoot(placed.gameObject);
			Object.DestroyImmediate(instance != null ? instance : placed.gameObject);
		}
		foreach (MonoBehaviour old in Object.FindObjectsOfType<MonoBehaviour>().Where(m => m != null && m.GetType().Name == "SetupLevel").ToList())
			Object.DestroyImmediate(old);
	}

	/// <summary>An enemy spawned at point can walk all the way to the robot's start (no NavMesh islands).</summary>
	public static bool AgentsReach(Vector3 from, Vector3 to) {
		var path = new NavMeshPath();
		return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
	}

	public static Bounds LevelArea() {
		Collider[] solid = Object.FindObjectsOfType<Collider>().Where(c => !c.isTrigger && c.attachedRigidbody == null).ToArray();
		Bounds area = solid[0].bounds;
		foreach (Collider c in solid)
			area.Encapsulate(c.bounds);
		area.Expand(4f);
		return area;
	}

	static NavMeshData BakeNavMesh(string levelId) {
		Bounds area = LevelArea();
		NavMeshData data = NavMeshBake.Build(area);
		data.name = levelId + " NavMesh";
		string path = "Assets/Scenes/" + levelId + "/NavMesh.asset";
		AssetDatabase.DeleteAsset(path);
		AssetDatabase.CreateAsset(data, path);
		return data;
	}

	// Points on the NavMesh reachable from the start, at least 12 m from it, spread out (farthest-point).
	static List<Transform> SpawnPoints(NavMeshData data) {
		GameObject old = GameObject.Find("Enemy Spawn Points");
		if (old != null)
			Object.DestroyImmediate(old);
		Transform root = new GameObject("Enemy Spawn Points").transform;

		NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
		var candidates = new List<Vector3>();
		Vector3 start = WalkableGrid.PlayerStart();
		RaycastHit startGround;
		NavMeshHit startHit;
		WalkableGrid.Ground(start.x, start.z, out startGround);
		NavMesh.SamplePosition(startGround.point, out startHit, 3f, NavMesh.AllAreas);
		Vector3 startOnMesh = startHit.position;
		try {
			foreach (Vector2Int cell in WalkableGrid.Reachable(start).Keys) {
				if (cell.x % 4 != 0 || cell.y % 4 != 0)
					continue;
				NavMeshHit hit;
				Vector3 p = new Vector3(cell.x, 0f, cell.y);
				RaycastHit ground;
				if (!WalkableGrid.Ground(p.x, p.z, out ground))
					continue;
				if (NavMesh.SamplePosition(ground.point, out hit, 0.6f, NavMesh.AllAreas) && Vector3.Distance(hit.position, start) >= 12f
					&& AgentsReach(startOnMesh, hit.position))
					candidates.Add(hit.position);
			}
		} finally {
			instance.Remove();
		}

		var chosen = new List<Vector3>();
		while (chosen.Count < 12 && candidates.Count > 0) {
			Vector3 best = chosen.Count == 0 ? candidates.OrderByDescending(c => Vector3.Distance(c, start)).First()
				: candidates.OrderByDescending(c => chosen.Min(o => Vector3.Distance(o, c))).First();
			chosen.Add(best);
			candidates.Remove(best);
		}
		var points = new List<Transform>();
		for (int i = 0; i < chosen.Count; i++) {
			Transform point = new GameObject("Enemy Spawn Point " + i).transform;
			point.SetParent(root, false);
			point.position = chosen[i];
			points.Add(point);
		}
		return points;
	}
}
