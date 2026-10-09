using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of a level's energy budget (spec §5). Starts with LevelConfig.energyAtStart cores (the first 4–10 m
/// from the robot by path); every energyInterval it drops a random batch (energyBatchMin..Max, upper bound included)
/// while cores alive + reserved (marked or falling) stay within min(energyCap, MaxAlive, target − score). Each core
/// reserves its point before its marker, so later cores of the same batch see it; one core per batch is placed
/// 6–18 m from the robot by path when possible, the others spread over the emptier areas. Candidates are landing
/// points with a complete NavMesh path from the robot, ≥ minPlayerDistance from it and ≥ minSpacing from cores,
/// reservations, items and enemies. Nothing alive or reserved for FallbackDelay → one core near the robot (retry each
/// second). No pickup for staleDelay and nothing reachable nearby → one far core is recalled and replaced near.
/// A win, a death or a transition cancels markers, falling cores and reservations; pause/intro only freeze.
/// </summary>
public class EnergySpawnDirector : MonoBehaviour {

	public const int MaxAlive = 10;
	public const float FallbackDelay = 3f;
	public const float FallbackRetry = 1f;

	public GameObject corePrefab;
	public Material markerMaterial;
	public Transform[] landingPoints = new Transform[0];

	[Tooltip("Seconds between batches; < 0 uses the LevelConfig interval (tests set it).")]
	public float intervalOverride = -1f;
	[Tooltip("Seconds without a pickup after which a far core is replaced near the robot.")]
	public float staleDelay = 12f;
	public float markerDuration = 0.35f;
	public float dropHeight = 4f;
	public float dropDuration = 0.5f;
	public float minPlayerDistance = 3f;
	public float minSpacing = 2f;
	public Vector2 firstCorePath = new Vector2(4f, 10f);
	public Vector2 nearPath = new Vector2(6f, 18f);

	LevelConfig config;
	readonly List<Treasure> alive = new List<Treasure>();
	readonly List<Vector3> reserved = new List<Vector3>();
	readonly List<GameObject> inFlight = new List<GameObject>();   // markers and falling cores
	float timer, nextInterval, emptyTimer, staleTimer, killHeight = -50f;
	int lastScore = -1;
	Transform player;
	OverdriveDirector boost;
	Vector3 robotPoint;
	bool robotPointValid;
	static readonly RaycastHit[] GroundHits = new RaycastHit[16];

	/// <summary>Where the first starting core went (tests check its path distance).</summary>
	internal Vector3 FirstCorePoint { get; private set; }

	/// <summary>Tests: run a batch on the next Update.</summary>
	internal void ForceBatch() {
		timer = float.MaxValue;
	}

	/// <summary>Cores on the map plus cores marked or falling.</summary>
	public int AliveCount {
		get {
			alive.RemoveAll(t => t == null);
			return alive.Count + reserved.Count;
		}
	}

	public int ReservedCount { get { return reserved.Count; } }

	/// <summary>How many cores may exist at once: the level cap, never above MaxAlive nor the energy still needed.</summary>
	public static int AllowedAlive(int cap, int target, int score) {
		return Mathf.Max(0, Mathf.Min(Mathf.Min(cap, MaxAlive), target - score));
	}

	/// <summary>Cores to drop this turn: the random batch, cut to the free slots.</summary>
	public static int SpawnCount(int batch, int allowed, int alive, int reservedCount) {
		return Mathf.Max(0, Mathf.Min(batch, allowed - alive - reservedCount));
	}

	/// <summary>Random batch size, both bounds included (Random.Range(int, int) excludes the upper one).</summary>
	public static int RandomBatch(int min, int max) {
		return Random.Range(min, max + 1);
	}

	public static float PathLength(NavMeshPath path) {
		float length = 0f;
		Vector3[] corners = path.corners;
		for (int i = 1; i < corners.Length; i++)
			length += Vector3.Distance(corners[i - 1], corners[i]);
		return length;
	}

