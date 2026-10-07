using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Control-panel look for all UI: OpenSans font, dark panels with cyan accents, Vietnamese strings,
/// energy core icon and zone name next to the score, click sound, no GitHub button.
/// Run with -executeMethod UiTheme.Apply
/// </summary>
public static class UiTheme {

	static readonly Color Panel = new Color32(30, 35, 40, 217);
	static readonly Color PanelSelected = new Color32(40, 110, 115, 235);
	static readonly Color Cyan = new Color32(46, 230, 230, 255);
	static readonly Color TextColor = new Color32(235, 242, 245, 255);
	// Danger red-orange, lightened to stay readable on the dark panel.
	static readonly Color LostColor = new Color32(255, 120, 80, 255);

	// Old text (prefix match, case sensitive) -> new text. "Play Again" must come before "Play".
	static readonly KeyValuePair<string, string>[] Strings = {
		new KeyValuePair<string, string>("LiBot Adventure", "Robo Lạc Lối"),
		new KeyValuePair<string, string>("Play Again", "Thử lại"),
		new KeyValuePair<string, string>("Play", "Chơi"),
		new KeyValuePair<string, string>("Quit", "Thoát"),
		new KeyValuePair<string, string>("Easy", "Dễ"),
		new KeyValuePair<string, string>("Normal", "Thường"),
		new KeyValuePair<string, string>("Hard", "Khó"),
		new KeyValuePair<string, string>("Level Victory!", "Đủ năng lượng!"),
		new KeyValuePair<string, string>("Main Menu", "Menu chính"),
		new KeyValuePair<string, string>("Next Level", "Khu tiếp theo"),
		new KeyValuePair<string, string>("CONGRATULATIONS!", "ĐÃ VỀ TỚI CĂN CỨ!"),
		new KeyValuePair<string, string>("ĐÃ VỀ TỚI CĂN CỨ!", "ĐÃ VỀ TỚI CĂN CỨ!"),
		new KeyValuePair<string, string>("Thanks for playing", "Cảm ơn bạn đã chơi!"),
	};

