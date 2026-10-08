using UnityEngine;

/// <summary>
/// Acid/lava death volume (spec §5): a thick trigger, separate from the sea visual. Anything with Health that
/// touches it dies; for the robot the level hazard cause is reported first so the game over screen names it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HazardVolume : MonoBehaviour {

	public string cause = "";

	void OnTriggerEnter(Collider other) {
		Health health = other.GetComponentInParent<Health>();
		if (health == null || health.healthPoints <= 0f)
			return;
		if (health.CompareTag("Player")) {
			if (!GameFlow.IsGameplayActive)
				return;
			GameFlow.ReportDeathCause(cause);
		}
		health.ApplyDamage(health.healthPoints);
	}
}
