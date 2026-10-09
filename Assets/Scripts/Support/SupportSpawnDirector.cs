using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of the support budget (spec §6.3): shield, +10, +20 and Overdrive items, separate from energy.
/// Items alive + falling stay within LevelConfig.supportCap; every supportInterval one item is rolled by
/// supportWeights (the timer runs whether or not items are picked up). Level1 drops a first shield firstShieldAfter
/// seconds into play. With HP ≤ 40 and no heal near the robot the next item is a +20; if the budget is full of
/// non-heal items, one far item (never within 3 m of the robot) is replaced by a +20. Drops go through the energy
/// director's candidates (shared occupancy, ≥ 2 m from cores/items) with the same marker + fall. A win, a death or a
/// transition cancels anything in flight; pause/intro only freeze.
/// </summary>
public class SupportSpawnDirector : MonoBehaviour {

	public static SupportSpawnDirector Current { get; private set; }

	public GameObject shieldPrefab, heal10Prefab, heal20Prefab, overdrivePrefab;
	public Material markerMaterial;
	[Tooltip("Seconds between items; < 0 uses the LevelConfig interval (tests set it).")]
	public float intervalOverride = -1f;
	[Tooltip("Item lifetime; < 0 keeps the prefab's 25 s (tests set it).")]
	public float lifetimeOverride = -1f;
	[Tooltip("Tests: replaces LevelConfig.supportWeights when set.")]
	public float[] weightsOverride;
	public float lowHealth = 40f;
	public float healNear = 18f;
	public float markerDuration = 0.35f;
	public float dropDuration = 0.5f;
	public float dropHeight = 4f;

	readonly List<SupportPickup> items = new List<SupportPickup>();
	readonly List<Vector3> pending = new List<Vector3>();
	readonly List<GameObject> inFlight = new List<GameObject>();
	LevelConfig config;
	EnergySpawnDirector energy;
	Health robot;
	float elapsed, timer, next;
	bool firstShieldDone;

	public IReadOnlyList<SupportPickup> Items { get { items.RemoveAll(i => i == null); return items; } }
	public IReadOnlyList<Vector3> Pending { get { return pending; } }
	public int AliveCount { get { return Items.Count + pending.Count; } }

	/// <summary>Weighted roll (weights: shield, heal10, heal20, overdrive; roll in [0, 1)).</summary>
	public static SupportKind Choose(float[] weights, float roll) {
		float total = 0f;
		foreach (float w in weights)
			total += w;
		float pick = roll * total, sum = 0f;
		for (int i = 0; i < weights.Length; i++) {
			sum += weights[i];
			if (weights[i] > 0f && pick < sum)
				return (SupportKind)i;
		}
		for (int i = weights.Length - 1; i >= 0; i--)
			if (weights[i] > 0f)
				return (SupportKind)i;
		return SupportKind.Shield;
	}

	void Awake() {
		Current = this;
	}

	void Start() {
		config = LevelCatalog.Get(SceneManager.GetActiveScene().name);
		energy = GetComponent<EnergySpawnDirector>();
		GameObject player = GameObject.FindWithTag("Player");
		robot = player != null ? player.GetComponent<Health>() : null;
		if (config == null || energy == null) {
			enabled = false;
			return;
		}
		GameFlow.Changed += OnFlowChanged;
		ScheduleNext();
	}

	void OnDestroy() {
		GameFlow.Changed -= OnFlowChanged;
		if (Current == this)
			Current = null;
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
		pending.Clear();
	}

	void OnDisable() {
		CancelInFlight();
	}

	void ScheduleNext() {
		next = intervalOverride > 0f ? intervalOverride : Random.Range(config.supportIntervalMin, config.supportIntervalMax);
		timer = 0f;
	}

	bool LowHealth() {
		return robot != null && robot.healthPoints > 0f && robot.healthPoints <= lowHealth;
	}

	static bool IsHeal(SupportKind kind) {
		return kind == SupportKind.Heal10 || kind == SupportKind.Heal20;
	}

