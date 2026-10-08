using UnityEngine;

/// <summary>
/// Keeps a square RectTransform at a share of its root canvas height (the joystick: ≈22 % of the screen height on
/// every aspect ratio, spec §7), whatever the canvas scaler's match mode.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ScreenShareSizer : MonoBehaviour {

	[Range(0.1f, 0.4f)] public float heightShare = 0.22f;

	private float appliedHeight = -1f;

	public static float Size(float canvasHeight, float share) {
		return canvasHeight * share;
	}

	void Update() {
		Canvas canvas = GetComponentInParent<Canvas>();
		if (canvas == null)
			return;
		float height = ((RectTransform)canvas.rootCanvas.transform).rect.height;
		if (Mathf.Approximately(height, appliedHeight) || height <= 0f)
			return;
		appliedHeight = height;
		float size = Size(height, heightShare);
		RectTransform rect = (RectTransform)transform;
		rect.sizeDelta = new Vector2(size, size);
		rect.anchoredPosition = new Vector2(size * 0.75f, size * 0.75f);
	}
}
