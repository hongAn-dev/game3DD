using UnityEngine;
using UnityEngine.UI;

/// <summary>Keeps the SFX/BGM controls compact in existing scenes as well as newly generated top bars.</summary>
public static class QuickButtonLayout {
	const float Margin = 24f;
	const float Gap = 24f;
	const float Touch = 132f; // about 8% smaller than the original 144-unit touch target
	const float Icon = 72f;

	public static void Apply(RectTransform button) {
		if (button == null || (button.name != "Sound Button" && button.name != "Music Button"))
			return;
		button.sizeDelta = new Vector2(Touch, Touch);
		bool hasPause = button.parent != null && button.parent.Find("Pause Button") != null;
		int order = button.name == "Sound Button" ? (hasPause ? 2 : 1) : (hasPause ? 1 : 0);
		button.anchoredPosition = new Vector2(-(Margin + order * (Touch + Gap)), -Margin);
		Transform icon = button.Find("Icon");
		if (icon != null)
			icon.GetComponent<RectTransform>().sizeDelta = new Vector2(Icon, Icon);
		Transform label = button.Find("Audio Label");
		if (label != null) {
			RectTransform rect = label.GetComponent<RectTransform>();
			rect.sizeDelta = new Vector2(Touch, 32f);
			rect.anchoredPosition = new Vector2(-Touch * 0.5f, -Touch + 16f);
		}
	}
}
