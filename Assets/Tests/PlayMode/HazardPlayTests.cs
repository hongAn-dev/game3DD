using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class HazardPlayTests {

	int deaths;

	void CountDeaths(FlowState state) {
		if (state == FlowState.Dead)
			deaths++;
	}

	IEnumerator Load(string level) {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene(level);
		yield return null;
		yield return null;
		deaths = 0;
		GameFlow.Changed += CountDeaths;
	}

	[TearDown]
	public void Unhook() {
		GameFlow.Changed -= CountDeaths;
	}

	static void Place(Vector3 position) {
		GameObject player = GameObject.FindWithTag("Player");
		Rigidbody body = player.GetComponent<Rigidbody>();
		body.velocity = Vector3.zero;
		body.position = position;
		player.transform.position = position;
	}

	// A point over open sea: far beyond every collider of the map, just above the sea surface.
	static Vector3 OverTheSea() {
		Bounds sea = Object.FindObjectsOfType<HazardVolume>().First(h => h.name == "DeathZone").GetComponent<Collider>().bounds;
		Bounds land = new Bounds(GameObject.FindWithTag("Player").transform.position, Vector3.zero);
		foreach (Collider c in Object.FindObjectsOfType<Collider>().Where(c => !c.isTrigger && c.attachedRigidbody == null))
			land.Encapsulate(c.bounds);
		return new Vector3(land.max.x + 5f, sea.max.y + 1f, land.center.z);
	}

	IEnumerator ExpectHazardDeath(string level) {
		yield return new WaitForSeconds(2f);
		Assert.AreEqual(FlowState.Dead, GameFlow.State, level);
		Assert.AreEqual(1, deaths, level + ": dies exactly once");
		Assert.AreEqual(LevelCatalog.Get(level).hazardDeathMessage, GameFlow.DeathCause, level);
	}

	[UnityTest]
	public IEnumerator AcidKillsOnceWithCause() {
		yield return Load("Level1");
		Place(OverTheSea());
		yield return ExpectHazardDeath("Level1");
	}

	[UnityTest]
	public IEnumerator LavaKillsWithLavaCause() {
		yield return Load("Level3");
		Place(OverTheSea());
		yield return ExpectHazardDeath("Level3");
		StringAssert.Contains("dung nham", GameFlow.DeathCause);
	}

	[UnityTest]
	public IEnumerator KillPlaneCatchesFallThrough() {
		yield return Load("Level2");
		Bounds kill = GameObject.Find("Kill Plane").GetComponent<Collider>().bounds;
		Place(new Vector3(kill.center.x, kill.max.y + 3f, kill.center.z));
		yield return ExpectHazardDeath("Level2");
	}
}
