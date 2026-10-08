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

	[Test]
	public void AllowedAliveShrinksWithRemainingNeed() {
		Assert.AreEqual(5, EnergySpawnDirector.AllowedAlive(5, 6, 0));
		Assert.AreEqual(1, EnergySpawnDirector.AllowedAlive(5, 6, 5));
		Assert.AreEqual(0, EnergySpawnDirector.AllowedAlive(5, 6, 6));
		Assert.AreEqual(0, EnergySpawnDirector.AllowedAlive(5, 6, 9));
	}

	[Test]
	public void PickPointPrefersFarFromCoresAndPlayer() {
		var points = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(20, 0, 0), new Vector3(2, 0, 0) };
		var occupied = new List<Vector3> { new Vector3(0, 0, 0) };
		// Player stands on (20,0,0): that point is too close; (10,0,0) is farthest from the occupied core.
		Assert.AreEqual(1, EnergySpawnDirector.PickPoint(points, occupied, new Vector3(20, 0, 0), 4f));
		Assert.AreEqual(-1, EnergySpawnDirector.PickPoint(new List<Vector3> { Vector3.zero }, occupied, new Vector3(50, 0, 0), 4f));
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
				StringAssert.DoesNotContain("Water", hit.collider.name, level.levelId);
			}
		}
	}
}
