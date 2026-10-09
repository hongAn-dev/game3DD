using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class EnergyTests {

	[Test]
	public void CrystalModelHasNoFrame() {
		var names = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/RoboLacLoi/Models/EnergyCore.fbx")
			.OfType<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct().ToList();
		CollectionAssert.IsNotEmpty(names);
		CollectionAssert.IsSubsetOf(names, new[] { "CoreGlow", "CoreShard" });
	}

	static IEnumerable<Treasure> CoreRoots() {
		foreach (string name in new[] { "Coin", "Coin Bouncy", "Sil Coins", "Vin Coins" }) {
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
			foreach (Treasure t in prefab.GetComponentsInChildren<Treasure>(true))
				yield return t;
		}
	}

	[Test]
	public void CoinsUseFramelessCrystal() {
		foreach (Treasure core in CoreRoots()) {
			SphereCollider trigger = core.GetComponent<SphereCollider>();
			Assert.IsNotNull(trigger, core.name);
			Assert.IsTrue(trigger.isTrigger, core.name);
			Assert.IsNull(core.GetComponent<Rotate>(), core.name + ": root must not rotate");
			Transform model = core.transform.Find("Model");
			Assert.IsNotNull(model, core.name);
			Assert.IsNotNull(model.GetComponent<EnergyBob>(), core.name);
			Assert.AreEqual("EnergyCore", model.GetComponentInChildren<MeshFilter>().sharedMesh.name, core.name);
			float height = MeshHeight(model);
			Assert.That(height, Is.InRange(0.5f, 0.7f), core.name);
		}
	}

	// World-space height of the meshes under t, measured from mesh bounds (prefab assets have no live renderer bounds).
	static float MeshHeight(Transform t) {
		float min = float.MaxValue, max = float.MinValue;
		foreach (MeshFilter f in t.GetComponentsInChildren<MeshFilter>()) {
			Bounds b = f.sharedMesh.bounds;
			for (int i = 0; i < 8; i++) {
				Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
				float y = (f.transform.localToWorldMatrix * new Vector4(c.x, c.y, c.z, 1f)).y;
				min = Mathf.Min(min, y);
				max = Mathf.Max(max, y);
			}
		}
		return max - min;
	}

	// Spec §5.1, same on every difficulty.
	[Test]
	public void EnergyConfigFollowsTheTable() {
		int[] target = { 6, 10, 14, 18 }, atStart = { 3, 5, 7, 8 }, cap = { 4, 6, 8, 10 }, batchMin = { 1, 1, 2, 2 }, batchMax = { 1, 2, 3, 4 };
		float[] intervalMin = { 2.5f, 2.0f, 1.5f, 1.5f }, intervalMax = { 3.5f, 3.0f, 2.5f, 2.0f };
		for (int i = 0; i < 4; i++) {
			LevelConfig c = LevelCatalog.All[i];
			Assert.AreEqual(target[i], c.energyTarget, c.levelId);
			Assert.AreEqual(atStart[i], c.energyAtStart, c.levelId);
			Assert.AreEqual(cap[i], c.energyCap, c.levelId);
			Assert.AreEqual(intervalMin[i], c.energyIntervalMin, 0.001f, c.levelId);
			Assert.AreEqual(intervalMax[i], c.energyIntervalMax, 0.001f, c.levelId);
			Assert.AreEqual(batchMin[i], c.energyBatchMin, c.levelId);
			Assert.AreEqual(batchMax[i], c.energyBatchMax, c.levelId);
		}
	}

	[Test]
	public void AllowedAliveCapsAtTenAndRemaining() {
		Assert.AreEqual(4, EnergySpawnDirector.AllowedAlive(4, 6, 0));
		Assert.AreEqual(10, EnergySpawnDirector.AllowedAlive(12, 18, 0), "never more than 10");
		Assert.AreEqual(1, EnergySpawnDirector.AllowedAlive(10, 18, 17));
		Assert.AreEqual(0, EnergySpawnDirector.AllowedAlive(10, 18, 18));
		Assert.AreEqual(0, EnergySpawnDirector.AllowedAlive(10, 18, 20));
	}

	[Test]
	public void SpawnCountUsesFreeSlots() {
		Assert.AreEqual(1, EnergySpawnDirector.SpawnCount(4, 10, 7, 2), "only the free slot");
		Assert.AreEqual(0, EnergySpawnDirector.SpawnCount(3, 2, 2, 0));
		Assert.AreEqual(0, EnergySpawnDirector.SpawnCount(3, 2, 1, 2), "reservations count");
		Assert.AreEqual(2, EnergySpawnDirector.SpawnCount(2, 8, 3, 1));
	}

	[Test]
	public void RandomBatchIncludesTheUpperBound() {
		bool sawMax = false, sawMin = false;
		for (int i = 0; i < 500; i++) {
			int n = EnergySpawnDirector.RandomBatch(2, 4);
			Assert.That(n, Is.InRange(2, 4));
			sawMax |= n == 4;
			sawMin |= n == 2;
		}
		Assert.IsTrue(sawMax && sawMin);
		Assert.AreEqual(1, EnergySpawnDirector.RandomBatch(1, 1));
	}

	[Test]
	public void EveryLevelHasEnoughLandingPointsOnGround() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			EnergySpawnDirector director = Object.FindObjectOfType<EnergySpawnDirector>();
			Assert.IsNotNull(director, level.levelId);
			Assert.IsNotNull(director.corePrefab, level.levelId);
			Assert.GreaterOrEqual(director.landingPoints.Length, level.energyCap + 2, level.levelId);
			foreach (Transform point in director.landingPoints) {
				RaycastHit hit;
				Assert.IsTrue(Physics.Raycast(point.position + Vector3.up, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore),
					level.levelId + " " + point.name + " has no ground");
				Assert.IsFalse(WalkableGrid.InHazard(hit.point + Vector3.up * 0.2f), level.levelId + " " + point.name + " is in a hazard");
			}
		}
	}

	[Test]
	public void LandingPointsAreValidatedAndSpread() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			EnergySpawnDirector director = Object.FindObjectOfType<EnergySpawnDirector>();
			Assert.LessOrEqual(director.landingPoints.Length, EnergySetup.PointTarget(level.order) + 2, level.levelId);
			foreach (Transform point in director.landingPoints) {
				RaycastHit hit;
				Assert.IsTrue(Physics.Raycast(point.position + Vector3.up, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore), point.name);
				foreach (Vector3 side in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right }) {
					RaycastHit near;
					Vector3 p = point.position + side * 2.5f;
					Assert.IsTrue(WalkableGrid.Ground(p.x, p.z, out near) && Mathf.Abs(near.point.y - hit.point.y) <= 1.5f,
						level.levelId + " " + point.name + " sits on a ledge/prop top");
				}
			}
		}
	}

	// Spec §5.2: at least 12 / 20 / 28 / 36 landing points.
	[Test]
	public void DirectorTimingsAreWrittenIntoEveryScene() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			EnergySpawnDirector d = Object.FindObjectOfType<EnergySpawnDirector>();
			Assert.AreEqual(new[] { 0.35f, 0.5f, 12f, 3f, 2f }, new[] { d.markerDuration, d.dropDuration, d.staleDelay, d.minPlayerDistance, d.minSpacing }, level.levelId);
			Assert.AreEqual(-1f, d.intervalOverride, level.levelId);
		}
	}

	[Test]
	public void LandingPointCountsMeetTheTarget() {
		int[] target = { 12, 20, 28, 36 };
		for (int i = 0; i < 4; i++) {
			LevelConfig level = LevelCatalog.All[i];
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			Assert.GreaterOrEqual(Object.FindObjectOfType<EnergySpawnDirector>().landingPoints.Length, target[i], level.levelId);
		}
	}

	[Test]
	public void LandingPointsHaveAPathToTheStart() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			var nav = UnityEngine.AI.NavMesh.AddNavMeshData(Object.FindObjectOfType<NavMeshLoader>().data);
			try {
				Vector3 start = WalkableGrid.PlayerStart();
				RaycastHit ground;
				WalkableGrid.Ground(start.x, start.z, out ground);
				UnityEngine.AI.NavMeshHit from;
				Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(ground.point, out from, 3f, UnityEngine.AI.NavMesh.AllAreas), level.levelId);
				foreach (Transform point in Object.FindObjectOfType<EnergySpawnDirector>().landingPoints) {
					UnityEngine.AI.NavMeshHit to;
					var path = new UnityEngine.AI.NavMeshPath();
					Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(point.position, out to, 1.5f, UnityEngine.AI.NavMesh.AllAreas)
						&& UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, path)
						&& path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete, level.levelId + " " + point.name + " cannot be reached");
				}
			} finally {
				nav.Remove();
			}
		}
	}

	[Test]
	public void ScenesHoldNoAuthoredCores() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			Assert.AreEqual(0, Object.FindObjectsOfType<Treasure>(true).Length, level.levelId + ": cores come from the director only");
		}
	}
}