	[MenuItem("Tools/Robo Lac Loi/Apply UI Theme")]
	public static void Apply() {
		Font bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/OpenSans/OpenSansBold.ttf");
		Sprite icon = CoreIcon();
		AudioClip click = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/KenneyAudio/click_002.ogg");

		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })) {
			string path = AssetDatabase.GUIDToAssetPath(guid);
			GameObject root = PrefabUtility.LoadPrefabContents(path);
			try {
				if (Style(root, bold, icon, click, ""))
					PrefabUtility.SaveAsPrefabAsset(root, path);
			} finally {
				PrefabUtility.UnloadPrefabContents(root);
			}
		}
		foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes) {
			var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
			bool changed = false;
			foreach (GameObject root in scene.GetRootGameObjects())
				changed |= Style(root, bold, icon, click, Zones.Title(scene.name));
			if (changed)
				EditorSceneManager.SaveScene(scene);
		}
		PlayerSettings.productName = "Robo Lac Loi";
		AssetDatabase.SaveAssets();
		Debug.Log("UiTheme: done");
	}

	static Sprite CoreIcon() {
		string path = "Assets/ThirdParty/RoboLacLoi/Sprites/core_icon.png";
		TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
		if (importer.textureType != TextureImporterType.Sprite) {
			importer.textureType = TextureImporterType.Sprite;
			importer.alphaIsTransparency = true;
			importer.SaveAndReimport();
		}
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	/// <summary>Returns true when anything under root changed.</summary>
	static bool Style(GameObject root, Font font, Sprite icon, AudioClip click, string zoneTitle) {
		bool changed = false;

		foreach (Button button in root.GetComponentsInChildren<Button>(true).ToList()) {
			Text label = button.GetComponentInChildren<Text>(true);
			if (label != null && label.text.StartsWith("Open Github")) {
				Object.DestroyImmediate(button.gameObject);
				changed = true;
				continue;
			}
			StyleButton(button, click);
			changed = true;
		}

		foreach (Text text in root.GetComponentsInChildren<Text>(true)) {
			text.font = font;
			// Accent texts added below keep their own color when the theme is re-applied.
			if (text.name != "Zone Text" && text.name != "Lost Title")
				text.color = TextColor;
			foreach (KeyValuePair<string, string> pair in Strings) {
				if (text.text.StartsWith(pair.Key)) {
					text.text = pair.Value;
					break;
				}
			}
			changed = true;
		}

		// "Score Text" is GameManager.mainScoreDisplay in each level.
		foreach (Text score in root.GetComponentsInChildren<Text>(true).Where(t => t.name == "Score Text").ToList())
			changed |= DecorateScore(score, font, icon, zoneTitle);

		// Message boxes: dark panel with a cyan border so the light text stays readable.
		foreach (Image image in root.GetComponentsInChildren<Image>(true)) {
			if (image.name == "BoxBackground") {
				image.color = new Color(Panel.r, Panel.g, Panel.b, 0.92f);
				changed = true;
			} else if (image.name == "BoxBorder") {
				image.color = Cyan;
				changed = true;
			}
		}

		changed |= FitIntroCard(root);
		changed |= FitFinalScreen(root);

		Transform lost = root.transform.Find("Lost Title");
		if (lost != null)
			lost.GetComponent<Text>().color = LostColor;
		if (root.name == "GameOver Canvas" && lost == null) {
			Text title = NewText("Lost Title", root.transform, font, "MẤT KẾT NỐI", 64, LostColor);
			RectTransform rect = title.rectTransform;
			rect.anchorMin = new Vector2(0f, 0.62f);
			rect.anchorMax = new Vector2(1f, 0.82f);
			rect.offsetMin = rect.offsetMax = Vector2.zero;
			changed = true;
		}
		return changed;
	}

	static void StyleButton(Button button, AudioClip click) {
		Image image = button.GetComponent<Image>();
		if (image != null) {
			// The panel color lives in the ColorBlock so the selected state can be told apart.
			image.color = Color.white;
			// Unity's fake null breaks '??' on components, so check explicitly.
			Outline outline = button.GetComponent<Outline>();
			if (outline == null)
				outline = button.gameObject.AddComponent<Outline>();
			outline.effectColor = Cyan;
			outline.effectDistance = new Vector2(2, -2);
		}
		ColorBlock colors = button.colors;
		colors.normalColor = Panel;
		colors.highlightedColor = PanelSelected;
		colors.selectedColor = PanelSelected;
		colors.pressedColor = new Color(Cyan.r * 0.6f, Cyan.g * 0.6f, Cyan.b * 0.6f, 1f);
		button.colors = colors;

		if (click == null)
			return;
		AudioSource source = button.GetComponent<AudioSource>();
		if (source == null) {
			source = button.gameObject.AddComponent<AudioSource>();
			source.playOnAwake = false;
		}
		for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
			if (button.onClick.GetPersistentTarget(i) == source)
				return;
		UnityEventTools.AddObjectPersistentListener<AudioClip>(button.onClick, source.PlayOneShot, click);
	}

	// Each level's HUD is "Score Canvas" (a small panel at the top right) > "Score" > "Score Text".
	// The text itself has a zero-size rect, so the icon and zone name are laid out on the panel.
	static bool DecorateScore(Text score, Font font, Sprite icon, string zoneTitle) {
		RectTransform panel = score.transform.parent != null ? score.transform.parent.parent as RectTransform : null;
		if (panel == null || panel.Find("Core Icon") != null)
			return false;

		// Make room on the left of the panel for the icon and shift the number right.
		panel.sizeDelta += new Vector2(40f, 8f);
		panel.anchoredPosition -= new Vector2(20f, 4f);
		RectTransform number = (RectTransform)score.transform.parent;
		number.anchoredPosition += new Vector2(16f, 0f);

		GameObject go = new GameObject("Core Icon", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(panel, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
		rect.pivot = new Vector2(0f, 0.5f);
		rect.anchoredPosition = new Vector2(6f, 0f);
		rect.sizeDelta = new Vector2(panel.sizeDelta.y - 6f, panel.sizeDelta.y - 6f);
		Image image = go.GetComponent<Image>();
		image.sprite = icon;
		image.preserveAspect = true;

		if (zoneTitle.Length > 0) {
			Text zone = NewText("Zone Text", panel, font, zoneTitle.ToUpper(), 16, Cyan);
			RectTransform zoneRect = zone.rectTransform;
			zoneRect.anchorMin = zoneRect.anchorMax = new Vector2(1f, 0f);
			zoneRect.pivot = new Vector2(1f, 1f);
			zoneRect.anchoredPosition = new Vector2(0f, -4f);
			zoneRect.sizeDelta = new Vector2(320f, 24f);
			zone.alignment = TextAnchor.UpperRight;
		}
		return true;
	}

	// The intro card shows the zone name and goal on two lines inside a 460x210 box on an 800-wide canvas.
	static bool FitIntroCard(GameObject root) {
		Text intro = root.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "Intro Level Text");
		if (intro == null)
			return false;
		intro.rectTransform.sizeDelta = new Vector2(440f, 190f);
		intro.lineSpacing = 1f;
		// The text is set at runtime; size the font for the longest zone name and goal.
		string runtime = intro.text;
		intro.text = Zones.Title("Level2").ToUpperInvariant() + "\nTHU 100 LÕI NĂNG LƯỢNG";
		intro.fontSize = 40;
		ShrinkToFit(intro);
		intro.text = runtime;
		return true;
	}

	// Final victory screen (Level4): title above the thanks line, both within the canvas width, no overlap.
	static bool FitFinalScreen(GameObject root) {
		Text[] texts = root.GetComponentsInChildren<Text>(true);
		Text title = texts.FirstOrDefault(t => t.text.StartsWith("ĐÃ VỀ"));
		Text thanks = texts.FirstOrDefault(t => t.text.StartsWith("Cảm ơn"));
		if (title == null || thanks == null)
			return false;
		// In batch mode the canvas rect may not be laid out yet; fall back to the scaler's reference width.
		// Text.canvas is null inside prefab editing contents, so look the root canvas up directly.
		Canvas canvas = title.GetComponentsInParent<Canvas>(true).Last();
		float canvasWidth = ((RectTransform)canvas.transform).rect.width;
		CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
		if (canvasWidth < 100f && scaler != null)
			canvasWidth = scaler.referenceResolution.x;
		foreach (Text text in new[] { title, thanks })
			text.rectTransform.sizeDelta = new Vector2(canvasWidth * 0.9f, text.rectTransform.sizeDelta.y);
		thanks.rectTransform.sizeDelta = new Vector2(thanks.rectTransform.sizeDelta.x, Mathf.Max(thanks.rectTransform.sizeDelta.y, 150f));

		// Size the title to end just above the thanks text.
		// Canvas-local tops; world corners are degenerate in batch mode (canvas scale 0 before layout).
		float height = TopInCanvas(title.rectTransform, canvas.transform) - TopInCanvas(thanks.rectTransform, canvas.transform) - 10f;
		RectTransform rect = title.rectTransform;
		float newHeight = Mathf.Max(60f, height);
		// Keep the title's top edge where it is while changing its height.
		float top = rect.anchoredPosition.y + (1f - rect.pivot.y) * rect.sizeDelta.y;
		rect.sizeDelta = new Vector2(rect.sizeDelta.x, newHeight);
		rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top - (1f - rect.pivot.y) * newHeight);

		ShrinkToFit(title);
		ShrinkToFit(thanks);
		return true;
	}

	static float TopInCanvas(RectTransform rect, Transform canvas) {
		float y = rect.rect.yMax;
		for (Transform t = rect; t != null && t != canvas; t = t.parent)
			y += t.localPosition.y;
		return y;
	}

		// Largest font size (up to the current one) at which every line fits the rect without wrapping.
	static void ShrinkToFit(Text text) {
		Vector2 size = text.rectTransform.rect.size;
		text.resizeTextForBestFit = false;
		for (int fontSize = text.fontSize; fontSize > 20; fontSize -= 2) {
			TextGenerationSettings settings = text.GetGenerationSettings(size);
			settings.fontSize = fontSize;
			settings.horizontalOverflow = HorizontalWrapMode.Overflow;
			settings.verticalOverflow = VerticalWrapMode.Overflow;
			TextGenerator generator = new TextGenerator();
			if (generator.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit <= size.x
				&& generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit <= size.y) {
				text.fontSize = fontSize;
				return;
			}
		}
		text.fontSize = 20;
	}

	static Text NewText(string name, Transform parent, Font font, string value, int size, Color color) {
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, false);
		Text text = go.GetComponent<Text>();
		text.text = value;
		text.font = font;
		text.fontSize = size;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = color;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		return text;
	}
}
