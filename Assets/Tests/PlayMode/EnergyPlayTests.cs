using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnergyPlayTests {

	EnergySpawnDirector director;
	Transform player;

	IEnumerator Load(string level, GameSettings.gameDifficulties difficulty) {
		CampaignProgress.BeginRun(difficulty);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene(level);
		yield return null;
		yield return null;
		director = Object.FindObjectOfType<EnergySpawnDirector>();
		player = GameObject.FindWithTag("Player").transform;
		player.GetComponent<Health>().numberOfLives = 999;
		EnemyDirector enemies = Object.FindObjectOfType<EnemyDirector>();
		if (enemies != null)
			enemies.enabled = false;
	}

	[TearDown]
	public void Reset() {
		GameFlow.ResetForScene();
	}

	void ClearCores() {
		foreach (Treasure t in Object.FindObjectsOfType<Treasure>())
			Object.Destroy(t.gameObject);
	}

	// Path length from the robot's ground point to a position, as the director measures it.
	float PathFromRobot(Vector3 to) {
		float ground = float.MinValue;
		foreach (RaycastHit hit in Physics.RaycastAll(player.position + Vector3.up, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
			if (hit.collider.attachedRigidbody == null)
				ground = Mathf.Max(ground, hit.point.y);
		NavMeshHit from, target;
		if (!NavMesh.SamplePosition(new Vector3(player.position.x, ground, player.position.z), out from, 3f, NavMesh.AllAreas)
			|| !NavMesh.SamplePosition(to, out target, 1.5f, NavMesh.AllAreas))
			return float.MaxValue;
		var path = new NavMeshPath();
		NavMesh.CalculatePath(from.position, target.position, NavMesh.AllAreas, path);
		return path.status == NavMeshPathStatus.PathComplete ? EnergySpawnDirector.PathLength(path) : float.MaxValue;
	}

	[UnityTest]
	public IEnumerator StartsWithConfiguredCores() {
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" }) {
			yield return Load(level, GameSettings.gameDifficulties.Easy);
			Assert.AreEqual(LevelCatalog.Get(level).energyAtStart, Object.FindObjectsOfType<Treasure>().Length, level);
		}
	}

	[UnityTest]
	public IEnumerator FirstCoreIsFourToTenMetresByPath() {
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" }) {
			yield return Load(level, GameSettings.gameDifficulties.Normal);
			Assert.That(PathFromRobot(director.FirstCorePoint), Is.InRange(3.5f, 10.5f), level + ": the first starting core");
		}
	}

	[UnityTest]
	public IEnumerator NeverExceedsCapWithBatches() {
		yield return Load("Level4", GameSettings.gameDifficulties.Normal);
		director.intervalOverride = 0.2f;
		int cap = Mathf.Min(LevelCatalog.Get("Level4").energyCap, EnergySpawnDirector.MaxAlive), max = 0;
		float end = Time.time + 4f;
		while (Time.time < end) {
			max = Mathf.Max(max, director.AliveCount);
			Assert.LessOrEqual(director.AliveCount, cap);
			yield return null;
		}
		Assert.AreEqual(cap, max, "fills up to the cap");
	}

	[UnityTest]
	public IEnumerator BatchesDropSeveralCores() {
		yield return Load("Level4", GameSettings.gameDifficulties.Normal);
		ClearCores();
		director.intervalOverride = 50f;
		yield return null;
		director.ForceBatch();
		yield return null;
		Assert.That(director.ReservedCount, Is.InRange(2, 4), "one L4 batch reserves 2-4 points in the same frame");
	}

	[UnityTest]
	public IEnumerator BatchCoresNeverShareAPoint() {
		yield return Load("Level4", GameSettings.gameDifficulties.Normal);
		ClearCores();
		director.intervalOverride = 0.1f;
		float end = Time.time + 3f;
		while (Time.time < end) {
			List<Vector3> taken = director.Occupied();
			for (int i = 0; i < taken.Count; i++)
				for (int j = i + 1; j < taken.Count; j++)
					Assert.GreaterOrEqual(Vector3.Distance(taken[i], taken[j]), 1.9f, "two cores on one point");
			yield return null;
		}
	}

	// Marker ~0.35 s then a 0.5 s fall; the trigger only turns on after landing. Batch test frames are long, so the
	// bounds allow a few frames of detection/overshoot (measured with the largest frame time seen).
	[UnityTest]
	public IEnumerator MarkerAndFallTiming() {
		yield return Load("Level1", GameSettings.gameDifficulties.Normal);
		ClearCores();
		director.intervalOverride = 0.1f;
		float marked = -1f, frame = 0f;
		while (marked < 0f) {
			if (Object.FindObjectsOfType<GameObject>().Any(g => g.name == "Energy Landing Marker"))
				marked = Time.time;
			yield return null;
		}
		Treasure falling = null;
		while (falling == null) {
			frame = Mathf.Max(frame, Time.deltaTime);
			falling = Object.FindObjectOfType<Treasure>();
			yield return null;
		}
		float appeared = Time.time - marked;
		Assert.IsFalse(falling.GetComponent<Collider>().enabled, "no pickup while falling");
		while (!falling.GetComponent<Collider>().enabled) {
			frame = Mathf.Max(frame, Time.deltaTime);
			yield return null;
		}
		float landed = Time.time - marked;
		Assert.That(appeared, Is.InRange(director.markerDuration - frame, director.markerDuration + 3f * frame), "marker ~0.35 s");
		Assert.That(landed, Is.InRange(director.markerDuration + director.dropDuration - frame,
			director.markerDuration + director.dropDuration + 4f * frame), "landed after 0.35 + 0.5 s");
		Assert.AreEqual(0.35f, director.markerDuration, 0.001f);
		Assert.AreEqual(0.5f, director.dropDuration, 0.001f);
	}

	[UnityTest]
	public IEnumerator WinCancelsFallingCores() {
		yield return Load("Level2", GameSettings.gameDifficulties.Normal);
		ClearCores();
		director.intervalOverride = 0.1f;
		while (director.ReservedCount == 0)
			yield return null;
		int score = GameManager.gm.score;
		int landed = Object.FindObjectsOfType<Treasure>().Count(t => t.GetComponent<Collider>().enabled);
		GameFlow.CompleteLevel();
		yield return new WaitForSeconds(1.5f);
		Assert.AreEqual(0, director.ReservedCount);
		Assert.IsFalse(Object.FindObjectsOfType<GameObject>().Any(g => g.name == "Energy Landing Marker"), "markers removed");
		Assert.AreEqual(landed, Object.FindObjectsOfType<Treasure>().Length, "nothing landed after the win");
		Assert.AreEqual(score, GameManager.gm.score);
	}

	[UnityTest]
	public IEnumerator DirectorDoesNotSpawnWhilePaused() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		director.intervalOverride = 0.1f;
		int before = director.AliveCount;
		GameFlow.Pause();
		yield return new WaitForSecondsRealtime(1f);
		int during = director.AliveCount;
		GameFlow.Resume();
		Assert.AreEqual(before, during);
	}

	[UnityTest]
	public IEnumerator DestroyedCoreFreesSlot() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		director.intervalOverride = 0.2f;
		yield return new WaitForSeconds(3f);
		int cap = director.AliveCount;
		director.intervalOverride = 100f;   // no refill in the same frame while checking the freed slot
		Object.Destroy(Object.FindObjectsOfType<Treasure>().First().gameObject);
		yield return null;
		Assert.AreEqual(cap - 1, director.AliveCount);
		director.intervalOverride = 0.2f;
		director.ForceBatch();
		yield return new WaitForSeconds(2f);
		Assert.AreEqual(cap, director.AliveCount);
	}

	// Spec §5.2: nothing alive or falling for 3 s while short → one core near the robot.
	[UnityTest]
	public IEnumerator FallbackWithinThreeSeconds() {
		yield return Load("Level1", GameSettings.gameDifficulties.Normal);
		director.intervalOverride = 1000f;
		yield return new WaitForSeconds(1.5f);   // robot has landed
		ClearCores();
		yield return new WaitForSeconds(EnergySpawnDirector.FallbackDelay + 0.35f + 0.5f + 0.3f);
		Treasure core = Object.FindObjectOfType<Treasure>();
		Assert.IsNotNull(core);
		Assert.LessOrEqual(PathFromRobot(core.transform.position), 18.5f, "near the robot");
	}

	[UnityTest]
	public IEnumerator AtLastCoreOnlyOneIsAlive() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		director.intervalOverride = 0.2f;
		ClearCores();
		GameManager.gm.Collect(GameManager.gm.BeatLevelScore - 1);
		yield return new WaitForSeconds(2f);
		Assert.AreEqual(1, Object.FindObjectsOfType<Treasure>().Length);
	}

	[UnityTest]
	public IEnumerator HardDifficultyStillSpawnsEnergy() {
		yield return Load("Level2", GameSettings.gameDifficulties.Hard);
		director.intervalOverride = 0.2f;
		ClearCores();
		yield return new WaitForSeconds(2.5f);
		Assert.GreaterOrEqual(Object.FindObjectsOfType<Treasure>().Length, 1);
	}

	// Spec §5.2: 12 s without a pickup and no reachable core near → one far core is replaced near the robot.
	[UnityTest]
	public IEnumerator StaleCoreReplacedNearTheRobot() {
		yield return Load("Level4", GameSettings.gameDifficulties.Normal);
		director.intervalOverride = 1000f;
		director.staleDelay = 1f;
		yield return new WaitForSeconds(1.5f);   // robot has landed
		Treasure[] cores = Object.FindObjectsOfType<Treasure>().OrderBy(t => PathFromRobot(t.transform.position)).ToArray();
		for (int i = 0; i < cores.Length - 1; i++)
			Object.Destroy(cores[i].gameObject);
		Treasure far = cores[cores.Length - 1];
		Assume.That(PathFromRobot(far.transform.position), Is.GreaterThan(18f), "the remaining core is far");
		GameManager.gm.Collect(GameManager.gm.BeatLevelScore - 1);
		yield return new WaitForSeconds(3f);
		Assert.IsTrue(far == null, "the far core is recalled");
		Treasure near = Object.FindObjectOfType<Treasure>();
		Assert.IsNotNull(near);
		Assert.LessOrEqual(PathFromRobot(near.transform.position), 18.5f);
		Assert.AreEqual(1, Object.FindObjectsOfType<Treasure>().Length);
	}

	[UnityTest]
	public IEnumerator DropsWhileRobotAirborne() {
		yield return Load("Level1", GameSettings.gameDifficulties.Normal);
		yield return new WaitForSeconds(1.5f);   // robot landed, its point is known
		ClearCores();
		Rigidbody body = player.GetComponent<Rigidbody>();
		body.isKinematic = true;
		body.position = player.position + Vector3.up * 20f;   // far above the ground
		player.position = body.position;
		director.intervalOverride = 0.1f;
		yield return new WaitForSeconds(1.5f);
		Assert.Greater(director.AliveCount, 0, "drops use the last valid robot point");
	}
}
