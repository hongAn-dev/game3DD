using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class HealthTests {

	Health health;

	[SetUp]
	public void Make() {
		GameFlow.ResetForScene();
		health = new GameObject("Robo").AddComponent<Health>();
		health.maxHealth = 100f;
		health.healthPoints = 100f;
	}

	[TearDown]
	public void Clean() {
		if (health != null)   // tests that open a scene already removed it
			Object.DestroyImmediate(health.gameObject);
		GameFlow.ResetForScene();
	}

	[Test]
	public void HealClampsAndNeverRevives() {
		Assert.IsTrue(health.TakeDamage(30f, DamageKind.EnemyAttack));
		Assert.AreEqual(70f, health.healthPoints);
		Assert.IsTrue(health.Heal(50f));
		Assert.AreEqual(100f, health.healthPoints, "clamped to max");
		Assert.IsFalse(health.Heal(10f), "nothing to heal when full");
		health.TakeDamage(0f, DamageKind.FatalHazard);
		Assert.AreEqual(0f, health.healthPoints);
		Assert.IsFalse(health.Heal(20f), "a dead robot is not revived");
		Assert.AreEqual(0f, health.healthPoints);
	}

	[Test]
	public void EnemyHitStartsInvulnerability() {
		Assert.IsTrue(health.TakeDamage(10f, DamageKind.EnemyAttack));
		Assert.AreEqual(Health.InvulnerableSeconds, health.InvulnerableLeft, 0.001f);
		Assert.IsFalse(health.TakeDamage(10f, DamageKind.EnemyAttack), "second hit inside the window");
		Assert.AreEqual(90f, health.healthPoints);
	}

	[Test]
	public void EventsReportWhatWasApplied() {
		float damaged = 0f, healed = 0f;
		health.Damaged += a => damaged = a;
		health.Healed += a => healed = a;
		health.healthPoints = 10f;
		health.TakeDamage(18f, DamageKind.EnemyAttack);
		Assert.AreEqual(10f, damaged, "only the HP that was left");
		health.healthPoints = 90f;
		health.Heal(25f);
		Assert.AreEqual(10f, healed, "only up to max");
	}

	[Test]
	public void FatalHazardIgnoresInvulnerability() {
		health.TakeDamage(10f, DamageKind.EnemyAttack);
		Assert.IsTrue(health.TakeDamage(0f, DamageKind.FatalHazard));
		Assert.AreEqual(0f, health.healthPoints);
	}

	[Test]
	public void NoDamageOutsidePlaying() {
		GameFlow.Pause();
		Assert.IsFalse(health.TakeDamage(10f, DamageKind.EnemyAttack));
		Assert.IsFalse(health.TakeDamage(0f, DamageKind.FatalHazard));
		Assert.AreEqual(100f, health.healthPoints);
		GameFlow.Resume();
	}

	[Test]
	public void PlayerPrefabHas100Hp() {
		Health prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab").GetComponent<Health>();
		Assert.AreEqual(100f, prefab.maxHealth);
		Assert.AreEqual(100f, prefab.healthPoints);
		Assert.AreEqual(100f, prefab.respawnHealthPoints);
		Assert.AreEqual(1, prefab.numberOfLives);
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			Health player = GameObject.FindWithTag("Player").GetComponent<Health>();
			Assert.AreEqual(100f, player.healthPoints, level.levelId);
			Assert.AreEqual(100f, player.maxHealth, level.levelId);
			Assert.AreEqual(1, player.numberOfLives, level.levelId);
		}
	}

	// Only strikes and hazards hurt the robot: no legacy contact damage anywhere it can meet.
	[Test]
	public void NoContactDamageOnPlayerOrEnemies() {
		foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath)) {
			if (path.EndsWith("WaterDeathZone.prefab"))
				continue;   // legacy, no longer placed in any level
			foreach (Damage damage in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Damage>(true))
				Assert.IsFalse(damage.damageOnCollision || damage.damageOnTrigger, path + " deals contact damage");
		}
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			Assert.IsEmpty(Object.FindObjectsOfType<Damage>(true).Where(d => d.damageOnCollision || d.damageOnTrigger), level.levelId);
		}
	}
}
