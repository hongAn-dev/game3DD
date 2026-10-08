using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class FlowPlayTests {

	[UnitySetUp]
	public IEnumerator LoadLevel1() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Easy);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;
	}

	[UnityTest]
	public IEnumerator PickupCountsOnceWithTwoPlayerColliders() {
		GameObject player = GameObject.FindWithTag("Player");
		SphereCollider extra = player.AddComponent<SphereCollider>();
		extra.radius = 0.45f;
		Treasure core = Object.FindObjectOfType<Treasure>();
		int before = GameManager.gm.score;

		player.GetComponent<Rigidbody>().position = core.transform.position;
		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();

		Assert.AreEqual(before + 1, GameManager.gm.score);
	}

	[UnityTest]
	public IEnumerator NoPickupAfterDeath() {
		GameObject player = GameObject.FindWithTag("Player");
		Treasure core = Object.FindObjectOfType<Treasure>();
		int before = GameManager.gm.score;
		GameFlow.Die("test");

		player.GetComponent<Rigidbody>().position = core.transform.position;
		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();

		Assert.AreEqual(before, GameManager.gm.score);
	}
	[UnityTest]
	public IEnumerator DeathWinsOverLastCoreInSameFrame() {
		GameManager gm = GameManager.gm;
		gm.Collect(gm.BeatLevelScore - 1);
		GameObject.FindWithTag("Player").GetComponent<Health>().ApplyDamage(10f);
		gm.Collect(1);
		yield return null;
		yield return null;

		Assert.AreEqual(FlowState.Dead, GameFlow.State);
		Assert.IsFalse(CampaignProgress.IsCompleted("Level1"));
	}

	[UnityTest]
	public IEnumerator CampaignRouteReachesEndingOnce() {
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" }) {
			Assert.AreEqual(level, SceneManager.GetActiveScene().name);
			// Next() shows the intro card (FlowState.Intro, time frozen); pickups count only once it is over.
			float playing = Time.realtimeSinceStartup + 4f;
			while (GameFlow.State != FlowState.Playing && Time.realtimeSinceStartup < playing)
				yield return null;
			GameManager.gm.Collect(GameManager.gm.BeatLevelScore);
			yield return null;
			yield return null;
			Assert.IsTrue(CampaignProgress.IsCompleted(level));
			if (level != "Level4") {
				SceneRouter.Next();
				yield return null;
				yield return null;
			}
		}
		float end = Time.realtimeSinceStartup + 5f;
		while (SceneManager.GetActiveScene().name != SceneRouter.EndingScene && Time.realtimeSinceStartup < end)
			yield return null;
		Assert.AreEqual(SceneRouter.EndingScene, SceneManager.GetActiveScene().name);
		Assert.AreEqual(100, CampaignProgress.FuelPercent);
		Assert.IsTrue(Object.FindObjectOfType<EndingController>().completionPanel.activeSelf);
	}

	[UnityTest]
	public IEnumerator DirectLevel4WinWithoutCampaignGoesToMenu() {
		CampaignProgress.Reset();
		SceneManager.LoadScene("Level4");
		yield return null;
		yield return null;
		GameManager.gm.Collect(GameManager.gm.BeatLevelScore);
		float end = Time.realtimeSinceStartup + 5f;
		while (SceneManager.GetActiveScene().name == "Level4" && Time.realtimeSinceStartup < end)
			yield return null;
		Assert.AreEqual(SceneRouter.MainMenuScene, SceneManager.GetActiveScene().name);
	}

	[UnityTest]
	public IEnumerator EndingOffersPlayAgainAndMenuButNoNextLevel() {
		SceneManager.LoadScene(SceneRouter.EndingScene);
		yield return null;
		yield return null;
		var labels = new System.Collections.Generic.List<string>();
		foreach (UnityEngine.UI.Button b in Object.FindObjectsOfType<UnityEngine.UI.Button>())
			labels.Add(b.GetComponentInChildren<UnityEngine.UI.Text>().text);
		CollectionAssert.AreEquivalent(new[] { "CHƠI LẠI TỪ ĐẦU", "VỀ MENU" }, labels);
		EndingController ending = Object.FindObjectOfType<EndingController>();
		ending.Complete();
		ending.Complete();
		Assert.IsTrue(ending.completionPanel.activeSelf);
	}
}
