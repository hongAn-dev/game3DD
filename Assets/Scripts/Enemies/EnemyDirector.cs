using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of a level's enemy budget (spec §4.1/§4.2): alive + telegraphed enemies never exceed
/// LevelConfig.enemyCap[difficulty], of which at most bossCap[difficulty] are bosses. Gameplay time only: nothing
/// spawns during quiet time, then a configured opening creep batch and later batches up to the cap; bosses wait for
/// BossGateOpen and the configured spacing. Each spawn is telegraphed (creep 0.7 s, boss 1 s) on a reachable NavMesh
/// point at least minPlayerDistance from the robot; if the robot reaches the marker the spawn is cancelled. One
/// active director owns the configured chase tokens and spreads L3/L4 patrols across authored energy regions.
/// Development builds log the counts every 5 s.
/// </summary>
public class EnemyDirector : MonoBehaviour {

	public static EnemyDirector Current { get; private set; }

	public GameObject bossPrefab;
	public GameObject creepPrefab;
	public Material telegraphMaterial;
	public Transform[] spawnPoints = new Transform[0];

	public float quietTime = 5f;   // replaced by LevelConfig.quietTime at Start
	public float intervalMin = 2f;
	public float intervalMax = 3f;
	[Tooltip("Seconds between spawns; < 0 uses intervalMin..intervalMax (tests set it).")]
	public float intervalOverride = -1f;
	public float telegraphTime = 0.7f;
	public float bossTelegraphTime = 1f;
	public float minPlayerDistance = 8f;
	public float cancelDistance = 4f;   // robot this close to a marker cancels the spawn
	public float minEnemySpacing = 2f;
	public float patrolRadius = 18f;
	public int maxChasers = 3;
	public const float ChaseReassignMargin = 4f;
	[Tooltip("Tests: open the boss gate at once.")]
	public bool bossGateOverride;

	readonly List<EnemyBrain> chasers = new List<EnemyBrain>();
	readonly List<Vector3> pendingPoints = new List<Vector3>();
	readonly List<EnemyBrain> bosses = new List<EnemyBrain>();
	readonly List<EnemyBrain> creeps = new List<EnemyBrain>();
	readonly List<GameObject> markers = new List<GameObject>();
	readonly List<int> pendingReservations = new List<int>();
	int pendingBosses, pendingCreeps;
	int cap, bossCap;
	LevelConfig config;
	float elapsed, timer, nextInterval;
	Transform player;
	bool initialBatchPending = true;
	float lastBossSpawnedAt = float.NegativeInfinity;
	EnemyBrain bossAttackOwner;
	public const float BossAttackTokenRange = 15f;

	public int AliveCount { get { Prune(); return bosses.Count + creeps.Count + pendingBosses + pendingCreeps; } }
	public int BossCount { get { Prune(); return bosses.Count + pendingBosses; } }
	/// <summary>Telegraphed (not yet spawned) enemy points; energy drops avoid them.</summary>
	public IReadOnlyList<Vector3> PendingSpawns { get { return pendingPoints; } }

	public int Chasing { get { chasers.RemoveAll(b => b == null); return chasers.Count; } }

	void Prune() {
		bosses.RemoveAll(b => b == null);
		creeps.RemoveAll(c => c == null);
	}

	/// <summary>Boss gate (spec §4.2): at least 12 s played and 30% of the energy target, or 30 s played.</summary>
	public static bool BossGateOpen(float elapsed, int score, int target) {
		return (elapsed >= 12f && score >= 0.3f * target) || elapsed >= 30f;
	}

	/// <summary>
	/// Asks for one of the chase tokens; true if this enemy may chase. Already holding one counts. When all tokens
	/// are taken, a requester closer to the robot takes the token of a chaser that has fallen behind (beyond its
	/// detect radius). Only a chaser currently in Chase can yield; an enemy in an attack keeps its slot until the attack
	/// finishes. The former holder is returned to Patrol synchronously before the token is granted.
	/// </summary>
	public bool RequestChase(EnemyBrain brain) {
		chasers.RemoveAll(b => b == null);
		if (chasers.Contains(brain))
			return true;
		if (chasers.Count >= maxChasers) {
			EnemyBrain trailing = null;
			foreach (EnemyBrain c in chasers)
				if (c.CanYieldChaseToken && (trailing == null || c.DistanceToTarget > trailing.DistanceToTarget))
					trailing = c;
			if (trailing == null || trailing.DistanceToTarget <= brain.DistanceToTarget + ChaseReassignMargin)
				return false;
			if (!trailing.YieldChaseToken())
				return false;
		}
		chasers.Add(brain);
		return true;
	}

	public bool HoldsChase(EnemyBrain brain) {
		return chasers.Contains(brain);
	}

