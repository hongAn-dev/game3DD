using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnemyDirectorPlayTests {

	EnemyDirector director;
	Transform player;

	IEnumerator Load(string level, GameSettings.gameDifficulties difficulty) {
		CampaignProgress.BeginRun(difficulty);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene(level);
		yield return null;
		yield return null;
		director = Object.FindObjectOfType<EnemyDirector>();
		player = GameObject.FindWithTag("Player").transform;
		player.GetComponent<Health>().numberOfLives = 999;   // hits must not end the level during the test
	}

	static bool IsBoss(EnemyBrain brain) {
		return brain.name.Contains("Monster");
	}

	[UnityTest]
	public IEnumerator EnemyCapRespectedOnHard() {
		yield return Load("Level4", GameSettings.gameDifficulties.Hard);
		director.quietTime = 0f;
		director.bossAfterTime = 0f;
		director.intervalOverride = 0.1f;
		LevelConfig config = LevelCatalog.Get("Level4");
		int cap = config.enemyCap[2], bossCap = config.bossCap[2], max = 0, maxBoss = 0;
		float end = Time.time + 6f;
		while (Time.time < end) {
			EnemyBrain[] all = Object.FindObjectsOfType<EnemyBrain>();
			max = Mathf.Max(max, Mathf.Max(director.AliveCount, all.Length));
			maxBoss = Mathf.Max(maxBoss, all.Count(IsBoss));
			yield return null;
		}
		Assert.LessOrEqual(max, cap);
		Assert.LessOrEqual(maxBoss, bossCap);
		Assert.AreEqual(cap, max, "fills up to the cap");
		Assert.AreEqual(bossCap, maxBoss);
	}

	[UnityTest]
	public IEnumerator SpawnsAwayFromRobot() {
		yield return Load("Level2", GameSettings.gameDifficulties.Hard);
		director.quietTime = 0f;
		director.intervalOverride = 0.1f;
		var seen = new HashSet<EnemyBrain>();
		float end = Time.time + 4f;
		while (Time.time < end) {
			foreach (EnemyBrain brain in Object.FindObjectsOfType<EnemyBrain>())
				if (seen.Add(brain))
					Assert.GreaterOrEqual(Vector3.Distance(brain.transform.position, player.position), director.minPlayerDistance, brain.name);
			yield return null;
		}
		Assert.Greater(seen.Count, 0);
	}

	[UnityTest]
	public IEnumerator NoSpawnInFirstSeconds() {
		yield return Load("Level2", GameSettings.gameDifficulties.Hard);
		director.intervalOverride = 0.1f;
		float end = Time.time + director.quietTime - 0.3f;
		while (Time.time < end) {
			Assert.IsEmpty(Object.FindObjectsOfType<EnemyBrain>());
			Assert.AreEqual(0, director.AliveCount);
			yield return null;
		}
	}

}
