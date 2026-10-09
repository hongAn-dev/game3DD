using UnityEngine;

/// <summary>
/// Overdrive on the robot (spec §9): top speed ×1.3 and acceleration ×1.15 for Duration seconds of gameplay time.
/// Picking it up again refreshes the time to Duration (never stacks). When it runs out the multipliers ease back to 1
/// over EaseOut seconds, so the robot slows down smoothly. Frozen while not Playing. Lives on the robot, so a retry
/// or the next level starts without it.
/// </summary>
[RequireComponent(typeof(Ball))]
public class Overdrive : MonoBehaviour {

	public const float Duration = 5f;
	public const float SpeedBoost = 1.3f;
	public const float AccelBoost = 1.15f;
	public const float EaseOut = 0.6f;

	public float Remaining { get; private set; }
	public bool Active { get { return Remaining > 0f; } }

	Ball ball;
	float blend;
	OverdriveIndicator indicator;

	void Awake() {
		ball = GetComponent<Ball>();
		indicator = FindObjectOfType<OverdriveIndicator>();
	}

	public void Activate() {
		Remaining = Duration;
		blend = 1f;
		Apply();
		Sfx.Play(SfxEvent.OverdriveOn);
	}

	void Update() {
		if (!GameFlow.IsGameplayActive)
			return;
		bool was = Active;
		Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
		if (was && !Active)
			Sfx.Play(SfxEvent.OverdriveOff);
		if (!Active)
			blend = Mathf.MoveTowards(blend, 0f, Time.deltaTime / EaseOut);
		Apply();
	}

	void Apply() {
		ball.SpeedMultiplier = Mathf.Lerp(1f, SpeedBoost, blend);
		ball.AccelMultiplier = Mathf.Lerp(1f, AccelBoost, blend);
		if (indicator != null)
			indicator.Show(Remaining);
	}

	void OnDisable() {
		if (indicator != null)
			indicator.Show(0f);
	}
}
