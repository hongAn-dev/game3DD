using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameplayTests {

	[UnitySetUp]
	public IEnumerator LoadLevel1() {
		GameSettings.difficulty = GameSettings.gameDifficulties.Easy;
		GameSettings.showIntroLevelMessage = false;
		Time.timeScale = 1;
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;
	}

	[UnityTest]
	public IEnumerator CollectingEnergyCoreIncreasesScore() {
		GameObject player = GameObject.FindWithTag("Player");
		Treasure core = Object.FindObjectOfType<Treasure>();
		int expected = GameManager.gm.score + core.value;

		player.GetComponent<Rigidbody>().position = core.transform.position;
		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();

		Assert.AreEqual(expected, GameManager.gm.score);
	}

	[UnityTest]
	public IEnumerator PatrolRobotKillsPlayerOnContact() {
#if UNITY_EDITOR
		GameObject player = GameObject.FindWithTag("Player");
		Health health = player.GetComponent<Health>();
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy - Monster.prefab");
		Object.Instantiate(prefab, player.transform.position + new Vector3(3, 1, 0), Quaternion.identity);

		float end = Time.time + 6f;
		while (Time.time < end && health != null && health.isAlive)
			yield return null;

		Assert.IsTrue(health == null || !health.isAlive, "Player should die when the patrol robot reaches it");
#else
		yield return null;
#endif
	}

	[UnityTest]
	public IEnumerator ScoreDisplayIsResetAfterReload() {
		GameManager.gm.Collect(3);
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;

		Assert.AreEqual(0, GameManager.gm.score);
		StringAssert.StartsWith("0", GameManager.gm.mainScoreDisplay.text);
	}

	[UnityTest]
	public IEnumerator PlayerUsesRobotBall() {
		yield return null;
		GameObject player = GameObject.FindWithTag("Player");
		Assert.AreEqual("RoboBall", player.GetComponent<MeshFilter>().sharedMesh.name);
		Assert.IsNotNull(player.GetComponent<TrailRenderer>());
	}

	[UnityTest]
	public IEnumerator CoinsUseEnergyCore() {
		yield return null;
		Treasure core = Object.FindObjectOfType<Treasure>();
		MeshFilter filter = core.GetComponentInChildren<MeshFilter>();
		Assert.AreEqual("EnergyCore", filter.sharedMesh.name);
	}

	[Test]
	public void PatrolRobotUsesRobotClips() {
#if UNITY_EDITOR
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy - Monster.prefab");
		MonsterChaser chaser = prefab.GetComponent<MonsterChaser>();
		Assert.AreEqual("Run", chaser.runClip);
		Assert.AreEqual("Attack", chaser.attackClip);
		Assert.IsNotNull(prefab.GetComponentInChildren<Animation>().GetClip("Attack"));
#endif
	}
}
