using UnityEngine;

/// <summary>
/// Shield on the robot (spec §6.3): blocks every enemy attack for Duration seconds of gameplay time (Health asks
/// Active), shown as a light transparent bubble 1.15× the robot. Picking it up again resets the time (no stacking),
/// it never adds HP and never blocks hazards. A blocked hit makes the bubble pulse. Lives on the robot, so a death,
/// a retry or the next level start without it.
/// </summary>
[RequireComponent(typeof(Health))]
public class ShieldEffect : MonoBehaviour {

	public const float Duration = 5f;

	public Material bubbleMaterial;
	public float bubbleScale = 1.15f;

	public float Remaining { get; private set; }
	public bool Active { get { return Remaining > 0f; } }
	public GameObject Bubble { get { EnsureBubble(); return bubble; } }

	GameObject bubble;
	float pulse;

	void Awake() {
		EnsureBubble();
		GetComponent<Health>().Blocked += () => pulse = 0.12f;
	}

	void EnsureBubble() {
		if (bubble != null)
			return;
		bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		bubble.name = "Shield Bubble";
		DestroyImmediate(bubble.GetComponent<Collider>());   // also safe in edit mode (tests)
		bubble.transform.SetParent(transform, false);
		bubble.transform.localScale = Vector3.one * bubbleScale;   // the robot sphere mesh is 1 unit across
		Renderer r = bubble.GetComponent<Renderer>();
		r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		r.receiveShadows = false;
		if (bubbleMaterial != null)
			r.sharedMaterial = bubbleMaterial;
		bubble.SetActive(false);
	}

	public void Activate() {
		Remaining = Duration;
		Bubble.SetActive(true);
		Sfx.Play(SfxEvent.ShieldOn);
	}

	void Update() {
		if (!GameFlow.IsGameplayActive || !Active)
			return;
		Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
		pulse = Mathf.Max(0f, pulse - Time.deltaTime);
		bubble.transform.localScale = Vector3.one * bubbleScale * (pulse > 0f ? 1.1f : 1f);
		if (!Active) {
			bubble.SetActive(false);
			Sfx.Play(SfxEvent.ShieldOff);
		}
	}

	void OnDisable() {
		Remaining = 0f;
		if (bubble != null)
			bubble.SetActive(false);
	}
}
