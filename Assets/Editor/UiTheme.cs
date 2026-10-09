using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sci-fi HUD look for all UI: Chakra Petch font (has Vietnamese glyphs), dark chamfered panels with cyan
/// corner brackets, color-coded difficulty chips, Vietnamese strings, energy core icon and zone name next to
/// the score, click sound, no GitHub button. Run with -executeMethod UiTheme.Apply
/// </summary>
public static class UiTheme {

	static readonly Color Panel = new Color32(14, 19, 26, 230);
	static readonly Color PanelSelected = new Color32(22, 92, 98, 240);
	static readonly Color Cyan = new Color32(46, 230, 230, 255);
	static readonly Color OverdriveYellow = new Color32(255, 214, 40, 255);
	static readonly Color TextColor = new Color32(240, 246, 250, 255);
	// Light slate: readable over the bright menu terrain with a drop shadow.
	static readonly Color Muted = new Color32(203, 213, 225, 255);
	static readonly Color Warm = new Color32(255, 196, 77, 255);
	// Danger red-orange, lightened to stay readable on the dark panel.
	static readonly Color LostColor = new Color32(255, 120, 80, 255);

	static readonly Dictionary<string, Color> DifficultyColors = new Dictionary<string, Color> {
		{ "Easy", new Color32(74, 222, 128, 255) },
		{ "Normal", new Color32(255, 196, 77, 255) },
		{ "Hard", new Color32(255, 106, 77, 255) },
	};

	// Old or current text (prefix match, case sensitive) -> new text. Longer keys come first.
	static readonly KeyValuePair<string, string>[] Strings = {
		new KeyValuePair<string, string>("LiBot Adventure", "ROBO LẠC LỐI"),
		new KeyValuePair<string, string>("Robo Lạc Lối", "ROBO LẠC LỐI"),
		new KeyValuePair<string, string>("Play Again", "THỬ LẠI"),
		new KeyValuePair<string, string>("Thử lại", "THỬ LẠI"),
		new KeyValuePair<string, string>("Play", "CHƠI"),
		new KeyValuePair<string, string>("Chơi", "CHƠI"),
		new KeyValuePair<string, string>("Quit", "THOÁT"),
		new KeyValuePair<string, string>("Thoát", "THOÁT"),
		new KeyValuePair<string, string>("Easy", "DỄ"),
		new KeyValuePair<string, string>("Dễ", "DỄ"),
		new KeyValuePair<string, string>("Normal", "THƯỜNG"),
		new KeyValuePair<string, string>("Thường", "THƯỜNG"),
		new KeyValuePair<string, string>("Hard", "KHÓ"),
		new KeyValuePair<string, string>("Khó", "KHÓ"),
		new KeyValuePair<string, string>("Level Victory!", "ĐỦ NĂNG LƯỢNG!"),
		new KeyValuePair<string, string>("Đủ năng lượng!", "ĐỦ NĂNG LƯỢNG!"),
		new KeyValuePair<string, string>("Main Menu", "MENU CHÍNH"),
		new KeyValuePair<string, string>("Menu chính", "MENU CHÍNH"),
		new KeyValuePair<string, string>("Next Level", "KHU TIẾP THEO"),
		new KeyValuePair<string, string>("Khu tiếp theo", "KHU TIẾP THEO"),
		new KeyValuePair<string, string>("CONGRATULATIONS!", "ĐÃ VỀ TỚI CĂN CỨ!"),
		new KeyValuePair<string, string>("Thanks for playing", "Cảm ơn bạn đã chơi!"),
	};

	class Fonts {
		public Font bold, semiBold, medium;
	}

	const string FontFolder = "Assets/Fonts/ChakraPetch/";
	const string SpriteFolder = "Assets/ThirdParty/RoboLacLoi/Sprites/";

