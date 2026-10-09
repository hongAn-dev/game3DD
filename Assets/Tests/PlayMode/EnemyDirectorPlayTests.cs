using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
		director.bossGateOverride = true;
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

	// Holds the robot still on the ground (the start point is 4 m up; an airborne robot has no NavMesh point).
	void PinRobotOnGround() {
		Rigidbody body = player.GetComponent<Rigidbody>();
		float ground = float.MinValue;
		foreach (RaycastHit hit in Physics.RaycastAll(player.position + Vector3.up, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore))
			if (hit.collider.attachedRigidbody == null)   // not the robot itself
				ground = Mathf.Max(ground, hit.point.y);
		body.isKinematic = true;
		body.position = new Vector3(player.position.x, ground + 0.5f, player.position.z);
		player.position = body.position;
	}

	static bool Chasing(EnemyBrain b) {
		return b.State == EnemyState.Chase || b.State == EnemyState.Windup || b.State == EnemyState.Strike || b.State == EnemyState.Recover;
	}

	[UnityTest]
	public IEnumerator Level1HasOneCreepAndNoBoss() {
		yield return Load("Level1", GameSettings.gameDifficulties.Hard);
		director.quietTime = 0f;
		director.intervalOverride = 0.1f;
		director.bossGateOverride = true;
		int max = 0;
		float end = Time.time + 5f;
		while (Time.time < end) {
			max = Mathf.Max(max, director.AliveCount);
			Assert.IsFalse(Object.FindObjectsOfType<EnemyBrain>().Any(IsBoss), "no boss in Level1");
			yield return null;
		}
		Assert.AreEqual(1, max, "exactly one creep at a time");
	}

	[UnityTest]
	public IEnumerator Level2NeverSpawnsABoss() {
		yield return Load("Level2", GameSettings.gameDifficulties.Hard);
		director.quietTime = 0f;
		director.intervalOverride = 0.1f;
		director.bossGateOverride = true;
		float end = Time.time + 4f;
		while (Time.time < end) {
			Assert.IsFalse(Object.FindObjectsOfType<EnemyBrain>().Any(IsBoss), "no boss in Level2");
			Assert.LessOrEqual(director.AliveCount, 4);
			yield return null;
		}
	}

	[UnityTest]
	public IEnumerator QuietTimeComesFromTheLevel() {
		yield return Load("Level1", GameSettings.gameDifficulties.Normal);
		Assert.AreEqual(8f, director.quietTime, 0.001f);
		yield return Load("Level3", GameSettings.gameDifficulties.Normal);
		Assert.AreEqual(5f, director.quietTime, 0.001f);
	}

	// After the quiet time, enemies come every 2-3 s until the cap.
	[UnityTest]
	public IEnumerator SpawnsAreTwoToThreeSecondsApart() {
		yield return Load("Level4", GameSettings.gameDifficulties.Normal);
		director.quietTime = 0f;
		PinRobotOnGround();
		var starts = new List<float>();
		var seen = new HashSet<GameObject>();
		float end = Time.time + 8f;
		while (Time.time < end && starts.Count < 4) {
			foreach (GameObject marker in Object.FindObjectsOfType<GameObject>().Where(g => g.name == "Enemy Spawn Marker"))
				if (seen.Add(marker))
					starts.Add(Time.time);
			yield return null;
		}
		Assert.GreaterOrEqual(starts.Count, 3);
		for (int i = 1; i < starts.Count; i++)
			Assert.That(starts[i] - starts[i - 1], Is.InRange(1.95f, 3.1f), "gap " + i);
	}

	[UnityTest]
	public IEnumerator SpawnCancelledWhenRobotReachesTheMarker() {
		yield return Load("Level2", GameSettings.gameDifficulties.Hard);
		director.quietTime = 0f;
		director.intervalOverride = 0.1f;
		PinRobotOnGround();
		Rigidbody body = player.GetComponent<Rigidbody>();
		GameObject marker = null;
		float end = Time.time + 3f;
		while (marker == null && Time.time < end) {
			marker = Object.FindObjectsOfType<GameObject>().FirstOrDefault(g => g.name == "Enemy Spawn Marker");
			yield return null;
		}
		Assert.IsNotNull(marker);
		Vector3 point = marker.transform.position;
		director.intervalOverride = 100f;   // no other spawn during the check
		body.position = point + Vector3.up * 0.5f;
		player.position = body.position;
		yield return new WaitForSeconds(director.telegraphTime + 0.3f);
		Assert.IsFalse(Object.FindObjectsOfType<EnemyBrain>().Any(b => Vector3.Distance(b.transform.position, point) < 3f), "nothing appeared under the robot");
	}

	[UnityTest]
	public IEnumerator SpawnsHaveAPathToTheRobot() {
		yield return Load("Level3", GameSettings.gameDifficulties.Hard);
		director.quietTime = 0f;
		director.intervalOverride = 0.1f;
		PinRobotOnGround();
		NavMeshHit robot;
		Assert.IsTrue(NavMesh.SamplePosition(player.position, out robot, 3f, NavMesh.AllAreas));
		var seen = new HashSet<EnemyBrain>();
		float end = Time.time + 4f;
		while (Time.time < end) {
			foreach (EnemyBrain brain in Object.FindObjectsOfType<EnemyBrain>())
				if (seen.Add(brain)) {
					Assert.GreaterOrEqual(Vector3.Distance(brain.transform.position, player.position), 8f, brain.name);
					var path = new NavMeshPath();
					NavMesh.CalculatePath(brain.transform.position, robot.position, NavMesh.AllAreas, path);
					Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, brain.name + " cannot reach the robot");
				}
			yield return null;
		}
		Assert.Greater(seen.Count, 0);
	}

	[UnityTest]
	public IEnumerator ChaseLimitHolds() {
		yield return Load("Level4", GameSettings.gameDifficulties.Hard);
		director.quietTime = 0f;
		director.intervalOverride = 0.1f;
		director.bossGateOverride = true;
		float end = Time.time + 8f;
		while (Time.time < end) {
			Assert.LessOrEqual(director.Chasing, 3);
			Assert.LessOrEqual(Object.FindObjectsOfType<EnemyBrain>().Count(Chasing), 3, "at most three chase at once in Level4");
			yield return null;
		}
	}
}
