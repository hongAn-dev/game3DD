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
}
