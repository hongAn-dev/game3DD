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
}
