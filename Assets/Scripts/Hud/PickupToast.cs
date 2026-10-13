using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating pickup messages (playtest: "+1 năng lượng!", "-20 HP!") so new players learn what each item does. A short
/// line pops above the robot, rises and fades in about a second; quick pickups stack instead of overlapping. Lives on
/// the "Toast Canvas" FlowUiSetup builds in each level; Show() is a no-op where there is none (tests, menu).
/// </summary>
public class PickupToast : MonoBehaviour {

	public static PickupToast Current { get; private set; }

	public Font font;
	public const float Life = 1.1f;
	const int Pool = 5;
	const float Rise = 90f, Stack = 52f;

	public static readonly Color Energy = new Color32(46, 230, 230, 255);
	public static readonly Color Heal = new Color32(95, 235, 115, 255);
	public static readonly Color Shield = new Color32(120, 175, 255, 255);
	public static readonly Color Boost = new Color32(255, 214, 40, 255);
	public static readonly Color Hurt = new Color32(255, 80, 80, 255);
	public static readonly Color EnergyLoss = new Color32(240, 95, 225, 255);
	public static readonly Color Slow = new Color32(255, 140, 40, 255);

	Text[] texts;
	float[] age;
	Vector2[] origin;
	RectTransform canvasRect;

	/// <summary>The newest message (tests).</summary>
	public string Last { get; private set; }

	public static void Show(string message, Color color, Vector3 worldPosition) {
		if (Current != null)
			Current.Spawn(message, color, worldPosition);
	}

	void Awake() {
		Current = this;
		canvasRect = (RectTransform)transform;
		texts = new Text[Pool];
		age = new float[Pool];
		origin = new Vector2[Pool];
		for (int i = 0; i < Pool; i++) {
			var go = new GameObject("Toast " + i, typeof(RectTransform), typeof(Text), typeof(Shadow));
			go.transform.SetParent(transform, false);
			Text t = go.GetComponent<Text>();
			t.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
			t.fontSize = 46;
			t.fontStyle = FontStyle.Bold;
			t.alignment = TextAnchor.MiddleCenter;
			t.horizontalOverflow = HorizontalWrapMode.Overflow;
			t.raycastTarget = false;
			t.rectTransform.sizeDelta = new Vector2(700f, 70f);
			Shadow shadow = go.GetComponent<Shadow>();
			shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
			shadow.effectDistance = new Vector2(3f, -3f);
			go.SetActive(false);
			texts[i] = t;
			age[i] = Life;
		}
	}

	void OnDestroy() {
		if (Current == this)
			Current = null;
	}

	void Spawn(string message, Color color, Vector3 worldPosition) {
		Last = message;
		int slot = 0, active = 0;
		for (int i = 0; i < Pool; i++) {
			if (age[i] < Life) active++;
			if (age[i] > age[slot]) slot = i;   // reuse the oldest
		}
		Vector2 local = Vector2.zero;
		Camera cam = Camera.main;
		if (cam != null) {
			Vector3 screen = cam.WorldToScreenPoint(worldPosition + Vector3.up * 1.4f);
			Canvas canvas = GetComponent<Canvas>();
			Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out local);
		}
		origin[slot] = local + Vector2.up * Stack * Mathf.Min(active, Pool - 1);
		age[slot] = 0f;
		texts[slot].text = message;
		texts[slot].color = color;
		texts[slot].gameObject.SetActive(true);
		Place(slot);
	}

	void Update() {
		for (int i = 0; i < Pool; i++) {
			if (age[i] >= Life)
				continue;
			age[i] += Time.deltaTime;   // frozen while paused
			if (age[i] >= Life) {
				texts[i].gameObject.SetActive(false);
				continue;
			}
			Place(i);
		}
	}

	void Place(int i) {
		float t = age[i] / Life;
		texts[i].rectTransform.anchoredPosition = origin[i] + Vector2.up * Rise * t;
		float pop = t < 0.15f ? Mathf.Lerp(1.25f, 1f, t / 0.15f) : 1f;
		texts[i].rectTransform.localScale = Vector3.one * pop;
		Color c = texts[i].color;
		c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
		texts[i].color = c;
	}
}
