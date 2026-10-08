using UnityEngine;
using System.Collections;

/// <summary>
/// Collectible energy core: +value once, only while the level is being played.
/// </summary>
public class Treasure : MonoBehaviour {

	// Set how much the coin worth it
	public int value = 10;
	// The game object to render when the treasure is dead
	public GameObject explosionPrefab;

	// One pickup per core even when several player colliders touch it in the same step.
	private bool collected;

	void OnTriggerEnter (Collider other) {
		Rigidbody body = other.attachedRigidbody;
		if (collected || !GameFlow.IsGameplayActive || body == null || body.gameObject.tag != "Player")
			return;
		collected = true;

		if (GameManager.gm != null)
			GameManager.gm.Collect (value);

		if (explosionPrefab != null)
			Instantiate (explosionPrefab, transform.position, Quaternion.identity);

		Destroy (gameObject);
	}
}
