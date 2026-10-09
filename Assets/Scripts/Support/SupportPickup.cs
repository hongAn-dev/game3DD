using UnityEngine;

public enum SupportKind { Shield, Heal10, Heal20, Overdrive }

/// <summary>
/// A support item (spec §6.3): the robot touching it while Playing gets the effect. It is consumed only when the
/// effect applied (a heal at full HP stays for later). It lasts lifetime seconds of gameplay time, blinks gently in
/// the last blinkTime seconds, and reports Gone exactly once (pickup or expiry). Never energy.
/// </summary>
public class SupportPickup : MonoBehaviour {

	public SupportKind kind;
	public float lifetime = 25f;
	public float blinkTime = 3f;

	public event System.Action<SupportPickup> Gone;

	float age;
	bool gone;
	Renderer[] renderers;

	void Awake() {
		renderers = GetComponentsInChildren<Renderer>();
	}

	void OnTriggerEnter(Collider other) {
		TryApply(other);
	}

	void OnTriggerStay(Collider other) {
		if (kind == SupportKind.Heal10 || kind == SupportKind.Heal20)
			TryApply(other);   // standing on a heal at full HP: it heals once HP drops
	}

	void TryApply(Collider other) {
		if (gone || !GameFlow.IsGameplayActive || !other.CompareTag("Player"))
			return;
		if (Apply(other.gameObject))
			Finish();
	}

	public static bool Apply(SupportKind kind, GameObject robot) {
		switch (kind) {
		case SupportKind.Shield: {
			ShieldEffect shield = robot.GetComponent<ShieldEffect>();
			if (shield == null)
				shield = robot.AddComponent<ShieldEffect>();
			shield.Activate();
			return true;
		}
		case SupportKind.Heal10:
		case SupportKind.Heal20: {
			Health health = robot.GetComponent<Health>();
			return health != null && health.Heal(kind == SupportKind.Heal10 ? 10f : 20f);
		}
		default: {
			Overdrive overdrive = robot.GetComponent<Overdrive>();
			if (overdrive == null)
				overdrive = robot.AddComponent<Overdrive>();
			overdrive.Activate();
			return true;
		}
		}
	}

	bool Apply(GameObject robot) {
		return Apply(kind, robot);
	}

	void Update() {
		if (gone || !GameFlow.IsGameplayActive)
			return;
		age += Time.deltaTime;
		if (age >= lifetime) {
			Finish();
			return;
		}
		bool visible = lifetime - age > blinkTime || Mathf.Repeat(age, 0.4f) < 0.28f;
		foreach (Renderer r in renderers)
			if (r != null && r.enabled != visible)
				r.enabled = visible;
	}

	/// <summary>Removes the item and frees its slot; safe to call more than once.</summary>
	public void Finish() {
		if (gone)
			return;
		gone = true;
		if (Gone != null)
			Gone(this);
		Destroy(gameObject);
	}
}
