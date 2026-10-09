using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SupportTests {

	static GameObject Prefab(string name) {
		return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
	}

	static float Height(GameObject prefab) {
		GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
		try {
			return EnemySetup.MeshBounds(instance).size.y;
		} finally {
			Object.DestroyImmediate(instance);
		}
	}

	// Spec §6.4: energy-core language, 0.9-1.1 x the 0.6 m core height, never energy.
	[Test]
	public void SupportPrefabsLookRight() {
		var kinds = new[] { ("Support Shield", SupportKind.Shield), ("Support Heal10", SupportKind.Heal10), ("Support Heal20", SupportKind.Heal20), ("Overdrive", SupportKind.Overdrive) };
		foreach (var (name, kind) in kinds) {
			GameObject prefab = Prefab(name);
			Assert.IsNotNull(prefab, name);
			SupportPickup pickup = prefab.GetComponent<SupportPickup>();
			Assert.IsNotNull(pickup, name);
			Assert.AreEqual(kind, pickup.kind, name);
			Assert.IsNull(prefab.GetComponentInChildren<Treasure>(true), name + " is not energy");
			Assert.IsTrue(prefab.GetComponent<SphereCollider>().isTrigger, name);
			Assert.IsNotNull(prefab.transform.Find("Model").GetComponent<EnergyBob>(), name);
			Assert.That(Height(prefab), Is.InRange(0.54f, 0.66f), name + " height");
			Assert.AreEqual(25f, pickup.lifetime, name);
		}
	}

	[Test]
	public void ShieldBlocksEnemyAttacksOnly() {
		GameFlow.ResetForScene();
		GameObject robo = new GameObject("Robo");
		try {
			Health health = robo.AddComponent<Health>();
			health.maxHealth = health.healthPoints = 100f;
			ShieldEffect shield = robo.AddComponent<ShieldEffect>();
			int blocked = 0;
			health.Blocked += () => blocked++;
			shield.Activate();
			Assert.AreEqual(ShieldEffect.Duration, shield.Remaining, 0.001f);
			Assert.IsFalse(health.TakeDamage(10f, DamageKind.EnemyAttack), "shield blocks the strike");
			Assert.AreEqual(1, blocked);
			Assert.AreEqual(100f, health.healthPoints);
			Assert.IsTrue(health.TakeDamage(0f, DamageKind.FatalHazard), "never blocks acid/lava/falling");
		} finally {
			Object.DestroyImmediate(robo);
		}
	}

	[Test]
	public void PlayerCarriesAShield() {
		ShieldEffect shield = Prefab("Player").GetComponent<ShieldEffect>();
		Assert.IsNotNull(shield);
		Assert.IsNotNull(shield.bubbleMaterial);
		Assert.That(shield.bubbleMaterial.color.a, Is.InRange(0.2f, 0.3f), "20-30% opacity");
	}

	// Spec §6.3: support budget and odds per level.
	[Test]
	public void SupportConfigFollowsTheTable() {
		int[] cap = { 1, 2, 3, 3 };
		float[] min = { 10f, 8f, 7f, 6f }, max = { 14f, 12f, 10f, 9f };
		for (int i = 0; i < 4; i++) {
			LevelConfig c = LevelCatalog.All[i];
			Assert.AreEqual(cap[i], c.supportCap, c.levelId);
			Assert.AreEqual(min[i], c.supportIntervalMin, 0.001f, c.levelId);
			Assert.AreEqual(max[i], c.supportIntervalMax, 0.001f, c.levelId);
			CollectionAssert.AreEqual(i == 0 ? new[] { 40f, 40f, 20f, 0f } : new[] { 30f, 35f, 20f, 15f }, c.supportWeights, c.levelId);
			Assert.AreEqual(i == 0 ? 5f : -1f, c.firstShieldAfter, c.levelId);
		}
	}

	[Test]
	public void ChooseFollowsTheWeights() {
		float[] w = { 30f, 35f, 20f, 15f };
		Assert.AreEqual(SupportKind.Shield, SupportSpawnDirector.Choose(w, 0.29f));
		Assert.AreEqual(SupportKind.Heal10, SupportSpawnDirector.Choose(w, 0.30f));
		Assert.AreEqual(SupportKind.Heal10, SupportSpawnDirector.Choose(w, 0.649f));
		Assert.AreEqual(SupportKind.Heal20, SupportSpawnDirector.Choose(w, 0.65f));
		Assert.AreEqual(SupportKind.Overdrive, SupportSpawnDirector.Choose(w, 0.85f));
		Assert.AreEqual(SupportKind.Overdrive, SupportSpawnDirector.Choose(w, 0.9999f));
		Assert.AreEqual(SupportKind.Heal20, SupportSpawnDirector.Choose(new[] { 40f, 40f, 20f, 0f }, 0.9999f), "no Overdrive in Level1");
	}

	[Test]
	public void OneSupportDirectorPerLevelAndNoOverdriveDirector() {
		Assert.IsNull(System.Type.GetType("OverdriveDirector, Assembly-CSharp"), "the old Overdrive spawner is gone");
		foreach (LevelConfig level in LevelCatalog.All) {
			UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
			SupportSpawnDirector d = Object.FindObjectOfType<SupportSpawnDirector>();
			Assert.IsNotNull(d, level.levelId);
			Assert.IsNotNull(d.shieldPrefab);
			Assert.IsNotNull(d.heal10Prefab);
			Assert.IsNotNull(d.heal20Prefab);
			Assert.IsNotNull(d.overdrivePrefab);
			foreach (MonoBehaviour m in Object.FindObjectsOfType<MonoBehaviour>(true))
				Assert.IsNotNull(m, level.levelId + " has a missing script");
		}
	}
}
