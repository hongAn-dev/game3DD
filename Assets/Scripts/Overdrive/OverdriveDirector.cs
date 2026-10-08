using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places at most one Overdrive pickup at a time (spec §9), every intervalMin..intervalMax seconds of gameplay after
/// the last one was taken, on an energy landing point (safe ground, away from the shore) at least minPlayerDistance
/// from the robot and minCoreDistance from any core, including cores still falling. EnergySpawnDirector in turn
/// never drops a core on the live pickup. Separate from the energy budget. Added from Level2 on.
/// </summary>
public class OverdriveDirector : MonoBehaviour {

	public GameObject pickupPrefab;
	public float intervalMin = 25f;
	public float intervalMax = 35f;
	[Tooltip("Seconds between pickups; < 0 uses intervalMin..intervalMax (tests set it).")]
	public float intervalOverride = -1f;
	public float minPlayerDistance = 6f;
	public float minCoreDistance = 2f;

	public GameObject Current { get; private set; }

	EnergySpawnDirector energy;
	Transform player;
	float timer, next;

	void Start() {
		energy = GetComponent<EnergySpawnDirector>();
		GameObject p = GameObject.FindWithTag("Player");
		player = p != null ? p.transform : null;
		Schedule();
	}

	void Schedule() {
		timer = 0f;
		next = Random.Range(intervalMin, intervalMax);
	}

	void Update() {
		if (!GameFlow.IsGameplayActive || Current != null || pickupPrefab == null || energy == null)
			return;
		timer += Time.deltaTime;
		if (timer < (intervalOverride > 0f ? intervalOverride : next))
			return;
		Vector3 point;
		if (PickPoint(out point)) {
			Current = Instantiate(pickupPrefab, point, Quaternion.identity);
			Schedule();
		}
	}

	bool PickPoint(out Vector3 point) {
		List<Vector3> cores = energy.Occupied();   // alive and still falling (their landing point)
		var options = new List<Vector3>();
		foreach (Transform t in energy.landingPoints) {
			if (t == null || (player != null && Vector3.Distance(t.position, player.position) < minPlayerDistance))
				continue;
			bool clear = true;
			foreach (Vector3 core in cores)
				clear &= Vector3.Distance(core, t.position) >= minCoreDistance;
			if (clear)
				options.Add(t.position);
		}
		point = options.Count > 0 ? options[Random.Range(0, options.Count)] : Vector3.zero;
		return options.Count > 0;
	}
}
