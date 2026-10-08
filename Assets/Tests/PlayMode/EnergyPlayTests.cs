using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnergyPlayTests {

	EnergySpawnDirector director;

	IEnumerator Load(string level, GameSettings.gameDifficulties difficulty) {
		CampaignProgress.BeginRun(difficulty);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene(level);
		yield return null;
		yield return null;
		director = Object.FindObjectOfType<EnergySpawnDirector>();
	}

	int Alive() {
		return director.AliveCount;
	}

	[UnityTest]
	public IEnumerator StartsWithConfiguredCores() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		Assert.AreEqual(LevelCatalog.Get("Level1").energyAtStart, Object.FindObjectsOfType<Treasure>().Length);
	}

	[UnityTest]
	public IEnumerator NeverExceedsCap() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		director.intervalOverride = 0.2f;
		int cap = LevelCatalog.Get("Level1").energyCap, max = 0;
		float end = Time.time + 3f;
		while (Time.time < end) {
			max = Mathf.Max(max, Alive());
			yield return null;
		}
		Assert.LessOrEqual(max, cap);
		Assert.AreEqual(cap, max, "fills up to the cap");
	}

	[UnityTest]
	public IEnumerator DirectorDoesNotSpawnWhilePaused() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		director.intervalOverride = 0.1f;
		int before = Alive();
		GameFlow.Pause();
		yield return new WaitForSecondsRealtime(1f);
		int during = Alive();
		GameFlow.Resume();
		Assert.AreEqual(before, during);
	}

	[UnityTest]
	public IEnumerator DestroyedCoreFreesSlot() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		director.intervalOverride = 0.2f;
		yield return new WaitForSeconds(3f);
		int cap = Alive();
		Object.Destroy(Object.FindObjectsOfType<Treasure>().First().gameObject);
		yield return null;
		Assert.AreEqual(cap - 1, Alive());
		yield return new WaitForSeconds(2f);
		Assert.AreEqual(cap, Alive());
	}

	[UnityTest]
	public IEnumerator RefillsWhenNoCoreFor8s() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		director.intervalOverride = 1000f;
		director.fallbackDelay = 0.5f;
		foreach (Treasure t in Object.FindObjectsOfType<Treasure>())
			Object.Destroy(t.gameObject);
		yield return new WaitForSeconds(2.5f);
		Assert.GreaterOrEqual(Object.FindObjectsOfType<Treasure>().Length, 1);
	}

	[UnityTest]
	public IEnumerator AtLastCoreOnlyOneIsAlive() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		director.intervalOverride = 0.2f;
		// Cores already on the map stay; with one core still needed no more than one is supplied.
		foreach (Treasure t in Object.FindObjectsOfType<Treasure>())
			Object.Destroy(t.gameObject);
		GameManager.gm.Collect(GameManager.gm.BeatLevelScore - 1);
		yield return new WaitForSeconds(2f);
		Assert.AreEqual(1, Object.FindObjectsOfType<Treasure>().Length);
	}

	[UnityTest]
	public IEnumerator HardDifficultyStillSpawnsEnergy() {
		yield return Load("Level2", GameSettings.gameDifficulties.Hard);
		director.intervalOverride = 0.2f;
		foreach (Treasure t in Object.FindObjectsOfType<Treasure>())
			Object.Destroy(t.gameObject);
		yield return new WaitForSeconds(2.5f);
		Assert.GreaterOrEqual(Object.FindObjectsOfType<Treasure>().Length, 1);
	}

	static float NearestCoreToPlayer() {
		Vector3 player = GameObject.FindWithTag("Player").transform.position;
		return Object.FindObjectsOfType<Treasure>().Select(t => Vector3.Distance(t.transform.position, player)).DefaultIfEmpty(float.MaxValue).Min();
	}

	[UnityTest]
	public IEnumerator FirstCoreIsCloseToTheStart() {
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" }) {
			yield return Load(level, GameSettings.gameDifficulties.Normal);
			Assert.LessOrEqual(NearestCoreToPlayer(), 25f, level);
		}
	}

	[UnityTest]
	public IEnumerator StaleCoreIsReplacedNearThePlayer() {
		yield return Load("Level4", GameSettings.gameDifficulties.Normal);
		director.intervalOverride = 1000f;
		director.staleDelay = 1f;
		// Leave a single core, far away (stands in for one the robot cannot reach).
		Vector3 player = GameObject.FindWithTag("Player").transform.position;
		Treasure[] cores = Object.FindObjectsOfType<Treasure>().OrderBy(t => Vector3.Distance(t.transform.position, player)).ToArray();
		for (int i = 0; i < cores.Length - 1; i++)
			Object.Destroy(cores[i].gameObject);
		Treasure far = cores[cores.Length - 1];
		GameManager.gm.Collect(GameManager.gm.BeatLevelScore - 1);
		yield return new WaitForSeconds(3f);
		Assert.IsTrue(far == null, "the stale core is removed");
		Assert.LessOrEqual(NearestCoreToPlayer(), 25f);
		Assert.AreEqual(1, Object.FindObjectsOfType<Treasure>().Length);
	}
}
