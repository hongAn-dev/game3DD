using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public class EnemyTests {

	static GameObject Prefab(string name) {
		return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
	}

	static Bounds Measure(GameObject prefab) {
		GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
		try {
			return EnemySetup.MeshBounds(instance);
		} finally {
			Object.DestroyImmediate(instance);
		}
	}

	[Test]
	public void BossIsAboutTwoAndAHalfRobotsTall() {
		GameObject boss = Prefab("Enemy - Monster");
		Assert.AreEqual(Vector3.one, boss.transform.localScale, "root scale normalised");
		Bounds b = Measure(boss);
		Assert.That(b.size.y, Is.InRange(2.0f, 2.6f), "height in robot diameters");
		CapsuleCollider capsule = boss.GetComponent<CapsuleCollider>();
		Assert.AreEqual(b.size.y, capsule.height, 0.3f, "collider follows the new size");
		Assert.AreEqual(capsule.radius, boss.GetComponent<NavMeshAgent>().radius, 0.01f);
		Assert.AreEqual(b.size.y, boss.GetComponent<NavMeshAgent>().height, 0.3f);
	}

	[Test]
	public void CreepIsBugSized() {
		GameObject creep = Prefab("Enemy - Crater");
		Bounds b = Measure(creep);
		Assert.That(Mathf.Max(b.size.x, b.size.z), Is.InRange(0.7f, 1.0f));
		Assert.Less(b.size.y, Mathf.Max(b.size.x, b.size.z), "low body");
		Assert.AreEqual("BugRobot", PrefabUtility.GetCorrespondingObjectFromSource(creep.transform.Find("Model").gameObject).name);
		Assert.IsNotNull(creep.GetComponentInChildren<BugWalk>());
		Vector3 eye = creep.transform.InverseTransformPoint(creep.GetComponentsInChildren<Transform>().First(t => t.name == "Eye").GetComponent<Renderer>().bounds.center);
		Assert.Greater(eye.z, Mathf.Abs(eye.x), "eye looks along the agent's forward (+Z)");
	}

	[Test]
	public void EnemiesStandOnTheirRoot() {
		foreach (string name in new[] { "Enemy - Monster", "Enemy - Crater" }) {
			GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(name));
			try {
				instance.transform.position = new Vector3(10f, 5f, 10f);
				Bounds b = EnemySetup.MeshBounds(instance);
				Assert.AreEqual(5f, b.min.y, 0.05f, name + ": feet at the root (agent base)");
				Assert.AreEqual(10f, b.center.x, 0.6f, name);
				Assert.AreEqual(10f, b.center.z, 0.6f, name);
			} finally {
				Object.DestroyImmediate(instance);
			}
		}
	}

	[Test]
	public void EnemiesHaveNoPassiveDamage() {
		foreach (string name in new[] { "Enemy - Monster", "Enemy - Crater" }) {
			GameObject prefab = Prefab(name);
			Assert.IsEmpty(prefab.GetComponentsInChildren<Damage>(true), name);
			Assert.IsNotNull(prefab.GetComponent<EnemyBrain>(), name);
			Assert.IsTrue(prefab.GetComponent<Rigidbody>().isKinematic, name + ": only the agent moves the root");
			Assert.IsFalse(prefab.GetComponent<Collider>().isTrigger, name + ": body still blocks");
		}
	}

	static readonly GameSettings.gameDifficulties[] Difficulties = { GameSettings.gameDifficulties.Easy, GameSettings.gameDifficulties.Normal, GameSettings.gameDifficulties.Hard };

	// Spec §4.1: max simultaneous enemies (creep + boss) per level and difficulty; no boss in L1-L2.
	[Test]
	public void CapsFollowTheDifficultyTable() {
		int[][] total = { new[] { 1, 1, 1 }, new[] { 2, 3, 4 }, new[] { 4, 5, 6 }, new[] { 6, 7, 8 } };
		int[] boss = { 0, 0, 1, 1 };
		for (int i = 0; i < 4; i++) {
			LevelConfig c = LevelCatalog.All[i];
			CollectionAssert.AreEqual(total[i], c.enemyCap, c.levelId);
			CollectionAssert.AreEqual(new[] { boss[i], boss[i], boss[i] }, c.bossCap, c.levelId);
		}
	}

	// Spec §4.1: Normal values per level; Easy speed x0.9 damage x0.8, Hard speed x1.1 damage x1.2, rounded.
	[Test]
	public void ProfilesFollowTheSpeedAndDamageTable() {
		float[] creepSpeed = { 5.0f, 6.0f, 6.8f, 7.5f };
		int[][] creepDamage = { new[] { 8, 10, 12 }, new[] { 10, 12, 14 }, new[] { 12, 15, 18 }, new[] { 14, 18, 22 } };
		float[] speedMul = { 0.9f, 1f, 1.1f };
		for (int i = 0; i < 4; i++)
			for (int d = 0; d < 3; d++) {
				EnemyProfile p = EnemyProfile.For(LevelCatalog.All[i], Difficulties[d], false);
				Assert.AreEqual(creepSpeed[i] * speedMul[d], p.speed, 0.001f, "creep speed L" + (i + 1) + " d" + d);
				Assert.AreEqual(creepDamage[i][d], p.damage, "creep damage L" + (i + 1) + " d" + d);
				Assert.Less(p.speed, 11f, "slower than the robot");
			}
		float[] bossSpeed = { 6.5f, 7.2f };
		int[][] bossDamage = { new[] { 20, 25, 30 }, new[] { 24, 30, 36 } };
		for (int i = 0; i < 2; i++)
			for (int d = 0; d < 3; d++) {
				EnemyProfile p = EnemyProfile.For(LevelCatalog.All[i + 2], Difficulties[d], true);
				Assert.AreEqual(bossSpeed[i] * speedMul[d], p.speed, 0.001f, "boss speed L" + (i + 3) + " d" + d);
				Assert.AreEqual(bossDamage[i][d], p.damage, "boss damage L" + (i + 3) + " d" + d);
			}
	}

	// Spec §4.3: creep windup 0.65/0.60/0.55/0.50 (+0.10 on Easy, Hard unchanged), strike 0.10, recover 1.0..0.85.
	[Test]
	public void CreepTimingsFollowTheLevel() {
		float[] windup = { 0.65f, 0.60f, 0.55f, 0.50f };
		float[] recover = { 1.0f, 0.95f, 0.90f, 0.85f };
		for (int i = 0; i < 4; i++) {
			EnemyProfile easy = EnemyProfile.For(LevelCatalog.All[i], GameSettings.gameDifficulties.Easy, false);
			EnemyProfile normal = EnemyProfile.For(LevelCatalog.All[i], GameSettings.gameDifficulties.Normal, false);
			EnemyProfile hard = EnemyProfile.For(LevelCatalog.All[i], GameSettings.gameDifficulties.Hard, false);
			Assert.AreEqual(windup[i], normal.windup, 0.001f);
			Assert.AreEqual(windup[i] + 0.10f, easy.windup, 0.001f);
			Assert.AreEqual(windup[i], hard.windup, 0.001f);
			Assert.AreEqual(0.10f, normal.strike, 0.001f);
			Assert.AreEqual(recover[i], normal.recover, 0.001f);
		}
	}

	// Spec §4.2: boss after at least 12 s and 30% of the energy target, or after 30 s regardless.
	[Test]
	public void BossGateOpensAt12sWith30PercentOrAt30s() {
		Assert.IsFalse(EnemyDirector.BossGateOpen(11.9f, 14, 14));
		Assert.IsTrue(EnemyDirector.BossGateOpen(12f, 5, 14));
		Assert.IsFalse(EnemyDirector.BossGateOpen(12f, 4, 14));
		Assert.IsFalse(EnemyDirector.BossGateOpen(29.9f, 0, 14));
		Assert.IsTrue(EnemyDirector.BossGateOpen(30f, 0, 14));
	}

	[Test]
	public void EnemyAccelerationAndTurnInRange() {
		EnemyBrain creep = Prefab("Enemy - Crater").GetComponent<EnemyBrain>();
		EnemyBrain boss = Prefab("Enemy - Monster").GetComponent<EnemyBrain>();
		Assert.That(creep.acceleration, Is.InRange(12f, 16f));
		Assert.That(boss.acceleration, Is.InRange(10f, 14f));
		Assert.That(creep.turnSpeed, Is.InRange(240f, 360f));
		Assert.That(boss.turnSpeed, Is.InRange(240f, 360f));
		Assert.IsNotNull(boss.telegraph);
		Assert.IsNotNull(creep.telegraph);
	}

	[Test]
	public void EveryLevelHasNavMeshAtStart() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			NavMeshLoader loader = Object.FindObjectOfType<NavMeshLoader>();
			Assert.IsNotNull(loader, level.levelId);
			Assert.IsNotNull(loader.data, level.levelId);
			NavMeshDataInstance instance = NavMesh.AddNavMeshData(loader.data);
			try {
				NavMeshHit hit;
				RaycastHit ground;
				Vector3 start = WalkableGrid.PlayerStart();
				Assert.IsTrue(WalkableGrid.Ground(start.x, start.z, out ground), level.levelId);
				Assert.IsTrue(NavMesh.SamplePosition(ground.point, out hit, 2.5f, NavMesh.AllAreas), level.levelId + ": no NavMesh near the start");
				foreach (Transform point in Object.FindObjectOfType<EnemyDirector>().spawnPoints) {
					Assert.IsTrue(NavMesh.SamplePosition(point.position, out hit, 1f, NavMesh.AllAreas), level.levelId + " " + point.name);
					Assert.GreaterOrEqual(Vector3.Distance(point.position, WalkableGrid.PlayerStart()), 10f, level.levelId + " " + point.name);
					NavMeshHit startHit;
					NavMesh.SamplePosition(ground.point, out startHit, 2.5f, NavMesh.AllAreas);
					Assert.IsTrue(EnemySetup.AgentsReach(startHit.position, point.position), level.levelId + " " + point.name + " is on a NavMesh island");
				}
				Assert.GreaterOrEqual(Object.FindObjectOfType<EnemyDirector>().spawnPoints.Length, 4, level.levelId);
			} finally {
				instance.Remove();
			}
		}
	}

	[Test]
	public void NavMeshAvoidsHazards() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			NavMeshDataInstance instance = NavMesh.AddNavMeshData(Object.FindObjectOfType<NavMeshLoader>().data);
			try {
				NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
				Assert.Greater(mesh.vertices.Length, 0, level.levelId);
				foreach (Vector3 v in mesh.vertices)
					Assert.IsFalse(WalkableGrid.InHazard(v + Vector3.up * 0.1f), level.levelId + ": NavMesh vertex " + v + " lies in a hazard");
			} finally {
				instance.Remove();
			}
		}
	}

	[Test]
	public void ScenesHoldNoOldEnemySources() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			Assert.IsEmpty(Object.FindObjectsOfType<EnemyBrain>(true), level.levelId + ": enemies come from the director only");
			Assert.IsFalse(Object.FindObjectsOfType<MonoBehaviour>(true).Any(m => m != null && m.GetType().Name == "SetupLevel"), level.levelId);
			Assert.IsNotNull(Object.FindObjectOfType<EnemyDirector>(), level.levelId);
		}
	}
}
