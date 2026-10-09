using System.Collections;
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
}
