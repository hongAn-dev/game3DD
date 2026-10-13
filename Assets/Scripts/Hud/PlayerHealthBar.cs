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
	public GameObject slowGroup;
	public Text slowSeconds;
	public float aboveTop = 0.45f;   // metres above the top of the ball
	public float barWidth = 114f;    // 30% larger than the original 88 px container
	public float barHeight = 44f;

	Health health;
	ShieldEffect shield;
	SlowStatus slow;
	int shownSlow = -1;
	int shownShield = -1;
	Transform robot;
	float radius = 0.5f;
	Canvas canvas;
	float shownHp = -1f;
	float shownMaxHp = -1f;
	static Sprite solidFillSprite;
	const float PlayerMaxHealth = 100f;

	public static Color ColorFor(float fraction) {
		return fraction > 0.5f ? Green : fraction > 0.25f ? Yellow : Red;
	}

	public static string Label(float hp, float max) {
		// The campaign player always has a 100 HP pool; guard the label against stale serialized max values.
		return Mathf.CeilToInt(hp) + "/" + Mathf.RoundToInt(Mathf.Max(max, PlayerMaxHealth));
	}

	void Start() {
		GameObject player = GameObject.FindWithTag("Player");
		if (player != null) {
			robot = player.transform;
			health = player.GetComponent<Health>();
			shield = player.GetComponent<ShieldEffect>();
			slow = player.GetComponent<SlowStatus>();
			SphereCollider sphere = player.GetComponent<SphereCollider>();
			if (sphere != null)
				radius = sphere.radius * robot.lossyScale.y;
		}
		canvas = GetComponentInParent<Canvas>().rootCanvas;
		ConfigureLayout();
		NormalizePlayerHealth();
		LateUpdate();
	}

	void NormalizePlayerHealth() {
		if (health == null || robot == null || !robot.CompareTag("Player"))
			return;
		// Some built builds still contain the old 10 HP prefab data. Repair it before drawing or applying damage.
		if (!Mathf.Approximately(health.maxHealth, PlayerMaxHealth)) {
			health.maxHealth = PlayerMaxHealth;
			health.respawnHealthPoints = PlayerMaxHealth;
			health.healthPoints = PlayerMaxHealth;
			shownHp = -1f;
			shownMaxHp = -1f;
		}
	}

	void ConfigureLayout() {
		if (bar == null)
			return;
		aboveTop = Mathf.Max(aboveTop, 0.45f);
		bar.sizeDelta = new Vector2(barWidth, barHeight);
		if (fill != null) {
			// UISprite.psd has padded end caps. Use a generated 1x1 sprite for a true edge-to-edge fill.
			if (solidFillSprite == null)
				solidFillSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
			fill.sprite = solidFillSprite;
			fill.type = Image.Type.Filled;
			fill.fillMethod = Image.FillMethod.Horizontal;
			fill.fillOrigin = (int)Image.OriginHorizontal.Left;
			RectTransform background = fill.transform.parent as RectTransform;
			if (background != null) {
				background.sizeDelta = new Vector2(barWidth, 9f);
				background.anchoredPosition = new Vector2(0f, 4.5f);
				fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
				fill.rectTransform.anchoredPosition = Vector2.zero;
				fill.rectTransform.sizeDelta = background.sizeDelta;
			}
		}
		if (label != null) {
			RectTransform labelRect = label.rectTransform;
			labelRect.anchorMin = labelRect.anchorMax = new Vector2(1f, 0.5f);
			labelRect.pivot = new Vector2(0f, 0.5f);
			labelRect.anchoredPosition = new Vector2(6f, 0f);
			labelRect.sizeDelta = new Vector2(104f, 32f);
			label.alignment = TextAnchor.MiddleLeft;
			label.horizontalOverflow = HorizontalWrapMode.Overflow;
			label.verticalOverflow = VerticalWrapMode.Overflow;
		}
	}

	void LateUpdate() {
		NormalizePlayerHealth();
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
		int slowTime = slow != null && slow.Active ? Mathf.CeilToInt(slow.Remaining) : 0;
		if (slowGroup != null && slowTime != shownSlow) {
			shownSlow = slowTime;
			slowGroup.SetActive(slowTime > 0);
			if (slowTime > 0 && slowSeconds != null) slowSeconds.text = slowTime.ToString();
		}
		if (shownHp == health.healthPoints && shownMaxHp == health.maxHealth)
			return;   // no new label string every frame
		shownHp = health.healthPoints;
		shownMaxHp = health.maxHealth;
		float displayMax = Mathf.Max(health.maxHealth, PlayerMaxHealth);
		float fraction = displayMax > 0f ? Mathf.Clamp01(health.healthPoints / displayMax) : 0f;
		fill.fillAmount = fraction;
		fill.color = ColorFor(fraction);
		label.text = Label(health.healthPoints, displayMax);
	}
}