	// A heal the robot can actually reach soon (by NavMesh path, not straight line).
	bool HealNearRobot() {
		foreach (SupportPickup item in Items)
			if (IsHeal(item.kind) && energy.PathFromRobot(item.transform.position) <= healNear)
				return true;
		return false;
	}

	void Update() {
		if (!GameFlow.IsGameplayActive)
			return;
		elapsed += Time.deltaTime;
		// First shield (Level1): retried until it actually drops, at a point the camera can see.
		if (!firstShieldDone && config.firstShieldAfter > 0f && elapsed >= config.firstShieldAfter && AliveCount < config.supportCap)
			firstShieldDone = TrySpawn(SupportKind.Shield, true, true);
		timer += Time.deltaTime;
		if (timer < (intervalOverride > 0f ? intervalOverride : next))
			return;
		ScheduleNext();
		bool needHeal = LowHealth() && !HealNearRobot();
		if (AliveCount < config.supportCap) {
			TrySpawn(needHeal ? SupportKind.Heal20 : Choose(weightsOverride != null && weightsOverride.Length == 4 ? weightsOverride : config.supportWeights, Random.value), needHeal, false);
		} else if (needHeal && pending.Count == 0) {
			// Budget full of non-heal items: swap the farthest one (never right next to the robot) for a +20 — only
			// once a point for the +20 is found, and never a heal.
			SupportPickup far = null;
			foreach (SupportPickup item in Items) {
				if (IsHeal(item.kind))
					continue;
				float d = Vector3.Distance(item.transform.position, robot.transform.position);
				if (d >= 3f && (far == null || d > Vector3.Distance(far.transform.position, robot.transform.position)))
					far = item;
			}
			Vector3 point;
			if (far != null && energy.PickSupportPoint(true, false, out point)) {
				far.Finish();
				StartDrop(SupportKind.Heal20, point);
			}
		}
	}

	GameObject PrefabFor(SupportKind kind) {
		switch (kind) {
		case SupportKind.Shield: return shieldPrefab;
		case SupportKind.Heal10: return heal10Prefab;
		case SupportKind.Heal20: return heal20Prefab;
		default: return overdrivePrefab;
		}
	}

	bool TrySpawn(SupportKind kind, bool near, bool visible) {
		Vector3 point;
		if (PrefabFor(kind) == null || !energy.PickSupportPoint(near, visible, out point))
			return false;
		StartDrop(kind, point);
		return true;
	}

	void StartDrop(SupportKind kind, Vector3 point) {
		pending.Add(point);
		StartCoroutine(Drop(PrefabFor(kind), point));
	}

	IEnumerator Drop(GameObject prefab, Vector3 point) {
		GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		marker.name = "Support Landing Marker";
		DestroyImmediate(marker.GetComponent<Collider>());
		marker.transform.position = point + Vector3.down * 0.38f;
		marker.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
		if (markerMaterial != null)
			marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
		inFlight.Add(marker);
		float elapsedDrop = 0f;
		while (elapsedDrop < markerDuration) {
			elapsedDrop += Time.deltaTime;
			yield return null;
		}
		GameObject item = Instantiate(prefab, point + Vector3.up * dropHeight, Quaternion.identity);
		inFlight.Add(item);
		Collider trigger = item.GetComponent<Collider>();
		trigger.enabled = false;
		elapsedDrop = 0f;
		while (elapsedDrop < dropDuration && item != null) {
			elapsedDrop += Time.deltaTime;
			float t = Mathf.Clamp01(elapsedDrop / dropDuration);
			item.transform.position = point + Vector3.up * dropHeight * (1f - t * t);
			yield return null;
		}
		inFlight.Remove(marker);
		inFlight.Remove(item);
		Destroy(marker);
		pending.Remove(point);
		if (item == null)
			yield break;
		item.transform.position = point;
		trigger.enabled = true;
		SupportPickup pickup = item.GetComponent<SupportPickup>();
		if (lifetimeOverride > 0f)
			pickup.lifetime = lifetimeOverride;
		pickup.Gone += p => items.Remove(p);
		items.Add(pickup);
	}
}
