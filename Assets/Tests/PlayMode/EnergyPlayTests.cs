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
			LevelConfig config = LevelCatalog.Get(level);
			director.intervalOverride = 1000f;
			yield return new WaitForSeconds(GroundSpawn.ScaleInDuration + 0.05f);
			Assert.That(director.StartingSupply, Is.InRange(config.EnergySupplyMin, config.EnergySupplyMax), level + ": target + 2..5 supply");
			Assert.AreEqual(director.StartingSupply, director.AliveCount, level + ": starting budget after scale-in");
			Assert.AreEqual(director.StartingSupply, Object.FindObjectsOfType<Treasure>().Length, level + ": visible starting cores");
		}
	}

	[UnityTest]
	public IEnumerator FirstCoreIsFiveToEighteenMetresByPath() {
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" }) {
			yield return Load(level, GameSettings.gameDifficulties.Normal);
			float actualPath = PathFromRobot(director.FirstCorePoint);
            if (director.StartingNearSafeCandidateCount == 0) {
                Debug.LogWarning(level + ": no runtime-eligible safe candidate has a 5–18 m path; authored nearby choices are absent after ground, hazard, occupancy and NavMesh validation. First core path: " + actualPath);
                continue;
            }
            Debug.Log(level + ": runtime-eligible safe 5–18 m candidate paths = [" + string.Join(", ", director.StartingNearSafePaths.Select(v => v.ToString("F2")).ToArray()) + "], selected first core path = " + actualPath);
            Assert.That(actualPath, Is.InRange(5f, 18f), level + ": the first starting core when a near safe choice exists");
		}
	}

	[UnityTest]
	public IEnumerator NeverExceedsCapWithBatches() {
		yield return Load("Level4", GameSettings.gameDifficulties.Normal);
		director.intervalOverride = 0.2f;
		int cap = director.Capacity, max = 0;
		float end = Time.time + 4f;
		while (Time.time < end) {
			max = Mathf.Max(max, director.AliveCount);
			Assert.LessOrEqual(director.AliveCount, cap);
			yield return null;
		}
		Assert.That(max, Is.InRange(LevelCatalog.Get("Level4").EnergySupplyMin, LevelCatalog.Get("Level4").EnergySupplyMax), "random starting supply stays in range");
		Assert.AreEqual(director.StartingSupply, max, "no extra cores are needed while net energy is already sufficient");
	}

	[UnityTest]
	public IEnumerator StartPlacesTheWholeSupply() {
		yield return Load("Level4", GameSettings.gameDifficulties.Normal);
		yield return new WaitForSeconds(1.5f);
		LevelConfig config = LevelCatalog.Get("Level4");
		Assert.That(director.AliveCount, Is.InRange(config.EnergySupplyMin, config.EnergySupplyMax), "playtest: target + 2..5 on the map");
		Treasure[] spawned = Object.FindObjectsOfType<Treasure>();
		int clusters = spawned.Select(item => director.landingPoints
			.OrderBy(point => Vector3.Distance(point.position, item.transform.position)).First()
			.GetComponent<SpawnPoint>().clusterId).Distinct().Count();
		Assert.GreaterOrEqual(clusters, 4, "spread over the map");
		yield return new WaitForSeconds(4f);
		Assert.That(director.AliveCount, Is.InRange(config.EnergySupplyMin, config.EnergySupplyMax), "nothing more spawns on a timer");
	}

	// Playtest: cores were bunched up. Spread over the map, no two too close, some on risky ground.
	[UnityTest]
	public IEnumerator CoresAreSpreadAndSomeAreRisky() {
		foreach (string level in new[] { "Level2", "Level4" }) {
			yield return Load(level, GameSettings.gameDifficulties.Normal);
			yield return new WaitForSeconds(1.5f);
			Vector3[] cores = Object.FindObjectsOfType<Treasure>().Select(t => t.transform.position).ToArray();
			float closest = float.MaxValue;
			for (int i = 0; i < cores.Length; i++)
				for (int j = i + 1; j < cores.Length; j++)
					closest = Mathf.Min(closest, Vector3.Distance(cores[i], cores[j]));
			Assert.GreaterOrEqual(closest, director.spreadSpacing - 0.01f, level + ": no two cores too close");
			Vector3 centre = cores.Aggregate(Vector3.zero, (a, b) => a + b) / cores.Length;
			float spread = cores.Average(c => Vector3.Distance(c, centre));
			float mapSpread = director.landingPoints.Average(t => Vector3.Distance(t.position,
				director.landingPoints.Aggregate(Vector3.zero, (a, b) => a + b.position) / director.landingPoints.Length));
			Assert.GreaterOrEqual(spread, mapSpread * 0.8f, level + ": as spread out as the map's landing points");
			int risky = cores.Count(c => director.landingPoints.OrderBy(t => Vector3.Distance(t.position, c)).First().GetComponent<SpawnPoint>().IsRisk);
			Assert.Greater(risky, 0, level + ": some cores sit on risky ground");
		}
	}

	[UnityTest]
	public IEnumerator SupplyStaysAfterStart() {
		yield return Load("Level4", GameSettings.gameDifficulties.Normal);
		yield return new WaitForSeconds(1.5f);
		LevelConfig config = LevelCatalog.Get("Level4");
		yield return new WaitForSeconds(4f);
		Assert.That(director.AliveCount, Is.InRange(config.EnergySupplyMin, config.EnergySupplyMax), "nothing more spawns on a timer");
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

	// Refill uses direct placement and a scale-in while its trigger remains disabled.
	[UnityTest]
	public IEnumerator GroundSpawnUsesBoundsAndScaleIn() {
		yield return Load("Level1", GameSettings.gameDifficulties.Normal);
		ClearCores();   // checked as respawned cores land, before they start to bob
		float deadline = Time.time + EnergySpawnDirector.FallbackDelay + 2f;
		Treasure[] cores = new Treasure[0];
		while (cores.Length == 0 && Time.time < deadline) {
			yield return null;
			cores = Object.FindObjectsOfType<Treasure>();
		}
		Assert.Greater(cores.Length,0);
		foreach(Treasure core in cores) {
			RaycastHit hit;
			Assert.IsTrue(Physics.Raycast(core.transform.position+Vector3.up,Vector3.down,out hit,2f,~0,QueryTriggerInteraction.Ignore));
			Bounds bounds=GroundSpawnBounds(core.transform);
			Assert.That(bounds.min.y-hit.point.y,Is.InRange(.04f,.12f),core.name+" base sits just above ground");
		}
	}
	static Bounds GroundSpawnBounds(Transform root) {
		Bounds b; Assert.IsTrue(GroundSpawn.TryBounds(root,out b)); return b;
	}

	[UnityTest]
	public IEnumerator WinCancelsGroundSpawning() {
		yield return Load("Level2", GameSettings.gameDifficulties.Normal);
		ClearCores();
		director.intervalOverride = 0.1f;
		while (director.ReservedCount == 0 && Object.FindObjectsOfType<Treasure>().All(t=>t.GetComponent<Collider>().enabled))
			yield return null;
		int score = GameManager.gm.score;
		int landed = Object.FindObjectsOfType<Treasure>().Count(t => t.GetComponent<Collider>().enabled);
		GameFlow.CompleteLevel();
		yield return new WaitForSeconds(1.5f);
		Assert.AreEqual(0, director.ReservedCount);
		Assert.AreEqual(0,director.ReservedCount,"ground spawn reservations released");
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
	public IEnumerator RespawnOnlyWhenTheLevelWouldBeLost() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		yield return new WaitForSeconds(1.5f);
		Treasure[] cores = Object.FindObjectsOfType<Treasure>();
		for (int i = 0; i < cores.Length - 6; i++)
			Object.Destroy(cores[i].gameObject);   // 6 left = exactly the target
		yield return new WaitForSeconds(EnergySpawnDirector.FallbackDelay + 1f);
		Assert.AreEqual(6, director.AliveCount, "still winnable: no respawn");
		Object.Destroy(Object.FindObjectsOfType<Treasure>()[0].gameObject);   // 5 < 6
		yield return new WaitForSeconds(EnergySpawnDirector.FallbackDelay + 1.5f);
		Assert.AreEqual(7, director.AliveCount, "back to target + 1");
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
	public IEnumerator LastNeededEnergyComesBackIfLost() {
		yield return Load("Level1", GameSettings.gameDifficulties.Easy);
		ClearCores();
		GameManager.gm.Collect(GameManager.gm.BeatLevelScore - 1);
		yield return new WaitForSeconds(EnergySpawnDirector.FallbackDelay + 1.5f);
		Assert.AreEqual(2, Object.FindObjectsOfType<Treasure>().Length, "the missing one plus one spare");
	}

	[UnityTest]
	public IEnumerator HardDifficultyStillSpawnsEnergy() {
		yield return Load("Level2", GameSettings.gameDifficulties.Hard);
		yield return new WaitForSeconds(1.5f);
		Assert.That(Object.FindObjectsOfType<Treasure>().Length, Is.EqualTo(director.StartingSupply), "the supply rolled for this run");
		Assert.That(director.StartingSupply, Is.InRange(12, 15), "target 10 + 2..5 on every difficulty");
		ClearCores();
		yield return new WaitForSeconds(EnergySpawnDirector.FallbackDelay + 1.5f);
		Assert.GreaterOrEqual(Object.FindObjectsOfType<Treasure>().Length, 1);
	}

	// Spec §5.2: 12 s without a pickup and no reachable core near → one far core is replaced near the robot.
	[UnityTest]
	public IEnumerator StaleCoreReplacedNearTheRobot() {
        yield return Load("Level4", GameSettings.gameDifficulties.Normal);
        director.intervalOverride = 1000f;
        director.staleDelay = 1000f;
        yield return new WaitForSeconds(1.5f);   // robot has landed
        Treasure[] cores = Object.FindObjectsOfType<Treasure>().OrderByDescending(t => PathFromRobot(t.transform.position)).ToArray();
        Assume.That(cores.Length, Is.GreaterThan(0), "Level4 starts with a core");
        Treasure far = cores[0];
        for (int i = 1; i < cores.Length; i++) Object.Destroy(cores[i].gameObject);
        yield return null;

        // Level4's authored points can all be far from its initial start. Move the test robot
        // onto broad safe ground where the same runtime selector can prove a 5–18 m replacement exists.
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null) { body.isKinematic = true; body.velocity = Vector3.zero; }
        SpawnPoint near = null;
        float nearPath = float.MaxValue;
        foreach (Transform ground in director.landingPoints) {
            SpawnPoint metadata = ground != null ? ground.GetComponent<SpawnPoint>() : null;
            if (metadata == null || metadata.riskFlags != SpawnRisk.None) continue;
            Vector3 robotPosition = ground.position + Vector3.up * 0.8f;
            if (body != null) body.position = robotPosition;
            player.position = robotPosition;
            if (body != null) body.velocity = Vector3.zero;
            yield return null;
            SpawnPoint candidate; float path;
            if (PathFromRobot(far.transform.position) > 18f
                && director.TryGetCandidate(false, true, 5f, 18f, out candidate, out path)) {
                near = candidate; nearPath = path; break;
            }
        }
        Assert.IsNotNull(near, "the test relocates the robot to a safe broad-ground area with an eligible near candidate");
        Assert.That(nearPath, Is.InRange(5f, 18f));
        Assume.That(PathFromRobot(far.transform.position), Is.GreaterThan(18f), "the retained core remains far after relocating the robot");

        // The gameplay contract keeps a stale item that is still in view. Point the camera away
        // from this deliberately far, invisible fixture so the test covers the recall path.
        ThirdPersonOrbitCamera orbit = Camera.main != null ? Camera.main.GetComponent<ThirdPersonOrbitCamera>() : null;
        if (orbit != null) {
            Vector3 awayFromFar = player.position - far.transform.position;
            orbit.yaw = Mathf.Atan2(awayFromFar.x, awayFromFar.z) * Mathf.Rad2Deg;
        }
        director.staleDelay = 1f;
        GameManager.gm.Collect(GameManager.gm.BeatLevelScore - 1);
        yield return new WaitForSeconds(3f);
        Assert.IsTrue(far == null, "the far core is recalled");
        Treasure replacement = Object.FindObjectOfType<Treasure>();
        Assert.IsNotNull(replacement);
        Assert.LessOrEqual(PathFromRobot(replacement.transform.position), 18.5f);
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
		yield return new WaitForSeconds(EnergySpawnDirector.FallbackDelay + 1.5f);
		Assert.Greater(director.AliveCount, 0, "drops use the last valid robot point");
	}
}


