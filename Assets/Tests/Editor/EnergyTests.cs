using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
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
}
