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
			Assert.That(height, Is.InRange(0.85f, 0.95f), core.name);
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
		int[] target = { 6, 10, 14, 18 }, atStart = { 4, 8, 12, 16 }, cap = { 6, 12, 18, 24 }, batchMin = { 2, 3, 4, 5 }, batchMax = { 3, 4, 6, 7 };
		float[] intervalMin = { 1.5f, 1f, .8f, .8f }, intervalMax = { 2f, 1.5f, 1.2f, 1f };
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
	public void AllowedAliveUsesLevelCapAndDoesNotClampToRemainingScore() {
		Assert.AreEqual(6, EnergySpawnDirector.AllowedAlive(6, 6, 0));
		Assert.AreEqual(24, EnergySpawnDirector.AllowedAlive(30, 18, 0), "technical maximum is 24");
		Assert.AreEqual(24, EnergySpawnDirector.AllowedAlive(30, 18, 17));
		Assert.AreEqual(0, EnergySpawnDirector.AllowedAlive(30, 18, 18));
		Assert.AreEqual(0, EnergySpawnDirector.AllowedAlive(30, 18, 20));
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
			Assert.GreaterOrEqual(director.landingPoints.Length, level.energyPointMinimum, level.levelId);
			foreach (Transform point in director.landingPoints) {
				RaycastHit hit;
				Assert.IsTrue(Physics.Raycast(point.position + Vector3.up, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore),
					level.levelId + " " + point.name + " has no ground");
				Assert.IsFalse(WalkableGrid.InHazard(hit.point + Vector3.up * 0.2f), level.levelId + " " + point.name + " is in a hazard");
			}
		}
	}

	[Test]
	public void LandingPointsHaveRiskAndOrdinaryValidation() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			EnergySpawnDirector director = Object.FindObjectOfType<EnergySpawnDirector>();
			Assert.GreaterOrEqual(director.landingPoints.Length, level.energyPointMinimum, level.levelId);
			foreach (Transform point in director.landingPoints) {
				SpawnPoint metadata=point.GetComponent<SpawnPoint>();
				Assert.IsNotNull(metadata,point.name);
				Assert.IsTrue(WalkableGrid.Ground(point.position.x,point.position.z,out _),point.name+" has ground");
				if((metadata.riskFlags&SpawnRisk.Edge)!=0) {
					Assert.That(metadata.edgeClearance,Is.InRange(1.15f,1.81f),point.name+" edge clearance");
					Assert.Greater(metadata.escapeDirection.sqrMagnitude,.5f,point.name+" escape route");
				} else {
					Assert.GreaterOrEqual(metadata.edgeClearance,2.5f,point.name+" ordinary clearance");
					Assert.IsTrue(WalkableGrid.FootprintGround(point.position,.9f),point.name+" supports pickup footprint");
				}
			}
		}
	}
	[Test]
	public void DirectorTimingsAreWrittenIntoEveryScene() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			EnergySpawnDirector d = Object.FindObjectOfType<EnergySpawnDirector>();
			Assert.AreEqual(12f,d.staleDelay,level.levelId);
            Assert.AreEqual(3f,d.minPlayerDistance,level.levelId);
            Assert.AreEqual(1.5f,d.minSpacing,level.levelId);
            Assert.AreEqual(24,EnergySpawnDirector.MaxAlive,level.levelId);
			Assert.AreEqual(-1f, d.intervalOverride, level.levelId);
		}
	}

	[Test]
	public void LandingPointCountsMeetTheTarget() {
		int[] target = { 20, 40, 64, 88 };
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