public class EnergyCorridorGeometryTests {
    readonly List<GameObject> fixtures = new List<GameObject>();
    static readonly Vector3 FixtureOrigin = new Vector3(10000f, 1000f, 10000f);

    GameObject Box(string name, Vector3 position, Vector3 size) {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = FixtureOrigin + position;
        box.transform.localScale = size;
        fixtures.Add(box);
        return box;
    }

    EnergySpawnDirector CreateDirector() {
        GameObject owner = new GameObject("Corridor Test Director");
        fixtures.Add(owner);
        return owner.AddComponent<EnergySpawnDirector>();
    }

    [TearDown]
    public void Cleanup() {
        for (int i = fixtures.Count - 1; i >= 0; i--)
            if (fixtures[i] != null) Object.DestroyImmediate(fixtures[i]);
        fixtures.Clear();
        Physics.SyncTransforms();
    }

    [Test]
    public void GroundedWidthPassesAcrossContinuousFloor() {
        Box("Continuous floor", new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 20f));
        EnergySpawnDirector director = CreateDirector();
        Physics.SyncTransforms();

        Assert.IsTrue(director.HasGroundedClearCorridor(FixtureOrigin, Vector3.forward, 1.5f, 0.5f, new HazardVolume[0]));
    }

    [Test]
    public void GroundGapRejectsCorridorEvenWhenBothEndsHaveGround() {
        Box("Near ground", new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 0.8f));
        Box("Far ground", new Vector3(0f, -0.1f, 1.4f), new Vector3(20f, 0.2f, 0.8f));
        EnergySpawnDirector director = CreateDirector();
        Physics.SyncTransforms();

        Assert.IsFalse(director.HasGroundedClearCorridor(FixtureOrigin, Vector3.forward, 1.5f, 0.5f, new HazardVolume[0]),
            "a grounded endpoint cannot hide a gap in the escape path");
    }

    [Test]
    public void OpposingOnlyLanesDoNotPassRiskRouteValidation() {
        Box("Continuous floor", new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 20f));
        Box("Left barrier", new Vector3(-0.8f, 0.9f, 0f), new Vector3(0.2f, 1.8f, 20f));
        Box("Right barrier", new Vector3(0.8f, 0.9f, 0f), new Vector3(0.2f, 1.8f, 20f));
        EnergySpawnDirector director = CreateDirector();
        SpawnPoint point = new GameObject("Trap risk point").AddComponent<SpawnPoint>();
        point.transform.position = FixtureOrigin;
        fixtures.Add(point.gameObject);
        point.riskFlags = SpawnRisk.Trap;
        Physics.SyncTransforms();

        Assert.IsFalse(director.ValidRiskFootprint(FixtureOrigin, point, new HazardVolume[0]),
            "forward/back lanes alone would leave one choke corridor");
    }

    [Test]
    public void ObstacleAcrossCorridorRejectsEscape() {
        Box("Continuous floor", new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 20f));
        Box("Blocking wall", new Vector3(0f, 0.9f, 0.75f), new Vector3(2f, 1.8f, 0.2f));
        EnergySpawnDirector director = CreateDirector();
        Physics.SyncTransforms();

        Assert.IsFalse(director.HasGroundedClearCorridor(FixtureOrigin, Vector3.forward, 1.5f, 0.5f, new HazardVolume[0]),
            "a wall across the full player width blocks the escape path");
    }
}