	/// <summary>Cores alive, reservations and items (Overdrive, support): nothing new goes on any of them.</summary>
	public List<Vector3> Occupied() {
		var occupied = new List<Vector3>(reserved);
		foreach (Treasure t in alive)
			if (t != null)
				occupied.Add(t.transform.position);
		if (boost != null && boost.Current != null)
			occupied.Add(boost.Current.transform.position);
		return occupied;
	}

	void Start() {
		config = LevelCatalog.Get(SceneManager.GetActiveScene().name);
		boost = GetComponent<OverdriveDirector>();
		GameObject playerObject = GameObject.FindWithTag("Player");
		player = playerObject != null ? playerObject.transform : null;
		if (config == null) {
			enabled = false;
			return;
		}
		float lowest = float.MaxValue;
		foreach (Transform p in landingPoints)
			if (p != null)
				lowest = Mathf.Min(lowest, p.position.y);
		if (lowest < float.MaxValue)
			killHeight = lowest - 20f;
		GameFlow.Changed += OnFlowChanged;
		StartingCores();
		ScheduleNext();
	}

	void OnDestroy() {
		GameFlow.Changed -= OnFlowChanged;
	}

	void OnFlowChanged(FlowState state) {
		if (state == FlowState.Dead || state == FlowState.LevelComplete || state == FlowState.Transition)
			CancelInFlight();
	}

	void CancelInFlight() {
		StopAllCoroutines();
		foreach (GameObject go in inFlight)
			if (go != null)
				Destroy(go);
		inFlight.Clear();
		reserved.Clear();
	}

	void OnDisable() {
		CancelInFlight();
	}

	// Starting cores appear at once (no fall): the first 4–10 m from the robot by path, the rest spread out.
	void StartingCores() {
		foreach (Treasure t in FindObjectsOfType<Treasure>())
			Destroy(t.gameObject);   // guards scenes that still hold authored cores
		int want = Mathf.Min(config.energyAtStart, AllowedAlive(config.energyCap, config.energyTarget, Score()));
		List<Candidate> candidates = Candidates();
		for (int n = 0; n < want; n++) {
			Vector3 point;
			if (!Pick(candidates, n == 0 ? firstCorePath : nearPath, n == 0, out point))
				break;
			if (n == 0)
				FirstCorePoint = point;
			alive.Add(Instantiate(corePrefab, point, Quaternion.identity).GetComponent<Treasure>());
			Taken(candidates, point);
		}
	}

	int Score() {
		return GameManager.gm != null ? GameManager.gm.score : 0;
	}

	void ScheduleNext() {
		nextInterval = intervalOverride > 0f ? intervalOverride : Random.Range(config.energyIntervalMin, config.energyIntervalMax);
		timer = 0f;
	}

	// The robot's NavMesh point under it; while airborne/off the mesh the last valid one is kept.
	void UpdateRobotPoint() {
		if (player == null)
			return;
		float ground = float.MinValue;
		int count = Physics.RaycastNonAlloc(player.position + Vector3.up, Vector3.down, GroundHits, 60f, ~0, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < count; i++)
			if (GroundHits[i].collider.attachedRigidbody == null)
				ground = Mathf.Max(ground, GroundHits[i].point.y);
		NavMeshHit nav;
		if (ground > float.MinValue && player.position.y - ground < 2f
			&& NavMesh.SamplePosition(new Vector3(player.position.x, ground, player.position.z), out nav, 3f, NavMesh.AllAreas)) {
			robotPoint = nav.position;
			robotPointValid = true;
		} else if (!robotPointValid && ground > float.MinValue
			&& NavMesh.SamplePosition(new Vector3(player.position.x, ground, player.position.z), out nav, 3f, NavMesh.AllAreas)) {
			robotPoint = nav.position;   // start of the level: the robot is still dropping onto the ground
			robotPointValid = true;
		}
	}

	struct Candidate {
		public Vector3 point;
		public float path;
		public float isolation;   // distance to the nearest core/reservation/item
	}

