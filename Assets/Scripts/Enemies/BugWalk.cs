using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Procedural walk for the bug creep: the four legs (children named Leg_FL/FR/BL/BR, pivot at the hip) swing in
/// diagonal pairs at a rate that follows the agent speed; the body bobs slightly. No animation clips needed.
/// </summary>
public class BugWalk : MonoBehaviour {

	public float strideDegrees = 28f;
	public float stepsPerMetre = 1.6f;

	NavMeshAgent agent;
	Transform[] legs;
	Quaternion[] rest;
	float phase;

	void Start() {
		agent = GetComponentInParent<NavMeshAgent>();
		string[] names = { "Leg_FL", "Leg_BR", "Leg_FR", "Leg_BL" };   // diagonal pairs: FL+BR, FR+BL
		legs = new Transform[names.Length];
		rest = new Quaternion[names.Length];
		for (int i = 0; i < names.Length; i++) {
			legs[i] = FindDeep(transform, names[i]);
			if (legs[i] != null)
				rest[i] = legs[i].localRotation;
		}
	}

	static Transform FindDeep(Transform root, string name) {
		foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
			if (t.name == name)
				return t;
		return null;
	}

	void Update() {
		float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
		phase += speed * stepsPerMetre * Mathf.PI * Time.deltaTime;
		float amount = Mathf.Clamp01(speed / 2f);
		for (int i = 0; i < legs.Length; i++) {
			if (legs[i] == null)
				continue;
			float swing = Mathf.Sin(phase + (i < 2 ? 0f : Mathf.PI)) * strideDegrees * amount;
			legs[i].localRotation = rest[i] * Quaternion.Euler(swing, 0f, 0f);
		}
	}
}
