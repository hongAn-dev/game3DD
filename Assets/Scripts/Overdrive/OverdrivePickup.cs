using UnityEngine;

/// <summary>Overdrive pickup: the robot rolling through it gets (or refreshes) Overdrive. Never counts as energy.</summary>
public class OverdrivePickup : MonoBehaviour {

	void OnTriggerEnter(Collider other) {
		if (!GameFlow.IsGameplayActive || !other.CompareTag("Player") || other.GetComponent<Ball>() == null)
			return;
		Overdrive overdrive = other.GetComponent<Overdrive>();
		if (overdrive == null)
			overdrive = other.gameObject.AddComponent<Overdrive>();
		overdrive.Activate();
		Destroy(gameObject);
	}
}
