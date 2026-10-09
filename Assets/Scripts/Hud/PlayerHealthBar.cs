using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small HP bar with "HP/max" just above the robot (spec §6.2), drawn on a screen-space overlay canvas from the
/// robot's world position, so it never rolls with the ball and never shows up in the minimap. Green above 50%,
/// yellow 26–50%, red at 25% or less. Hidden behind the camera, once dead and outside Playing/Paused.
/// </summary>
public class PlayerHealthBar : MonoBehaviour {

	public static readonly Color Green = new Color32(70, 210, 110, 255);
	public static readonly Color Yellow = new Color32(245, 200, 60, 255);
	public static readonly Color Red = new Color32(235, 70, 60, 255);

	public RectTransform bar;    // moved over the robot each frame
	public Image fill;           // horizontal filled image
	public Text label;
	public GameObject shieldGroup;   // shield icon + seconds beside the bar
	public Text shieldSeconds;
	public float aboveTop = 0.32f;   // metres above the top of the ball

	Health health;
	ShieldEffect shield;
	int shownShield = -1;
	Transform robot;
	float radius = 0.5f;
	Canvas canvas;
	float shownHp = -1f;

	public static Color ColorFor(float fraction) {
		return fraction > 0.5f ? Green : fraction > 0.25f ? Yellow : Red;
	}

	public static string Label(float hp, float max) {
		return Mathf.CeilToInt(hp) + "/" + Mathf.RoundToInt(max);
	}

	void Start() {
		GameObject player = GameObject.FindWithTag("Player");
		if (player != null) {
			robot = player.transform;
			health = player.GetComponent<Health>();
			shield = player.GetComponent<ShieldEffect>();
			SphereCollider sphere = player.GetComponent<SphereCollider>();
			if (sphere != null)
				radius = sphere.radius * robot.lossyScale.y;
		}
		canvas = GetComponentInParent<Canvas>().rootCanvas;
		LateUpdate();
	}

	void LateUpdate() {
		Camera cam = Camera.main;
		bool show = health != null && robot != null && cam != null && health.healthPoints > 0f && robot.gameObject.activeInHierarchy
			&& (GameFlow.State == FlowState.Playing || GameFlow.State == FlowState.Paused);
		Vector3 screen = show ? cam.WorldToScreenPoint(robot.position + Vector3.up * (radius + aboveTop)) : Vector3.zero;
		show &= screen.z > 0f;
		if (bar.gameObject.activeSelf != show)
			bar.gameObject.SetActive(show);
		if (!show)
			return;
		Vector2 local;
		RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)bar.parent, screen,
			canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out local);
		bar.anchoredPosition = local;
		int seconds = shield != null && shield.Active ? Mathf.CeilToInt(shield.Remaining) : 0;
		if (shieldGroup != null && seconds != shownShield) {
			shownShield = seconds;
			shieldGroup.SetActive(seconds > 0);
			if (seconds > 0)
				shieldSeconds.text = seconds.ToString();
		}
		if (shownHp == health.healthPoints)
			return;   // no new label string every frame
		shownHp = health.healthPoints;
		float fraction = health.maxHealth > 0f ? Mathf.Clamp01(health.healthPoints / health.maxHealth) : 0f;
		fill.fillAmount = fraction;
		fill.color = ColorFor(fraction);
		label.text = Label(health.healthPoints, health.maxHealth);
	}
}
