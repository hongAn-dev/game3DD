using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class SupportPlayTests {

	GameObject player;
	Health health;

	IEnumerator Level1() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;
		player = GameObject.FindWithTag("Player");
		health = player.GetComponent<Health>();
		Object.FindObjectOfType<EnemyDirector>().enabled = false;
		yield return new WaitForSeconds(1.2f);   // the robot has landed
	}

	[TearDown]
	public void Reset() {
		GameFlow.ResetForScene();
	}

	GameObject Drop(string prefab) {
#if UNITY_EDITOR
		return Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + prefab + ".prefab"), player.transform.position, Quaternion.identity);
#else
		return null;
#endif
	}

	IEnumerator Physics2() {
		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();
		yield return null;
	}

	[UnityTest]
	public IEnumerator HealStaysWhenHpIsFull() {
		yield return Level1();
		GameObject heal = Drop("Support Heal10");
		yield return Physics2();
		Assert.IsTrue(heal != null, "left for later");
		Assert.AreEqual(100f, health.healthPoints);
	}

	[UnityTest]
	public IEnumerator HealRestoresAndConsumes() {
		yield return Level1();
		health.TakeDamage(30f, DamageKind.EnemyAttack);
		GameObject heal = Drop("Support Heal20");
		yield return Physics2();
		Assert.IsTrue(heal == null, "consumed");
		Assert.AreEqual(90f, health.healthPoints, 0.01f);
	}

	[UnityTest]
	public IEnumerator ShieldBlocksForFiveSeconds() {
		yield return Level1();
		Drop("Support Shield");
		yield return Physics2();
		ShieldEffect shield = player.GetComponent<ShieldEffect>();
		Assert.IsTrue(shield.Active);
		Assert.IsTrue(shield.Bubble.activeSelf, "bubble shown");
		Assert.IsFalse(health.TakeDamage(10f, DamageKind.EnemyAttack));
		yield return new WaitForSeconds(ShieldEffect.Duration + 0.2f);
		Assert.IsFalse(shield.Active);
		Assert.IsFalse(shield.Bubble.activeSelf, "bubble gone");
		Assert.IsTrue(health.TakeDamage(10f, DamageKind.EnemyAttack));
	}

	[UnityTest]
	public IEnumerator ShieldRefreshesWithoutStacking() {
		yield return Level1();
		ShieldEffect shield = player.GetComponent<ShieldEffect>();
		shield.Activate();
		yield return new WaitForSeconds(2f);
		shield.Activate();
		Assert.AreEqual(ShieldEffect.Duration, shield.Remaining, 0.05f, "reset to 5 s, not 8");
	}

	[UnityTest]
	public IEnumerator HudShowsShieldTime() {
		yield return Level1();
		PlayerHealthBar bar = Object.FindObjectOfType<PlayerHealthBar>();
		Assert.IsFalse(bar.shieldGroup.activeSelf);
		player.GetComponent<ShieldEffect>().Activate();
		yield return null;
		Assert.IsTrue(bar.shieldGroup.activeSelf);
		Assert.AreEqual("5", bar.shieldSeconds.text);
	}

	[UnityTest]
	public IEnumerator RetryClearsTheShield() {
		yield return Level1();
		player.GetComponent<ShieldEffect>().Activate();
		SceneRouter.Retry();
		yield return null;
		yield return null;
		ShieldEffect fresh = GameObject.FindWithTag("Player").GetComponent<ShieldEffect>();
		Assert.IsFalse(fresh.Active);
		Assert.IsFalse(fresh.Bubble.activeSelf);
	}

	// A creep strike on a shielded robot is blocked and spent: no HP lost, and it does not land later.
	[UnityTest]
	public IEnumerator ShieldBlocksAndTheStrikeIsSpent() {
#if UNITY_EDITOR
		EditorSceneManager.LoadSceneInPlayMode("Assets/Tests/PlayMode/EnemyArena.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
		yield return null;
		yield return null;
		GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
		floor.transform.position = new Vector3(0f, -0.5f, 0f);
		floor.transform.localScale = new Vector3(60f, 1f, 60f);
		NavMeshDataInstance nav = NavMesh.AddNavMeshData(NavMeshBake.Build(new Bounds(Vector3.zero, new Vector3(64f, 10f, 64f))));
		try {
#if UNITY_EDITOR
			player = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"), new Vector3(0f, 0.5f, 0f), Quaternion.identity);
			GameObject enemy = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy - Crater.prefab"), new Vector3(0f, 0f, 1.1f), Quaternion.LookRotation(Vector3.back));
#else
			GameObject enemy = null;
#endif
			player.tag = "Player";
			Object.Destroy(player.GetComponent<BallUserControl>());
			player.GetComponent<Rigidbody>().isKinematic = true;
			health = player.GetComponent<Health>();
			int blocked = 0;
			health.Blocked += () => blocked++;
			player.GetComponent<ShieldEffect>().Activate();
			EnemyBrain creep = enemy.GetComponent<EnemyBrain>();
			creep.target = player.transform;
			float end = Time.time + 3f;
			while (creep.State != EnemyState.Recover && Time.time < end)
				yield return null;
			Assert.AreEqual(EnemyState.Recover, creep.State);
			Assert.AreEqual(1, blocked, "the strike hit the shield");
			Assert.AreEqual(1, creep.HitAttempts, "and was spent");
			Assert.AreEqual(100f, health.healthPoints);
		} finally {
			nav.Remove();
		}
	}

	SupportSpawnDirector support;

	IEnumerator Load(string level) {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene(level);
		yield return null;
		yield return null;
		player = GameObject.FindWithTag("Player");
		health = player.GetComponent<Health>();
		health.numberOfLives = 999;
		Object.FindObjectOfType<EnemyDirector>().enabled = false;
		support = Object.FindObjectOfType<SupportSpawnDirector>();
	}

	[UnityTest]
	public IEnumerator FirstShieldFiveSecondsIntoLevel1() {
		yield return Load("Level1");
		float start = Time.time;
		SupportPickup first = null;
		while (first == null && Time.time - start < 8f) {
			first = Object.FindObjectOfType<SupportPickup>();
			yield return null;
		}
		Assert.IsNotNull(first);
		Assert.AreEqual(SupportKind.Shield, first.kind);
		Assert.That(Time.time - start, Is.InRange(5f, 5f + 0.85f + 0.6f), "5 s after the intro, after its marker and fall");
		while (support.Items.Count == 0 && Time.time - start < 9f)
			yield return null;   // landed
		Assert.AreEqual(1, support.Items.Count);
		Vector3 view = Camera.main.WorldToViewportPoint(support.Items[0].transform.position);
		Assert.IsTrue(view.z > 0f && view.x > 0f && view.x < 1f && view.y > 0f && view.y < 1f, "on screen for the robot's camera");
	}

	[UnityTest]
	public IEnumerator SupportCapHolds() {
		yield return Load("Level4");
		support.intervalOverride = 0.2f;
		int most = 0;
		float end = Time.time + 4f;
		while (Time.time < end) {
			most = Mathf.Max(most, support.AliveCount);
			Assert.LessOrEqual(support.AliveCount, 3);
			yield return null;
		}
		Assert.AreEqual(3, most);
	}

	[UnityTest]
	public IEnumerator LowHpBringsAHeal20() {
		yield return Load("Level2");
		yield return new WaitForSeconds(1.2f);
		health.TakeDamage(70f, DamageKind.EnemyAttack);
		support.intervalOverride = 0.2f;
		SupportPickup first = null;
		float end = Time.time + 3f;
		while (first == null && Time.time < end) {
			first = Object.FindObjectOfType<SupportPickup>();
			yield return null;
		}
		Assert.IsNotNull(first);
		Assert.AreEqual(SupportKind.Heal20, first.kind, "HP <= 40 and no heal nearby");
	}

	void PinRobot() {
		Rigidbody body = player.GetComponent<Rigidbody>();
		body.isKinematic = true;
	}

	[UnityTest]
	public IEnumerator ItemsExpireAfterTheirLifetime() {
		yield return Load("Level2");
		PinRobot();   // items land >= 3 m away, so only expiry can remove them
		support.lifetimeOverride = 1f;
		support.intervalOverride = 0.2f;
		SupportPickup item = null;
		while (item == null) {
			item = Object.FindObjectOfType<SupportPickup>();
			yield return null;
		}
		support.intervalOverride = 100f;
		int gone = 0;
		item.Gone += _ => gone++;
		yield return new WaitForSeconds(1.3f);
		Assert.IsTrue(item == null, "expired");
		Assert.AreEqual(1, gone);
	}

	[UnityTest]
	public IEnumerator ExpiryFreesTheSlotOnce() {
		yield return Load("Level2");
		support.intervalOverride = 0.2f;
		PinRobot();
		while (support.Items.Count == 0)
			yield return null;
		SupportPickup item = support.Items[0];   // a landed item, not a falling one
		support.intervalOverride = 100f;
		yield return null;
		int before = support.AliveCount, gone = 0;
		item.Gone += _ => gone++;
		item.Finish();
		item.Finish();
		yield return null;
		Assert.AreEqual(1, gone);
		Assert.AreEqual(before - 1, support.AliveCount);
	}

	[UnityTest]
	public IEnumerator SupportNeverOnCores() {
		yield return Load("Level4");
		support.intervalOverride = 0.2f;
		Object.FindObjectOfType<EnergySpawnDirector>().intervalOverride = 0.2f;
		float end = Time.time + 4f;
		while (Time.time < end) {
			foreach (SupportPickup item in Object.FindObjectsOfType<SupportPickup>())
				foreach (Treasure core in Object.FindObjectsOfType<Treasure>())
					Assert.GreaterOrEqual(Vector3.Distance(item.transform.position, core.transform.position), 1.9f);
			yield return null;
		}
	}

	[UnityTest]
	public IEnumerator OverdriveOnlyFromLevel2() {
		yield return Load("Level1");
		support.intervalOverride = 0.1f;
		support.lifetimeOverride = 0.15f;   // keep slots free so many items roll
		int seen = 0;
		var counted = new System.Collections.Generic.HashSet<SupportPickup>();
		float end = Time.time + 4f;
		while (Time.time < end) {
			foreach (SupportPickup p in Object.FindObjectsOfType<SupportPickup>())
				if (counted.Add(p))
					seen++;
			Assert.IsFalse(Object.FindObjectsOfType<SupportPickup>().Any(p => p.kind == SupportKind.Overdrive), "no Overdrive in Level1");
			yield return null;
		}
		Assert.Greater(seen, 3, "items did spawn");
	}

	// HP <= 40 with the budget full of non-heal items: one far item is swapped for a +20.
	[UnityTest]
	public IEnumerator LowHpSwapsAFarItemForAHeal20() {
		yield return Load("Level2");
		PinRobot();
		support.weightsOverride = new[] { 1f, 0f, 0f, 0f };   // shields only
		support.intervalOverride = 0.2f;
		while (support.Items.Count < 2)
			yield return null;
		health.TakeDamage(70f, DamageKind.EnemyAttack);
		float end = Time.time + 3f;
		while (Time.time < end && !support.Items.Any(i => i.kind == SupportKind.Heal20))
			yield return null;
		Assert.AreEqual(1, support.Items.Count(i => i.kind == SupportKind.Heal20), "one far shield became a +20");
		Assert.LessOrEqual(support.AliveCount, 2, "within the budget");
	}
}
