using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of a level's energy budget (spec §4.2). Creates LevelConfig.energyAtStart cores on landing points (the
/// first near the robot); every interval drops at most one core on a landing point (marker, then a controlled fall)
/// while cores alive + falling stay under min(cap, target − score). If no core exists for fallbackDelay seconds while
/// energy is still needed, one is dropped at once; if no core is picked up for staleDelay seconds, the farthest one is
/// replaced near the robot. Runs only while GameFlow is Playing.
/// </summary>
public class EnergySpawnDirector : MonoBehaviour {

	public GameObject corePrefab;
	public Material markerMaterial;
	public Transform[] landingPoints = new Transform[0];

	[Tooltip("Seconds between drops; < 0 uses the LevelConfig interval (tests set it).")]
	public float intervalOverride = -1f;
	public float fallbackDelay = 8f;
	[Tooltip("Seconds without a pickup after which the farthest core is treated as unreachable and replaced near the robot.")]
	public float staleDelay = 30f;
	public float markerDuration = 0.5f;
	public float dropHeight = 4f;
	public float dropDuration = 0.8f;
	public float minPlayerDistance = 4f;

	private LevelConfig config;
	private readonly List<Treasure> alive = new List<Treasure>();
	private readonly List<Vector3> pendingPoints = new List<Vector3>();
	private float timer;
	private float nextInterval;
	private float emptyTimer;
	private float killHeight = -50f;
	private float staleTimer;
	private int lastScore = -1;
	private Transform player;
	private OverdriveDirector boost;

	/// <summary>Cores on the map plus cores being dropped.</summary>
	public int AliveCount {
		get {
			alive.RemoveAll(t => t == null);
			return alive.Count + pendingPoints.Count;
		}
	}

	/// <summary>How many cores may exist at once: never more than the cap nor than the energy still needed.</summary>
	public static int AllowedAlive(int cap, int target, int score) {
		return Mathf.Max(0, Mathf.Min(cap, target - score));
	}

	public const float IsolationCap = 12f;   // beyond this, more distance from other cores does not help
	public const float NearBand = 8f;        // preferred distance band from the robot
	public const float FarBand = 35f;

	/// <summary>
	/// Index of the best point: away from other cores (up to IsolationCap) and inside the NearBand..FarBand distance
	/// band around the robot. Points under 1 m from a core or closer than minPlayerDistance to the robot are skipped;
	/// -1 when none qualifies. Ties keep list order (the director shuffles the list).
	/// </summary>
	public static int PickPoint(IList<Vector3> points, IList<Vector3> occupied, Vector3 player, float minPlayerDistance) {
		int best = -1;
		float bestScore = float.MinValue;
		for (int i = 0; i < points.Count; i++) {
			float toPlayer = Vector3.Distance(points[i], player);
			if (toPlayer < minPlayerDistance)
				continue;
			float nearest = float.MaxValue;
			foreach (Vector3 o in occupied)
				nearest = Mathf.Min(nearest, Vector3.Distance(points[i], o));
			if (nearest < 1f)
				continue;
			float score = Mathf.Min(nearest, IsolationCap)
				- 0.5f * Mathf.Max(0f, toPlayer - FarBand) - 0.5f * Mathf.Max(0f, NearBand - toPlayer);
			if (score > bestScore) {
				bestScore = score;
				best = i;
			}
		}
		return best;
	}

	/// <summary>Index of the free point closest to ~12 m from the robot (used for the first core and replacements).</summary>
	public static int NearPoint(IList<Vector3> points, IList<Vector3> occupied, Vector3 player, float minPlayerDistance) {
		int best = -1;
		float bestScore = float.MaxValue;
		for (int i = 0; i < points.Count; i++) {
			float toPlayer = Vector3.Distance(points[i], player);
			if (toPlayer < minPlayerDistance)
				continue;
			bool taken = false;
			foreach (Vector3 o in occupied)
				taken |= Vector3.Distance(points[i], o) < 1f;
			float score = Mathf.Abs(toPlayer - 12f);
			if (!taken && score < bestScore) {
				bestScore = score;
				best = i;
			}
		}
		return best;
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

		KeepStartingCores();
		ScheduleNext();
	}

	// Starting cores are created on landing points: the first close to the robot, the rest spread out. Any core
	// still authored in the scene is removed (EnergySetup strips them; this guards older scenes).
	void KeepStartingCores() {
		foreach (Treasure t in FindObjectsOfType<Treasure>())
			Destroy(t.gameObject);
		int want = Mathf.Min(config.energyAtStart, AllowedAlive(config.energyCap, config.energyTarget, Score()));
		for (int n = 0; n < want; n++) {
			List<Vector3> points = ShuffledPoints();
			List<Vector3> occupied = Occupied();
			int index = n == 0 ? NearPoint(points, occupied, PlayerPosition(), minPlayerDistance)
				: PickPoint(points, occupied, PlayerPosition(), minPlayerDistance);
			if (index < 0)
				break;
			GameObject core = Instantiate(corePrefab, points[index], Quaternion.identity);
			alive.Add(core.GetComponent<Treasure>());
		}
	}

