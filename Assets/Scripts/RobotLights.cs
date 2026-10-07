using UnityEngine;

/// <summary>
/// Robot light strip: brighter as energy is collected, flashes red when the robot is hit.
/// </summary>
public class RobotLights : MonoBehaviour {

	public string lightSlotName = "RoboLight";
	public Color energyColor = new Color32(46, 230, 230, 255);
	public Color hitColor = new Color32(255, 90, 46, 255);
	public float hitFlashSeconds = 0.3f;

	private Material lightMaterial;
	private Health health;
	private float lastHealth;
	private float hitUntil;

	public static float Brightness(int score, int target) {
		if (target <= 0)
			return 1f;
		return Mathf.Lerp(0.3f, 1f, Mathf.Clamp01((float)score / target));
	}

	void Start() {
		health = GetComponent<Health>();
		lastHealth = health != null ? health.healthPoints : 0f;
		// renderer.materials creates instances, so other robots are not affected.
		foreach (Material material in GetComponent<Renderer>().materials)
			if (material.name.StartsWith(lightSlotName))
				lightMaterial = material;
	}

	void Update() {
		if (lightMaterial == null)
			return;
		if (health != null && health.healthPoints < lastHealth)
			hitUntil = Time.time + hitFlashSeconds;
		if (health != null)
			lastHealth = health.healthPoints;

		Color color;
		if (Time.time < hitUntil) {
			color = hitColor;
		} else {
			GameManager gm = GameManager.gm;
			float brightness = gm != null ? Brightness(gm.score, gm.BeatLevelScore) : 0.3f;
			color = energyColor * brightness;
		}
		lightMaterial.SetColor("_EmissionColor", color);
	}
}