	[MenuItem("Tools/Robo Lac Loi/Apply UI Theme")]
	public static void Apply() {
		var fonts = new Fonts {
			bold = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "ChakraPetch-Bold.ttf"),
			semiBold = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "ChakraPetch-SemiBold.ttf"),
			medium = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "ChakraPetch-Medium.ttf"),
		};
		Sprite icon = ImportSprite(SpriteFolder + "core_icon.png", Vector4.zero);
		Sprite fill = ImportSprite(SpriteFolder + "hud_fill.png", new Vector4(24, 24, 24, 24));
		Sprite brackets = ImportSprite(SpriteFolder + "hud_brackets.png", new Vector4(24, 24, 24, 24));
		var theme = new Theme { fonts = fonts, icon = icon, fill = fill, brackets = brackets };

		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })) {
			string path = AssetDatabase.GUIDToAssetPath(guid);
			GameObject root = PrefabUtility.LoadPrefabContents(path);
			try {
				if (Style(root, theme, ""))
					PrefabUtility.SaveAsPrefabAsset(root, path);
			} finally {
				PrefabUtility.UnloadPrefabContents(root);
			}
		}
		foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes) {
			var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
			bool changed = false;
			foreach (GameObject root in scene.GetRootGameObjects()) {
				changed |= Style(root, theme, LevelCatalog.Get(scene.name) != null ? LevelCatalog.Get(scene.name).displayName : "");
				// Values set from script on prefab instances only stick once recorded as overrides.
				foreach (Component component in root.GetComponentsInChildren<Component>(true))
					if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
						PrefabUtility.RecordPrefabInstancePropertyModifications(component);
			}
			if (changed)
				EditorSceneManager.SaveScene(scene);
		}
		PlayerSettings.productName = "Robo Lac Loi";
		AssetDatabase.SaveAssets();
		Debug.Log("UiTheme: done");
	}

	class Theme {
		public Fonts fonts;
		public Sprite icon, fill, brackets;
	}

	static Sprite ImportSprite(string path, Vector4 border) {
		TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
		if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border) {
			importer.textureType = TextureImporterType.Sprite;
			importer.alphaIsTransparency = true;
			importer.mipmapEnabled = false;
			importer.spriteBorder = border;
			importer.SaveAndReimport();
		}
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	/// <summary>Returns true when anything under root changed.</summary>
	static bool Style(GameObject root, Theme theme, string zoneTitle) {
		bool changed = false;

		if (root.name == "GameOver Canvas" && root.transform.Find("Lost Title") == null) {
			NewText("Lost Title", root.transform, theme.fonts.bold, "MẤT KẾT NỐI", 72, LostColor);
			changed = true;
		}

		foreach (Button button in root.GetComponentsInChildren<Button>(true).ToList()) {
			Text label = button.GetComponentInChildren<Text>(true);
			if (label != null && label.text.StartsWith("Open Github")) {
				Object.DestroyImmediate(button.gameObject);
				changed = true;
			}
		}

		// Strings, fonts and colors first: the layout below measures the final text.
		foreach (Text text in root.GetComponentsInChildren<Text>(true)) {
			StyleText(text, theme.fonts);
			changed = true;
		}

		changed |= LayoutMainMenu(root, theme.fonts);
		changed |= LayoutMessageBoxes(root);

		foreach (Button button in root.GetComponentsInChildren<Button>(true)) {
			StyleButton(button, theme);
			changed = true;
		}

		// "Score Text" is GameManager.mainScoreDisplay in each level.
		foreach (Text score in root.GetComponentsInChildren<Text>(true).Where(t => t.name == "Score Text").ToList())
			changed |= DecorateScore(score, theme, zoneTitle);

		// Message boxes: dark chamfered panel with cyan corner brackets drawn on top of it.
		foreach (Image image in root.GetComponentsInChildren<Image>(true).ToList()) {
			if (image.name == "BoxBackground") {
				SetSliced(image, theme.fill, Panel);
				changed = true;
			} else if (image.name == "BoxBorder") {
				SetSliced(image, theme.brackets, Cyan);
				Transform background = image.transform.parent.Find("BoxBackground");
				if (background != null && image.transform.GetSiblingIndex() < background.GetSiblingIndex())
					image.transform.SetSiblingIndex(background.GetSiblingIndex());
				changed = true;
			}
		}

		changed |= FitFinalScreen(root);
		return changed;
	}

	static void SetCenter(Transform t, float y, Vector2 size) {
		RectTransform rect = (RectTransform)t;
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
		rect.localScale = Vector3.one;
		rect.sizeDelta = size;
		rect.anchoredPosition = new Vector2(0f, y);
	}

	// Shrink a box text for a sample (worst case) value, then restore the real one.
	static void FitFor(Transform t, string sample, int maxSize) {
		Text text = t.GetComponent<Text>();
		string real = text.text;
		text.text = sample ?? real;
		text.fontSize = maxSize;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		text.alignment = TextAnchor.MiddleCenter;
		ShrinkToFit(text);
		text.text = real;
	}

	// Win and lose boxes (1920x1080 reference): fixed column layout, everything centered inside the box.
	static bool LayoutMessageBoxes(GameObject root) {
		Transform box = root.transform.Find("BoxBackground");
		if (box == null || (root.name != "BeatLevelUICanvas" && root.name != "GameOver Canvas"))
			return false;
		Transform border = root.transform.Find("BoxBorder");

		if (root.name == "BeatLevelUICanvas") {
			SetCenter(box, 0f, new Vector2(820f, 440f));
			Transform title = root.transform.Find("Congratulations Text");
			SetCenter(title, 80f, new Vector2(740f, 150f));
			FitFor(title, null, 110);
			SetCenter(root.transform.Find("Next Level Button"), -120f, new Vector2(460f, 110f));
		} else {
			SetCenter(box, 0f, new Vector2(820f, 580f));
			Transform lost = root.transform.Find("Lost Title");
			SetCenter(lost, 200f, new Vector2(740f, 90f));
			FitFor(lost, null, 72);
			Transform score = root.transform.Find("EndGameScore Text");
			SetCenter(score, 95f, new Vector2(740f, 100f));
			FitFor(score, "100 / 100", 96);
			SetCenter(root.transform.Find("Play Again Button"), -45f, new Vector2(460f, 100f));
			SetCenter(root.transform.Find("Main Menu Button"), -170f, new Vector2(460f, 100f));
		}
		if (border != null)
			SetCenter(border, 0f, ((RectTransform)box).sizeDelta);
		return true;
	}

	static void StyleText(Text text, Fonts fonts) {
		foreach (KeyValuePair<string, string> pair in Strings) {
			if (text.text.StartsWith(pair.Key)) {
				text.text = pair.Value;
				break;
			}
		}

		bool heading = text.name == "Game Title" || text.name == "Congratulations Text" || text.name == "Lost Title"
			|| text.name == "Score Text" || text.name == "Dialog Title" || text.name == "EndGameScore Text";
		text.font = heading ? fonts.bold : text.name == "Subtitle" ? fonts.medium : fonts.semiBold;

		Color color = TextColor;
		string difficulty = DifficultyOf(text.transform);
		if (difficulty != null)
			color = DifficultyColors[difficulty];
		else if (text.name == "Zone Text" || text.name == "Dialog Title")
			color = Cyan;
		else if (text.transform.parent != null && text.transform.parent.parent != null && text.transform.parent.parent.name == "Overdrive Indicator")
			color = OverdriveYellow;
		else if (text.name == "Subtitle")
			color = Muted;
		else if (text.name == "Lost Title")
			color = LostColor;
		else if (text.text.StartsWith("ĐÃ VỀ"))
			color = Warm;
		text.color = color;

		Outline glow = text.GetComponent<Outline>();
		if (text.name == "Game Title") {
			if (glow == null)
				glow = text.gameObject.AddComponent<Outline>();
			glow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f);
			glow.effectDistance = new Vector2(3f, -3f);
		} else if (glow != null) {
			Object.DestroyImmediate(glow);
		}
	}

	// "Easy Button", "Easy Mode Canvas", "Easy Text"... -> "Easy"
	static string DifficultyOf(Transform t) {
		for (; t != null; t = t.parent)
			foreach (string key in DifficultyColors.Keys)
				if (t.name.StartsWith(key + " "))
					return key;
		return null;
	}

	static void SetSliced(Image image, Sprite sprite, Color color) {
		image.sprite = sprite;
		image.type = Image.Type.Sliced;
		image.fillCenter = true;
		image.color = color;
	}

	// Cyan (or difficulty-colored) corner brackets stretched over target, under its text.
	static void AddFrame(RectTransform target, Sprite brackets, Color color) {
		Transform existing = target.Find("HUD Frame");
		GameObject go = existing != null ? existing.gameObject : new GameObject("HUD Frame", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(target, false);
		go.transform.SetAsFirstSibling();
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = rect.offsetMax = Vector2.zero;
		Image image = go.GetComponent<Image>();
		SetSliced(image, brackets, color);
		image.raycastTarget = false;
	}

	static void StyleButton(Button button, Theme theme) {
		string difficulty = DifficultyOf(button.transform);
		Color accent = difficulty != null ? DifficultyColors[difficulty] : Cyan;

		Image image = button.GetComponent<Image>();
		if (image != null) {
			// The panel color lives in the ColorBlock so the selected state can be told apart.
			SetSliced(image, theme.fill, Color.white);
			Outline outline = button.GetComponent<Outline>();
			if (outline != null)
				Object.DestroyImmediate(outline);
		}
		AddFrame((RectTransform)button.transform, theme.brackets, accent);

		// A uniformly scaled button (e.g. the standalone "Main Menu Button" at 2.3x) gets its scale folded into
		// its size, so the label is measured at the size it is shown.
		RectTransform buttonRect = (RectTransform)button.transform;
		Vector3 scale = buttonRect.localScale;
		if (scale != Vector3.one && Mathf.Approximately(scale.x, scale.y) && buttonRect.anchorMin == buttonRect.anchorMax) {
			buttonRect.sizeDelta *= scale.x;
			buttonRect.localScale = Vector3.one;
		}

		// Label fills the button with padding and shrinks until the longest line fits.
		foreach (Text label in button.GetComponentsInChildren<Text>(true)) {
			RectTransform rect = label.rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = new Vector2(18f, 8f);
			rect.offsetMax = new Vector2(-18f, -8f);
			label.alignment = TextAnchor.MiddleCenter;
			label.horizontalOverflow = HorizontalWrapMode.Wrap;
			label.verticalOverflow = VerticalWrapMode.Truncate;
			label.fontSize = Mathf.Min(label.fontSize, 56);
			ShrinkToFit(label);
		}

		ColorBlock colors = button.colors;
		colors.normalColor = Panel;
		colors.highlightedColor = PanelSelected;
		colors.selectedColor = PanelSelected;
		colors.pressedColor = new Color(Cyan.r * 0.6f, Cyan.g * 0.6f, Cyan.b * 0.6f, 1f);
		colors.fadeDuration = 0.12f;
		button.colors = colors;

		// One soft tick from the SFX catalog (muted with SFX); the old per-button AudioSource and its listener go.
		for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
			if (button.onClick.GetPersistentTarget(i) is AudioSource)
				UnityEventTools.RemovePersistentListener(button.onClick, i);
		AudioSource old = button.GetComponent<AudioSource>();
		if (old != null)
			Object.DestroyImmediate(old, true);
		ClickSound click = button.GetComponent<ClickSound>();
		if (click == null)
			click = button.gameObject.AddComponent<ClickSound>();
		for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
			if (button.onClick.GetPersistentTarget(i) == click)
				return;
		UnityEventTools.AddVoidPersistentListener(button.onClick, click.Play);
	}

	// Main menu (1920x1080 reference): title, subtitle, and stacked buttons without the old GitHub gap.
	// The difficulty choice reuses the same column.
	static bool LayoutMainMenu(GameObject root, Fonts fonts) {
		bool changed = false;
		Transform titleT = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Game Title");
		if (titleT != null) {
			Text title = titleT.GetComponent<Text>();
			title.fontSize = 170;
			title.resizeTextForBestFit = false;
			Transform subtitleT = titleT.parent.Find("Subtitle");
			Text subtitle = subtitleT != null ? subtitleT.GetComponent<Text>()
				: NewText("Subtitle", titleT.parent, fonts.medium, "HÀNH TRÌNH VỀ CĂN CỨ", 44, Muted);
			RectTransform rect = subtitle.rectTransform;
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(1200f, 60f);
			rect.anchoredPosition = new Vector2(0f, 105f);
			Shadow shadow = subtitle.GetComponent<Shadow>();
			if (shadow == null)
				shadow = subtitle.gameObject.AddComponent<Shadow>();
			shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
			shadow.effectDistance = new Vector2(2f, -2f);
			changed = true;
		}
		changed |= Place(root, "Play Button", 0f, new Vector2(440f, 110f));
		changed |= Place(root, "Quit Button", -140f, new Vector2(440f, 110f));
		changed |= Place(root, "Easy Button", 140f, new Vector2(440f, 110f), "DifficultCanvas");
		changed |= Place(root, "Normal Button", 0f, new Vector2(440f, 110f), "DifficultCanvas");
		changed |= Place(root, "Hard Button", -140f, new Vector2(440f, 110f), "DifficultCanvas");
		return changed;
	}

	static bool Place(GameObject root, string name, float y, Vector2 size, string parentName = null) {
		bool changed = false;
		foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name)) {
			if (parentName != null && (t.parent == null || t.parent.name != parentName))
				continue;
			RectTransform rect = (RectTransform)t;
			// Only the menu's centered buttons; HUD badges with the same names are anchored top-left.
			if (rect.anchorMin != new Vector2(0.5f, 0.5f))
				continue;
			rect.sizeDelta = size;
			rect.anchoredPosition = new Vector2(0f, y);
			Text label = t.GetComponentInChildren<Text>(true);
			if (label != null)
				label.fontSize = 56;
			changed = true;
		}
		return changed;
	}

	// Each level's HUD is "Score Canvas" (a small panel at the top right) > "Score" > "Score Text".
	// The text itself has a zero-size rect, so the icon and zone name are laid out on the panel.
	static bool DecorateScore(Text score, Theme theme, string zoneTitle) {
		RectTransform panel = score.transform.parent != null ? score.transform.parent.parent as RectTransform : null;
		if (panel == null)
			return false;

		Image panelImage = panel.GetComponent<Image>();
		if (panelImage != null)
			SetSliced(panelImage, theme.fill, Panel);
		AddFrame(panel, theme.brackets, Cyan);
		// The inner number bar is replaced by the panel itself.
		Image inner = score.transform.parent.GetComponent<Image>();
		if (inner != null)
			inner.color = Color.clear;

		if (panel.Find("Core Icon") == null) {
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
			rect.anchoredPosition = new Vector2(8f, 0f);
			rect.sizeDelta = new Vector2(panel.sizeDelta.y - 6f, panel.sizeDelta.y - 6f);
			Image image = go.GetComponent<Image>();
			image.sprite = theme.icon;
			image.preserveAspect = true;
		}

		Transform zoneT = panel.Find("Zone Text");
		if (zoneTitle.Length > 0 && zoneT == null) {
			Text zone = NewText("Zone Text", panel, theme.fonts.semiBold, zoneTitle.ToUpperInvariant(), 17, Cyan);
			zoneT = zone.transform;
		}
		if (zoneT != null) {
			// Re-runs keep the label in step with LevelConfig (zone names changed in T4).
			if (zoneTitle.Length > 0)
				zoneT.GetComponent<Text>().text = zoneTitle.ToUpperInvariant();
			RectTransform zoneRect = (RectTransform)zoneT;
			zoneRect.anchorMin = zoneRect.anchorMax = new Vector2(1f, 0f);
			zoneRect.pivot = new Vector2(1f, 1f);
			zoneRect.anchoredPosition = new Vector2(-4f, -6f);
			zoneRect.sizeDelta = new Vector2(320f, 24f);
			zoneT.GetComponent<Text>().alignment = TextAnchor.UpperRight;
		}
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