	Vector3 PlayerPosition() {
		return player != null ? player.position : Vector3.one * 9999f;
	}

	List<Vector3> ShuffledPoints() {
		var points = new List<Vector3>();
		foreach (Transform p in landingPoints)
			if (p != null)
				points.Add(p.position);
		for (int i = points.Count - 1; i > 0; i--) {
			int j = Random.Range(0, i + 1);
			Vector3 tmp = points[i];
			points[i] = points[j];
			points[j] = tmp;
		}
		return points;
	}

	/// <summary>Cores alive, cores being dropped, and the Overdrive pickup: no new core goes on any of them.</summary>
	public List<Vector3> Occupied() {
		var occupied = new List<Vector3>(pendingPoints);
		foreach (Treasure t in alive)
			if (t != null)
				occupied.Add(t.transform.position);
		if (boost != null && boost.Current != null)
			occupied.Add(boost.Current.transform.position);
		return occupied;
	}

	int Score() {
		return GameManager.gm != null ? GameManager.gm.score : 0;
	}

	void ScheduleNext() {
		nextInterval = intervalOverride > 0f ? intervalOverride : Random.Range(config.energyIntervalMin, config.energyIntervalMax);
		timer = 0f;
	}

	void Update() {
		if (!GameFlow.IsGameplayActive)
			return;

		// Cores that fell out of the world free their slot.
		foreach (Treasure t in alive)
			if (t != null && t.transform.position.y < killHeight)
				Destroy(t.gameObject);

		int count = AliveCount;
		int score = Score();
		int allowed = AllowedAlive(config.energyCap, config.energyTarget, score);

		// No pickup for staleDelay while cores exist: treat the farthest one as unreachable and replace it near the
		// robot (spec §4.2 rule 5: clean the broken core first, then refill within the cap).
		if (score != lastScore || count == 0) {
			lastScore = score;
			staleTimer = 0f;
		} else {
			staleTimer += Time.deltaTime;
			if (staleTimer >= staleDelay) {
				staleTimer = 0f;
				Treasure farthest = null;
				foreach (Treasure t in alive)
					if (t != null && (farthest == null || Vector3.Distance(t.transform.position, PlayerPosition()) > Vector3.Distance(farthest.transform.position, PlayerPosition())))
						farthest = t;
				if (farthest != null) {
					alive.Remove(farthest);
					Destroy(farthest.gameObject);
					TryDrop(true);
					return;
				}
			}
		}
		emptyTimer = count == 0 && allowed > 0 ? emptyTimer + Time.deltaTime : 0f;

		if (emptyTimer >= fallbackDelay && count < allowed) {
			TryDrop();
			emptyTimer = 0f;
			ScheduleNext();
			return;
		}

		timer += Time.deltaTime;
		float interval = intervalOverride > 0f ? intervalOverride : nextInterval;
		if (timer >= interval) {
			// At most one drop per interval; a full cap waits for the next interval (no backlog).
			if (count < allowed)
				TryDrop();
			ScheduleNext();
		}
	}

	void TryDrop(bool nearPlayer = false) {
		if (landingPoints.Length == 0 || corePrefab == null)
			return;
		List<Vector3> points = ShuffledPoints();
		List<Vector3> occupied = Occupied();
		int index = nearPlayer ? NearPoint(points, occupied, PlayerPosition(), minPlayerDistance)
			: PickPoint(points, occupied, PlayerPosition(), minPlayerDistance);
		if (index >= 0)
			StartCoroutine(Drop(points[index]));
	}

	IEnumerator Drop(Vector3 point) {
		pendingPoints.Add(point);
		GameObject marker = Marker(point);

		float elapsed = 0f;
		while (elapsed < markerDuration) {
			elapsed += Time.deltaTime;   // frozen while paused (timeScale 0)
			yield return null;
		}

		GameObject core = Instantiate(corePrefab, point + Vector3.up * dropHeight, Quaternion.identity);
		Collider trigger = core.GetComponent<Collider>();
		if (trigger != null)
			trigger.enabled = false;
		elapsed = 0f;
		while (elapsed < dropDuration && core != null) {
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / dropDuration);
			core.transform.position = point + Vector3.up * dropHeight * (1f - t * t);
			yield return null;
		}

		if (marker != null)
			Destroy(marker);
		pendingPoints.Remove(point);
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
		Destroy(marker.GetComponent<Collider>());
		RaycastHit hit;
		Vector3 ground = Physics.Raycast(point + Vector3.up, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : point;
		marker.transform.position = ground + Vector3.up * 0.02f;
		marker.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
		if (markerMaterial != null)
			marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
		return marker;
	}

	void OnDisable() {
		StopAllCoroutines();
		pendingPoints.Clear();
	}
}