	/// <summary>Only one boss inside the player bubble may begin/hold a slam or dash at once.</summary>
	public bool RequestBossAttack(EnemyBrain brain) {
		if (brain == null)
			return false;
		if (brain.DistanceToTarget > BossAttackTokenRange)
			return false;
		if (bossAttackOwner == null || bossAttackOwner.DistanceToTarget > BossAttackTokenRange) {
			if (bossAttackOwner != null)
				bossAttackOwner.ReleaseBossAttackToken();
			bossAttackOwner = brain;
			return true;
		}
		return bossAttackOwner == brain;
	}

	public void ReleaseBossAttack(EnemyBrain brain) {
		if (bossAttackOwner == brain)
			bossAttackOwner = null;
	}

	public void ForgetBossAttackOwner(EnemyBrain brain) {
		if (bossAttackOwner == brain)
			bossAttackOwner = null;
	}

	public void ReleaseChase(EnemyBrain brain) {
		chasers.Remove(brain);
	}

	void Awake() {
		if (Current != null && Current != this) {
			enabled = false;
			return;
		}
		Current = this;
	}

	void OnEnable() {
		if (Current == null || Current == this)
			Current = this;
		else
			enabled = false;
	}

	void OnDestroy() {
		if (Current == this)
			Current = null;
	}

	void Start() {
		config = LevelCatalog.Get(SceneManager.GetActiveScene().name);
		if (config == null) {
			enabled = false;   // still hands out chase tokens (tests use it bare)
			return;
		}
		int d = (int)GameSettings.difficulty;
		cap = config.enemyCap[d];
		bossCap = Mathf.Min(config.bossCap[d], cap);
		quietTime = config.quietTime;
		maxChasers = config.maxChasers != null && d < config.maxChasers.Length
			? config.maxChasers[d] : (config.order <= 2 ? 2 : 3);
		GameObject p = GameObject.FindWithTag("Player");
		player = p != null ? p.transform : null;
		ScheduleNext();
		timer = nextInterval;   // the first enemy comes as soon as the quiet time ends
	}

	void ScheduleNext() {
		nextInterval = intervalOverride > 0f ? intervalOverride : Random.Range(intervalMin, intervalMax);
		timer = 0f;
	}

	bool BossAllowed() {
		if (bossGateOverride)
			return true;
		int score = GameManager.gm != null ? GameManager.gm.score : 0;
		return BossGateOpen(elapsed, score, config.energyTarget);
	}

	void Update() {
		if (!GameFlow.IsGameplayActive)
			return;
		elapsed += Time.deltaTime;
		LogCounts();
		if (elapsed < quietTime)
			return;
		timer += Time.deltaTime;
		if (timer < (intervalOverride > 0f ? intervalOverride : nextInterval))
			return;
		ScheduleNext();
		if (AliveCount >= cap)
			return;
		bool boss = bossPrefab != null && BossCount < bossCap && BossAllowed() && pendingBosses == 0
			&& elapsed - lastBossSpawnedAt >= config.bossSpacing;
		if (boss) {
			Vector3 bossPoint;
			if (PickPoint(out bossPoint))
				BeginSpawn(true, bossPoint);
			return;
		}
		if (creepPrefab == null)
			return;
		int creepLimit = Mathf.Max(0, cap - bossCap);
		int free = Mathf.Min(cap - AliveCount, creepLimit - creeps.Count - pendingCreeps);
		int wanted = initialBatchPending ? config.enemyInitialBatch
			: Random.Range(1, Mathf.Max(1, config.enemyBatchMax) + 1);
		int count = Mathf.Min(free, wanted);
		int spawned = 0;
		for (int i = 0; i < count; i++) {
			Vector3 point;
			if (!PickPoint(out point) || !BeginSpawn(false, point))
				break;
			spawned++;
		}
		if (spawned > 0)
			initialBatchPending = false;
	}

	bool RobotOnMesh(out Vector3 robot) {
		robot = Vector3.zero;
		NavMeshHit hit;
		if (player == null || !NavMesh.SamplePosition(player.position, out hit, 3f, NavMesh.AllAreas))
			return false;
		robot = hit.position;
		return true;
	}

