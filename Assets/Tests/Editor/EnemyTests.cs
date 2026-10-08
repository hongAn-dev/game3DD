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

	[Test]
	public void SpeedsAndTimingsFollowSpec() {
		EnemyBrain boss = Prefab("Enemy - Monster").GetComponent<EnemyBrain>();
		EnemyBrain creep = Prefab("Enemy - Crater").GetComponent<EnemyBrain>();
		CollectionAssert.AreEqual(new[] { 0.70f, 0.85f, 1.00f }, boss.speedFactor);
		CollectionAssert.AreEqual(new[] { 0.80f, 0.95f, 1.05f }, creep.speedFactor);
		Assert.AreEqual(9f * 0.85f, EnemyBrain.Speed(boss.speedFactor, GameSettings.gameDifficulties.Normal, 9f), 0.001f);
		Assert.AreEqual(new[] { 0.55f, 0.15f, 0.8f }, new[] { boss.windup, boss.strike, boss.recover });
		Assert.AreEqual(new[] { 0.35f, 0.10f, 0.7f }, new[] { creep.windup, creep.strike, creep.recover });
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