	List<Candidate> Candidates() {
		var result = new List<Candidate>();
		UpdateRobotPoint();
		if (!robotPointValid)
			return result;
		List<Vector3> taken = Occupied();
		var enemies = new List<Vector3>();
		foreach (EnemyBrain e in FindObjectsOfType<EnemyBrain>())
			enemies.Add(e.transform.position);
		if (EnemyDirector.Current != null)
			enemies.AddRange(EnemyDirector.Current.PendingSpawns);   // telegraphed enemy spawns too
		var path = new NavMeshPath();
		foreach (Transform t in landingPoints) {
			if (t == null || Vector3.Distance(t.position, player.position) < minPlayerDistance)
				continue;
			float isolation = float.MaxValue;
			foreach (Vector3 o in taken)
				isolation = Mathf.Min(isolation, Vector3.Distance(o, t.position));
			if (isolation < minSpacing)
				continue;
			bool nearEnemy = false;
			foreach (Vector3 e in enemies)
				nearEnemy |= Vector3.Distance(e, t.position) < minSpacing;
			if (nearEnemy)
				continue;
			NavMeshHit nav;
			if (!NavMesh.SamplePosition(t.position, out nav, 1.5f, NavMesh.AllAreas)
				|| !NavMesh.CalculatePath(robotPoint, nav.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
				continue;
			result.Add(new Candidate { point = t.position, path = PathLength(path), isolation = isolation });
		}
		return result;
	}

	/// <summary>
	/// A point in the [band] path range (random among them); otherwise the closest to that range (strict for the
	/// first core, which would rather be near than far). Spread picks (strict = false and no band match) favour the
	/// most isolated candidates.
	/// </summary>
	bool Pick(Vector2 band, bool near, out Vector3 point) {
		return Pick(Candidates(), band, near, out point);
	}

	// Removes candidates the new core makes too close, and lowers the isolation of the rest (one candidate scan per
	// batch instead of one per core).
	void Taken(List<Candidate> candidates, Vector3 point) {
		for (int i = candidates.Count - 1; i >= 0; i--) {
			float d = Vector3.Distance(candidates[i].point, point);
			if (d < minSpacing) {
				candidates.RemoveAt(i);
			} else {
				Candidate c = candidates[i];
				c.isolation = Mathf.Min(c.isolation, d);
				candidates[i] = c;
			}
		}
	}

	bool Pick(List<Candidate> candidates, Vector2 band, bool near, out Vector3 point) {
		point = Vector3.zero;
		var all = new List<Candidate>(candidates);
		if (all.Count == 0)
			return false;
		if (near) {
			List<Candidate> inBand = all.FindAll(c => c.path >= band.x && c.path <= band.y);
			if (inBand.Count > 0) {
				point = inBand[Random.Range(0, inBand.Count)].point;
				return true;
			}
			all.Sort((a, b) => Mathf.Abs(a.path - Mid(band)).CompareTo(Mathf.Abs(b.path - Mid(band))));
			point = all[0].point;
			return true;
		}
		// Spread: random among the three most isolated candidates (areas with fewer cores).
		all.Sort((a, b) => b.isolation.CompareTo(a.isolation));
		point = all[Random.Range(0, Mathf.Min(3, all.Count))].point;
		return true;
	}

	static float Mid(Vector2 band) {
		return (band.x + band.y) * 0.5f;
	}

	void Update() {
		if (!GameFlow.IsGameplayActive)
			return;
		UpdateRobotPoint();

		// Cores that fell out of the world free their slot.
		foreach (Treasure t in alive)
			if (t != null && t.transform.position.y < killHeight)
				Destroy(t.gameObject);

		int count = AliveCount;
		int score = Score();
		int allowed = AllowedAlive(config.energyCap, config.energyTarget, score);
		if (score != lastScore) {
			lastScore = score;
			staleTimer = 0f;
		}

		// Nothing alive or falling while energy is still needed: one core near the robot, retried every second.
		if (allowed > 0 && count == 0) {
			emptyTimer += Time.deltaTime;
			if (emptyTimer >= FallbackDelay) {
				Vector3 point;
				if (Pick(nearPath, true, out point)) {
					Drop(point);
					emptyTimer = 0f;
					ScheduleNext();
				} else {
					emptyTimer = FallbackDelay - FallbackRetry;
					if (Debug.isDebugBuild)
						Debug.LogWarning("EnergySpawnDirector: no safe landing point near the robot, retrying in 1 s");
				}
				return;
			}
		} else {
			emptyTimer = 0f;
		}

		ReplaceStaleCore(count);

		timer += Time.deltaTime;
		if (timer < (intervalOverride > 0f ? intervalOverride : nextInterval))
			return;
		ScheduleNext();
		// One batch per interval; what does not fit is dropped (no backlog).
		int n = SpawnCount(RandomBatch(config.energyBatchMin, config.energyBatchMax), allowed, alive.Count, reserved.Count);
		List<Candidate> candidates = n > 0 ? Candidates() : null;
		for (int i = 0; i < n; i++) {
			Vector3 point;
			if (!Pick(candidates, nearPath, i == 0, out point))
				break;
			Drop(point);   // reserves the point at once
			Taken(candidates, point);   // and the next core of the batch sees it
		}
	}

	// No pickup for staleDelay and nothing reachable within the near band: recall the farthest (by path, or
	// unreachable) core that is not right next to the robot and drop one near it instead. One core per staleDelay.
	void ReplaceStaleCore(int count) {
		if (count == 0) {
			staleTimer = 0f;
			return;
		}
		staleTimer += Time.deltaTime;
		if (staleTimer < staleDelay)
			return;
		staleTimer = 0f;
		Treasure worst = null;
		float worstPath = -1f;
		bool nearExists = false;
		var path = new NavMeshPath();
		foreach (Treasure t in alive) {
			if (t == null)
				continue;
			float length = float.MaxValue;
			NavMeshHit nav;
			if (robotPointValid && NavMesh.SamplePosition(t.transform.position, out nav, 1.5f, NavMesh.AllAreas)
				&& NavMesh.CalculatePath(robotPoint, nav.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
				length = PathLength(path);
			nearExists |= length <= nearPath.y;
			if (Vector3.Distance(t.transform.position, player.position) >= 3f && length > worstPath) {
				worstPath = length;
				worst = t;
			}
		}
		if (nearExists || worst == null)
			return;
		alive.Remove(worst);
		Destroy(worst.gameObject);
		Vector3 point;
		if (Pick(nearPath, true, out point))
			Drop(point);
	}

	void Drop(Vector3 point) {
		reserved.Add(point);
		StartCoroutine(DropRoutine(point));
	}

	IEnumerator DropRoutine(Vector3 point) {
		GameObject marker = Marker(point);
		inFlight.Add(marker);
		float elapsed = 0f;
		while (elapsed < markerDuration) {
			elapsed += Time.deltaTime;   // frozen while paused
			yield return null;
		}

		GameObject core = Instantiate(corePrefab, point + Vector3.up * dropHeight, Quaternion.identity);
		inFlight.Add(core);
		Collider trigger = core.GetComponent<Collider>();
		if (trigger != null)
			trigger.enabled = false;   // no pickup until it lands
		elapsed = 0f;
		while (elapsed < dropDuration && core != null) {
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / dropDuration);
			core.transform.position = point + Vector3.up * dropHeight * (1f - t * t);
			yield return null;
		}

		inFlight.Remove(marker);
		inFlight.Remove(core);
		if (marker != null)
			Destroy(marker);
		reserved.Remove(point);
		if (core != null) {
			core.transform.position = point;
			if (trigger != null)
				trigger.enabled = true;
			alive.Add(core.GetComponent<Treasure>());
		}
	}

	// Flat cyan disc on the ground under the landing point.
	GameObject Marker(Vector3 point) {
		GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		marker.name = "Energy Landing Marker";
		DestroyImmediate(marker.GetComponent<Collider>());   // never blocks the robot, not even for a frame
		RaycastHit hit;
		Vector3 ground = Physics.Raycast(point + Vector3.up, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : point;
		marker.transform.position = ground + Vector3.up * 0.02f;
		marker.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
		if (markerMaterial != null)
			marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
		return marker;
	}
}
