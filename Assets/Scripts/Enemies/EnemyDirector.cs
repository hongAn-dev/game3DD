using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of a level's enemy budget (spec §4.1/§4.2): alive + telegraphed enemies never exceed
/// LevelConfig.enemyCap[difficulty], of which at most bossCap[difficulty] are bosses. Gameplay time only: nothing
/// spawns during the level's quiet time, then one enemy every 2-3 s up to the cap; the boss (L3/L4) waits for
/// BossGateOpen. Each spawn is telegraphed (creep 0.7 s, boss 1 s) on a NavMesh point at least minPlayerDistance from
/// the robot, with a complete path to it and away from other enemies; if the robot reaches the marker the spawn is
/// cancelled. It also hands out chase tokens (2 in L1-L2, 3 in L3-L4, boss included) and patrol points near the
/// energy landing points. Development builds log the counts every 5 s.
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
	[Tooltip("Tests: open the boss gate at once.")]
	public bool bossGateOverride;

	readonly List<EnemyBrain> chasers = new List<EnemyBrain>();
	readonly List<Vector3> pendingPoints = new List<Vector3>();
	readonly List<EnemyBrain> bosses = new List<EnemyBrain>();
	readonly List<EnemyBrain> creeps = new List<EnemyBrain>();
	readonly List<GameObject> markers = new List<GameObject>();
	int pendingBosses, pendingCreeps;
	int cap, bossCap;
	LevelConfig config;
	float elapsed, timer, nextInterval;
	Transform player;

	public int AliveCount { get { Prune(); return bosses.Count + creeps.Count + pendingBosses + pendingCreeps; } }
	public int BossCount { get { Prune(); return bosses.Count + pendingBosses; } }
	public int Chasing { get { chasers.RemoveAll(b => b == null); return chasers.Count; } }

	void Prune() {
		bosses.RemoveAll(b => b == null);
		creeps.RemoveAll(c => c == null);
	}

	/// <summary>Boss gate (spec §4.2): at least 12 s played and 30% of the energy target, or 30 s played.</summary>
	public static bool BossGateOpen(float elapsed, int score, int target) {
		return (elapsed >= 12f && score >= 0.3f * target) || elapsed >= 30f;
	}

	/// <summary>Asks for one of the chase tokens; true if this enemy may chase. Already holding one counts.</summary>
	public bool RequestChase(EnemyBrain brain) {
		chasers.RemoveAll(b => b == null);
		if (chasers.Contains(brain))
			return true;
		if (chasers.Count >= maxChasers)
			return false;
		chasers.Add(brain);
		return true;
	}

	public void ReleaseChase(EnemyBrain brain) {
		chasers.Remove(brain);
	}

	void Awake() {
		Current = this;
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
		maxChasers = config.order <= 2 ? 2 : 3;
		GameObject p = GameObject.FindWithTag("Player");
		player = p != null ? p.transform : null;
		ScheduleNext();
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
		bool boss = BossCount < bossCap && BossAllowed() && bossPrefab != null;
		if (!boss && (creepPrefab == null || creeps.Count + pendingCreeps >= cap - bossCap))
			return;
		Vector3 point;
		if (PickPoint(out point))
			StartCoroutine(Spawn(boss, point));
	}

	bool RobotOnMesh(out Vector3 robot) {
		robot = Vector3.zero;
		NavMeshHit hit;
		if (player == null || !NavMesh.SamplePosition(player.position, out hit, 3f, NavMesh.AllAreas))
			return false;
		robot = hit.position;
		return true;
	}

	bool Valid(Vector3 point, Vector3 robot) {
		if (Vector3.Distance(point, player.position) < minPlayerDistance)
			return false;
		foreach (Vector3 pending in pendingPoints)
			if (Vector3.Distance(pending, point) < minEnemySpacing)
				return false;
		foreach (EnemyBrain brain in FindObjectsOfType<EnemyBrain>())
			if (Vector3.Distance(brain.transform.position, point) < minEnemySpacing)
				return false;
		var path = new NavMeshPath();
		return NavMesh.CalculatePath(point, robot, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
	}

	// A random valid spawn point: on the NavMesh, far enough from the robot, with a path to it, not on an enemy.
	bool PickPoint(out Vector3 point) {
		point = Vector3.zero;
		Vector3 robot;
		if (!RobotOnMesh(out robot))
			return false;   // robot airborne/off the mesh: try again next time
		var options = new List<Vector3>();
		foreach (Transform t in spawnPoints) {
			NavMeshHit hit;
			if (t != null && NavMesh.SamplePosition(t.position, out hit, 1.5f, NavMesh.AllAreas) && Valid(hit.position, robot))
				options.Add(hit.position);
		}
		if (options.Count == 0)
			return false;
		point = options[Random.Range(0, options.Count)];
		return true;
	}

	IEnumerator Spawn(bool boss, Vector3 point) {
		if (boss) pendingBosses++; else pendingCreeps++;
		pendingPoints.Add(point);
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
		if (cancelled || !GameFlow.IsGameplayActive)
			yield break;
		GameObject enemy = Instantiate(boss ? bossPrefab : creepPrefab, point, Quaternion.LookRotation(player != null ? Flat(player.position - point) : Vector3.forward));
		EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
		brain.Configure(EnemyProfile.For(config, GameSettings.difficulty, boss));
		brain.patrolPoints = PatrolPoints(point);
		(boss ? bosses : creeps).Add(brain);
	}

	// Energy landing points near the spawn: patrols pass along the routes the robot takes to the cores.
	Vector3[] PatrolPoints(Vector3 home) {
		EnergySpawnDirector energy = GetComponent<EnergySpawnDirector>();
		var points = new List<Vector3>();
		if (energy != null)
			foreach (Transform t in energy.landingPoints)
				if (t != null && Vector3.Distance(t.position, home) <= patrolRadius)
					points.Add(t.position);
		return points.ToArray();
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
			foreach (Transform t in spawnPoints)
				if (t != null && Valid(t.position, robot))
					valid++;
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
		pendingBosses = pendingCreeps = 0;
	}
}
