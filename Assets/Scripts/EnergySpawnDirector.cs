using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of a level's energy budget (spec §4.2). Keeps LevelConfig.energyAtStart of the authored cores (spread
/// out) and removes the rest; every interval drops at most one core on an authored landing point (marker, then a
/// controlled fall) while cores alive + falling stay under min(cap, target − score). If no core exists for
/// fallbackDelay seconds while energy is still needed, one is dropped at once. Runs only while GameFlow is Playing.
/// </summary>
public class EnergySpawnDirector : MonoBehaviour {

	public GameObject corePrefab;
	public Material markerMaterial;
	public Transform[] landingPoints = new Transform[0];

	[Tooltip("Seconds between drops; < 0 uses the LevelConfig interval (tests set it).")]
	public float intervalOverride = -1f;
	public float fallbackDelay = 8f;
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
	private Transform player;

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

	/// <summary>
	/// Index of the point farthest from every occupied position, skipping points under 1 m from a core or closer
	/// than minPlayerDistance to the player; -1 when none qualifies.
	/// </summary>
	public static int PickPoint(IList<Vector3> points, IList<Vector3> occupied, Vector3 player, float minPlayerDistance) {
		int best = -1;
		float bestScore = -1f;
		for (int i = 0; i < points.Count; i++) {
			if (Vector3.Distance(points[i], player) < minPlayerDistance)
				continue;
			float nearest = float.MaxValue;
			foreach (Vector3 o in occupied)
				nearest = Mathf.Min(nearest, Vector3.Distance(points[i], o));
			if (nearest < 1f)
				continue;
			if (nearest > bestScore) {
				bestScore = nearest;
				best = i;
			}
		}
		return best;
	}

	void Start() {
		config = LevelCatalog.Get(SceneManager.GetActiveScene().name);
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

	// Keep energyAtStart authored cores, chosen spread out from the player start; remove every other authored core.
	void KeepStartingCores() {
		List<Treasure> authored = new List<Treasure>(FindObjectsOfType<Treasure>());
		List<Vector3> positions = authored.ConvertAll(t => t.transform.position);
		List<Vector3> kept = new List<Vector3>();
		if (player != null)
			kept.Add(player.position);
		int want = Mathf.Min(config.energyAtStart, AllowedAlive(config.energyCap, config.energyTarget, Score()));
		var keep = new HashSet<Treasure>();
		while (keep.Count < want) {
			int index = PickPoint(positions, kept, player != null ? player.position : Vector3.one * 9999f, minPlayerDistance);
			if (index < 0)
				break;
			keep.Add(authored[index]);
			kept.Add(positions[index]);
		}
		foreach (Treasure t in authored) {
			if (keep.Contains(t))
				alive.Add(t);
			else
				Destroy(t.gameObject);
		}
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
		int allowed = AllowedAlive(config.energyCap, config.energyTarget, Score());
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

	void TryDrop() {
		if (landingPoints.Length == 0 || corePrefab == null)
			return;
		var points = new List<Vector3>();
		foreach (Transform p in landingPoints)
			if (p != null)
				points.Add(p.position);
		var occupied = new List<Vector3>(pendingPoints);
		foreach (Treasure t in alive)
			if (t != null)
				occupied.Add(t.transform.position);
		int index = PickPoint(points, occupied, player != null ? player.position : Vector3.one * 9999f, minPlayerDistance);
		if (index < 0)
			return;
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
