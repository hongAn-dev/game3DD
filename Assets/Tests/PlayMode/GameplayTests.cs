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
		EnemyBrain brain = prefab.GetComponent<EnemyBrain>();
		Assert.AreEqual("Run", brain.runClip);
		Assert.AreEqual("Attack", brain.attackClip);
		Assert.AreEqual("Idle", brain.idleClip);
		Assert.IsNotNull(prefab.GetComponentInChildren<Animation>().GetClip("Attack"));
#endif
	}

	[Test]
	public void RobotLightBrightnessFollowsEnergy() {
		Assert.AreEqual(0.3f, RobotLights.Brightness(0, 10), 0.001f);
		Assert.AreEqual(0.65f, RobotLights.Brightness(5, 10), 0.001f);
		Assert.AreEqual(1f, RobotLights.Brightness(15, 10), 0.001f);
		Assert.AreEqual(1f, RobotLights.Brightness(0, 0), 0.001f);
	}

	[UnityTest]
	public IEnumerator PlayerHasRobotLights() {
		yield return null;
		Assert.IsNotNull(GameObject.FindWithTag("Player").GetComponent<RobotLights>());
	}

	[UnityTest]
	public IEnumerator ScoreShowsCollectedOverTarget() {
		yield return null;
		GameManager gm = GameManager.gm;
		Assert.AreEqual(6, gm.BeatLevelScore);
		Assert.AreEqual("0 / 6", gm.mainScoreDisplay.text);
	}

	[Test]
	public void ZoneTitlesAreVietnamese() {
		Assert.AreEqual("Bãi đáp hỏng", LevelCatalog.Get("Level1").displayName);
		Assert.AreEqual("Trạm khai thác bỏ hoang", LevelCatalog.Get("Level2").displayName);
		Assert.AreEqual("Vùng địa nhiệt", LevelCatalog.Get("Level3").displayName);
		Assert.AreEqual("Bãi phóng cũ", LevelCatalog.Get("Level4").displayName);
		Assert.IsNull(LevelCatalog.Get("MainMenu"));
	}
}
