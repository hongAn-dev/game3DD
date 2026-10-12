using UnityEngine;

/// <summary>
/// Keeps a square RectTransform at a share of its root canvas height, whatever the canvas scaler's match mode. The
/// joystick uses 22 % on desktop and twice that on phones (playtest: the 22 % stick was too small for a thumb).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ScreenShareSizer : MonoBehaviour {

	[Range(0.1f, 0.5f)] public float heightShare = 0.22f;
	[Tooltip("Share used on phones and tablets.")]
	[Range(0.1f, 0.5f)] public float mobileShare = 0.44f;

	public float Share { get { return Application.isMobilePlatform ? mobileShare : heightShare; } }

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
		float size = Size(height, Share);
		RectTransform rect = (RectTransform)transform;
		rect.sizeDelta = new Vector2(size, size);
		// Centre half a stick plus a small margin from the bottom-left corner (it used to sit 0.75 sizes up,
		// which put the twice-as-big phone stick a third of the way up the screen).
		float margin = height * 0.05f;
		rect.anchoredPosition = new Vector2(size * 0.5f + margin, size * 0.5f + margin);
	}
}
