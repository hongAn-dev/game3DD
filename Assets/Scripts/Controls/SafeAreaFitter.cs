using UnityEngine;

/// <summary>
/// Keeps its RectTransform inside Screen.safeArea (notches, rounded corners) — spec §7.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour {

	private Rect applied;
	private Vector2 appliedScreen;

	/// <summary>Anchors for a safe area: x,y = anchorMin, width,height = anchorMax (all 0..1).</summary>
	public static Rect Anchors(Rect safeArea, Vector2 screen) {
		if (screen.x <= 0f || screen.y <= 0f)
			return new Rect(0f, 0f, 1f, 1f);
		return new Rect(safeArea.xMin / screen.x, safeArea.yMin / screen.y, safeArea.xMax / screen.x, safeArea.yMax / screen.y);
	}

	void Awake() {
		Apply();
	}

	void Update() {
		if (Screen.safeArea != applied || new Vector2(Screen.width, Screen.height) != appliedScreen)
			Apply();
	}

	void Apply() {
		applied = Screen.safeArea;
		appliedScreen = new Vector2(Screen.width, Screen.height);
		Rect anchors = Anchors(applied, appliedScreen);
		RectTransform rect = (RectTransform)transform;
		rect.anchorMin = new Vector2(anchors.x, anchors.y);
		rect.anchorMax = new Vector2(anchors.width, anchors.height);
		rect.offsetMin = rect.offsetMax = Vector2.zero;
	}
}
