using UnityEngine;

/// <summary>
/// Turns the base station lights on when the level is beaten.
/// </summary>
public class WinBeacons : MonoBehaviour {

	public Light[] lights;

	void Start() {
		SetLights(false);
	}

	void Update() {
		if (LevelBeaten()) {
			SetLights(true);
			enabled = false;
		}
	}

	// GameOver follows both BeatLevel and Death, so a live player tells them apart.
	bool LevelBeaten() {
		GameManager gm = GameManager.gm;
		if (gm == null)
			return false;
		if (gm.gameState == GameManager.gameStates.BeatLevel)
			return true;
		Health health = gm.player != null ? gm.player.GetComponent<Health>() : null;
		return gm.gameState == GameManager.gameStates.GameOver && health != null && health.isAlive;
	}

	void SetLights(bool on) {
		foreach (Light light in lights)
			if (light != null)
				light.enabled = on;
	}
}