	// Cheap checks first (robot distance, enemies, pending spawns, cores/items), the NavMesh path last.
	bool Valid(Vector3 point, Vector3 robot, List<Vector3> taken) {
		if (Vector3.Distance(point, player.position) < minPlayerDistance)
			return false;
		foreach (Vector3 other in taken)
			if (Vector3.Distance(other, point) < minEnemySpacing)
				return false;
		var path = new NavMeshPath();
		return NavMesh.CalculatePath(point, robot, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
	}

	// Everything a new enemy must not appear on: enemies, telegraphed spawns, energy cores/drops and items.
	List<Vector3> Taken() {
		var taken = new List<Vector3>(pendingPoints);
		Prune();
		foreach (EnemyBrain brain in bosses) taken.Add(brain.transform.position);
		foreach (EnemyBrain brain in creeps) taken.Add(brain.transform.position);
		if (SpawnRegistry.Current != null)
			taken.AddRange(SpawnRegistry.Current.OccupiedPoints());
		else {
			EnergySpawnDirector energy = GetComponent<EnergySpawnDirector>();
			if (energy != null) taken.AddRange(energy.Occupied());
		}
		return taken;
	}

	// A random valid spawn point: on the NavMesh, far enough from the robot, with a path to it, not on an enemy.
	bool PickPoint(out Vector3 point) {
		point = Vector3.zero;
		Vector3 robot;
		if (!RobotOnMesh(out robot))
			return false;   // robot airborne/off the mesh: try again next time
		List<Vector3> taken = Taken();
		var options = new List<Vector3>();
		foreach (Transform t in spawnPoints) {
			NavMeshHit hit;
			if (t != null && NavMesh.SamplePosition(t.position, out hit, 1.5f, NavMesh.AllAreas) && Valid(hit.position, robot, taken))
				options.Add(hit.position);
		}
		if (options.Count == 0)
			return false;
		if (config != null && config.order >= 3) {
			int leastLoaded = int.MaxValue;
			var balanced = new List<Vector3>();
			foreach (Vector3 candidate in options) {
				int load = RegionLoad(RegionAt(candidate));
				if (load < leastLoaded) {
					leastLoaded = load;
					balanced.Clear();
				}
				if (load == leastLoaded)
					balanced.Add(candidate);
			}
			options = balanced;
		}
		point = options[Random.Range(0, options.Count)];
		return true;
	}

	bool BeginSpawn(bool boss, Vector3 point) {
		int reservation = 0;
		SpawnRegistry registry = SpawnRegistry.Current;
		GameObject prefab = boss ? bossPrefab : creepPrefab;
		float radius = prefab != null && prefab.GetComponent<NavMeshAgent>() != null
			? prefab.GetComponent<NavMeshAgent>().radius : 0.6f;
		if (registry != null && !registry.TryReserve(SpawnRegistry.SpawnCategory.EnemyTelegraph, this,
			point, radius, cap, out reservation))
			return false;
		if (boss) pendingBosses++; else pendingCreeps++;
		pendingPoints.Add(point);
		if (reservation != 0) pendingReservations.Add(reservation);
		StartCoroutine(Spawn(boss, point, reservation));
		return true;
	}

	IEnumerator Spawn(bool boss, Vector3 point, int reservation) {
		GameObject marker = Marker(point);
		markers.Add(marker);
		float duration = boss ? bossTelegraphTime : telegraphTime;
		bool cancelled = false;
		float t = 0f;
		while (t < duration) {
			t += Time.deltaTime;   // frozen while paused
			float size = Mathf.Lerp(0.5f, boss ? 2.4f : 1.6f, t / duration);
			if (marker != null)
				marker.transform.localScale = new Vector3(size, 0.01f, size);
			// The robot rolled onto the marker: cancel; a later turn picks another point with a full telegraph.
			cancelled |= player != null && Vector3.Distance(Flat3(point), Flat3(player.position)) < cancelDistance;
			if (cancelled)
				break;
			yield return null;
		}
		markers.Remove(marker);
		Destroy(marker);
		pendingPoints.Remove(point);
		if (boss) pendingBosses--; else pendingCreeps--;
		pendingReservations.Remove(reservation);
		if (cancelled || !GameFlow.IsGameplayActive) {
			if (SpawnRegistry.Current != null) SpawnRegistry.Current.Release(reservation);
			yield break;
		}
		GameObject enemy = Instantiate(boss ? bossPrefab : creepPrefab, point, Quaternion.LookRotation(player != null ? Flat(player.position - point) : Vector3.forward));
		EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
		brain.Configure(EnemyProfile.For(config, GameSettings.difficulty, boss));
		int region, cluster;
		RegionAndCluster(point, out region, out cluster);
		brain.SetPatrolRegion(region, PatrolPoints(point, region, cluster));
		(boss ? bosses : creeps).Add(brain);
		if (boss) lastBossSpawnedAt = elapsed;
		// The shared registry reserves telegraph footprints only. Live enemies are represented by their moving
		// transforms below, so committing them would leave a stale spawn-location footprint behind.
		if (SpawnRegistry.Current != null && reservation != 0)
			SpawnRegistry.Current.Release(reservation);
	}

	int GridRegion(Vector3 point) {
		return Mathf.FloorToInt(point.x / 16f) * 10000 + Mathf.FloorToInt(point.z / 16f);
	}

	void RegionAndCluster(Vector3 point, out int region, out int cluster) {
		region = cluster = GridRegion(point);
		EnergySpawnDirector energy = GetComponent<EnergySpawnDirector>();
		if (energy == null)
			return;
		float closest = float.PositiveInfinity;
		foreach (Transform t in energy.landingPoints) {
			if (t == null) continue;
			SpawnPoint metadata = t.GetComponent<SpawnPoint>();
			if (metadata == null) continue;
			float distance = (t.position - point).sqrMagnitude;
			if (distance >= closest) continue;
			closest = distance;
			region = metadata.regionId;
			cluster = metadata.clusterId;
		}
	}

	int RegionAt(Vector3 point) {
		int region, cluster;
		RegionAndCluster(point, out region, out cluster);
		return region;
	}

	int RegionLoad(int region) {
		int count = 0;
		Prune();
		foreach (EnemyBrain brain in bosses)
			if (brain.PatrolRegionId == region) count++;
		foreach (EnemyBrain brain in creeps)
			if (brain.PatrolRegionId == region) count++;
		foreach (Vector3 point in pendingPoints)
			if (RegionAt(point) == region) count++;
		return count;
	}

	// Enemies patrol the same authored energy cluster as their spawn region, spreading pressure around the map.
	Vector3[] PatrolPoints(Vector3 home, int region, int cluster) {
		EnergySpawnDirector energy = GetComponent<EnergySpawnDirector>();
		var sameCluster = new List<Vector3>();
		var nearby = new List<Vector3>();
		if (energy != null) {
			foreach (Transform t in energy.landingPoints) {
				if (t == null) continue;
				if (Vector3.Distance(t.position, home) <= patrolRadius)
					nearby.Add(t.position);
				SpawnPoint metadata = t.GetComponent<SpawnPoint>();
				if (metadata != null && metadata.regionId == region && metadata.clusterId == cluster
					&& Vector3.Distance(t.position, home) <= patrolRadius * 1.5f)
					sameCluster.Add(t.position);
			}
		}
		return sameCluster.Count > 0 ? sameCluster.ToArray() : nearby.ToArray();
	}

	static Vector3 Flat3(Vector3 v) {
		v.y = 0f;
		return v;
	}

	float nextLog;

	// Development builds: tell "not spawned" apart from "spawned but far or stuck" (spec §4.2).
	void LogCounts() {
		if (!Debug.isDebugBuild || Application.isEditor || elapsed < nextLog)
			return;
		nextLog = elapsed + 5f;
		Vector3 robot;
		int valid = 0, withPath = 0;
		if (RobotOnMesh(out robot)) {
			List<Vector3> taken = Taken();
			foreach (Transform t in spawnPoints) {
				NavMeshHit hit;
				if (t != null && NavMesh.SamplePosition(t.position, out hit, 1.5f, NavMesh.AllAreas) && Valid(hit.position, robot, taken))
					valid++;
			}
			foreach (EnemyBrain brain in FindObjectsOfType<EnemyBrain>()) {
				var path = new NavMeshPath();
				if (NavMesh.CalculatePath(brain.transform.position, robot, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
					withPath++;
			}
		}
		Debug.Log("EnemyDirector: cap " + cap + " alive " + (AliveCount - pendingBosses - pendingCreeps) + " pending " + (pendingBosses + pendingCreeps)
			+ " valid spawns " + valid + " with path " + withPath + " chasing " + Chasing);
	}

	static Vector3 Flat(Vector3 v) {
		v.y = 0f;
		return v.sqrMagnitude > 0.001f ? v : Vector3.forward;
	}

	GameObject Marker(Vector3 point) {
		GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		marker.name = "Enemy Spawn Marker";
		DestroyImmediate(marker.GetComponent<Collider>());   // never blocks the robot, not even for a frame
		marker.transform.position = point + Vector3.up * 0.03f;
		marker.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
		if (telegraphMaterial != null)
			marker.GetComponent<Renderer>().sharedMaterial = telegraphMaterial;
		return marker;
	}

	void OnDisable() {
		StopAllCoroutines();
		foreach (GameObject marker in markers)
			if (marker != null)
				Destroy(marker);
		markers.Clear();
		pendingPoints.Clear();
		pendingReservations.Clear();
		pendingBosses = pendingCreeps = 0;
		if (bossAttackOwner != null)
			bossAttackOwner.ReleaseBossAttackToken();
		bossAttackOwner = null;
		chasers.Clear();
		if (Current == this && config != null)
			Current = null;
		if (SpawnRegistry.Current != null)
			SpawnRegistry.Current.ReleaseOwner(this);
	}
}
