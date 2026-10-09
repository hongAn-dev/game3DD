using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spec §7 UI: builds the story dialog ("Story Canvas") in Level1..4 and the main menu, wires it to GameManager and
/// removes the old timed intro card; builds the top-right bar ("Top Bar Canvas": sound toggle, plus the pause button in
/// levels) in the levels, the main menu and the Ending, and moves the energy panel to the top centre. The canvas scales by height (1080 units on every phone), so the 1440-wide box
/// fits 16:9 and wider screens; the body font is the largest (≤ 40, ≥ 32) at which every story page fits.
/// Rebuilt from scratch on every run. Run UiTheme.Apply afterwards for the panel and button look.
/// </summary>
public static class FlowUiSetup {

	const string Fonts = "Assets/Fonts/ChakraPetch/";

	[MenuItem("Tools/Robo Lac Loi/Apply Flow UI")]
	public static void Apply() {
		foreach (LevelConfig level in LevelCatalog.All) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			GameManager manager = Object.FindObjectOfType<GameManager>();
			// The old timed intro card (a prefab instance under the UI root).
			foreach (Transform t in Object.FindObjectsOfType<Transform>(true).Where(t => t != null && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)
				&& PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == "Assets/Prefabs/IntroBeatLevelCanvas.prefab").ToList())
				Object.DestroyImmediate(t.gameObject);
			manager.dialog = Dialog();
			TopBar(Object.FindObjectOfType<PauseController>(true));
			ScorePanelToTheCentre();
			manager.hideOnResult = new[] { Object.FindObjectsOfType<Camera>(true).Single(c => c.name == "Minimap Camera").gameObject,
				Object.FindObjectOfType<PlayerHealthBar>(true).gameObject };
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
		}
		var menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
		MenuPanels(Dialog());
		TopBar(null);
		EditorSceneManager.MarkSceneDirty(menu);
		EditorSceneManager.SaveScene(menu);
		var ending = EditorSceneManager.OpenScene("Assets/Scenes/Ending.unity", OpenSceneMode.Single);
		TopBar(null, -1);
		EditorSceneManager.MarkSceneDirty(ending);
		EditorSceneManager.SaveScene(ending);
		Debug.Log("FlowUiSetup: done");
	}

	static DialogPanel Dialog() {
		GameObject old = GameObject.Find("Story Canvas");
		if (old != null)
			Object.DestroyImmediate(old);
		GameObject root = Canvas("Story Canvas", 70);   // over the top bar: its buttons never sit on the dialog box

		RectTransform backdrop = Rect("Dialog", root.transform, Vector2.zero);
		Stretch(backdrop);
		backdrop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
		backdrop.gameObject.AddComponent<ModalBackdrop>();   // blocks touches behind (camera look, joystick)
		DialogPanel dialog = backdrop.gameObject.AddComponent<DialogPanel>();

		dialog.box = Rect("Box", backdrop, new Vector2(1440f, 860f));
		foreach (string name in new[] { "BoxBackground", "BoxBorder" }) {
			RectTransform part = Rect(name, dialog.box, Vector2.zero);
			Stretch(part);
			part.gameObject.AddComponent<Image>().raycastTarget = false;
		}
		dialog.title = Label("Dialog Title", dialog.box, new Vector2(0f, 330f), new Vector2(1300f, 100f), "Bold", 60);
		dialog.body = Label("Dialog Body", dialog.box, new Vector2(0f, 10f), new Vector2(1300f, 540f), "SemiBold", 40);
		dialog.body.alignment = TextAnchor.MiddleLeft;
		dialog.body.lineSpacing = 1.1f;
		dialog.body.fontSize = BodySize(dialog.body);

		dialog.backButton = Button("Back Button", dialog.box, -490f, 320f, "Quay lại", dialog.Back);
		dialog.skipButton = Button("Skip Button", dialog.box, -40f, 500f, "Bỏ qua hướng dẫn", dialog.Skip);
		dialog.nextButton = Button("Next Button", dialog.box, 480f, 340f, "Tiếp theo", dialog.PressNext);
		dialog.startButton = Button("Start Button", dialog.box, 480f, 340f, "Bắt đầu", dialog.PressFinish);
		backdrop.gameObject.SetActive(false);
		return dialog;
	}

	// Main menu: "Hướng dẫn chơi" and "Cài đặt" under "Chơi mới" (UiTheme lays the column out) and the settings panel.
	static void MenuPanels(DialogPanel dialog) {
		GameObject old = GameObject.Find("Settings Canvas");
		if (old != null)
			Object.DestroyImmediate(old);
		GameObject root = Canvas("Settings Canvas", 70);
		MainMenuPanels panels = root.AddComponent<MainMenuPanels>();
		panels.dialog = dialog;

		RectTransform backdrop = Rect("Settings", root.transform, Vector2.zero);
		Stretch(backdrop);
		backdrop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
		backdrop.gameObject.AddComponent<ModalBackdrop>();
		panels.settings = backdrop.gameObject;
		RectTransform box = Rect("Box", backdrop, new Vector2(1240f, 860f));
		foreach (string name in new[] { "BoxBackground", "BoxBorder" }) {
			RectTransform part = Rect(name, box, Vector2.zero);
			Stretch(part);
			part.gameObject.AddComponent<Image>().raycastTarget = false;
		}
		Label("Dialog Title", box, new Vector2(0f, 340f), new Vector2(1100f, 100f), "Bold", 60).text = "Cài đặt";
		panels.sensitivity = SettingRow(box, "Độ nhạy camera", 190f, 0.5f, 2f, panels.SetSensitivity);
		panels.music = SettingRow(box, "Nhạc nền", 60f, 0f, 1f, panels.SetMusic);
		panels.sfx = SettingRow(box, "Hiệu ứng âm thanh", -70f, 0f, 1f, panels.SetSfx);
		panels.ambience = SettingRow(box, "Âm thanh môi trường", -200f, 0f, 1f, panels.SetAmbience);
		Button("Close Button", box, 0f, 340f, "Đóng", panels.CloseSettings);
		backdrop.gameObject.SetActive(false);

		Transform play = Object.FindObjectsOfType<Button>(true).Single(x => x.name == "Play Button").transform;
		foreach (string name in new[] { "Guide Button", "Settings Button" }) {
			Transform existing = play.parent.Find(name);
			if (existing != null)
				Object.DestroyImmediate(existing.gameObject);
		}
		MenuButton("Guide Button", play.parent, "Hướng dẫn chơi", panels.OpenGuide);
		MenuButton("Settings Button", play.parent, "Cài đặt", panels.OpenSettings);
	}

	static Slider SettingRow(Transform box, string label, float y, float min, float max, UnityEngine.Events.UnityAction<float> action) {
		Text text = Label(label + " Label", box, new Vector2(-300f, y), new Vector2(500f, 70f), "SemiBold", 38);
		text.text = label;
		text.alignment = TextAnchor.MiddleLeft;
		GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
		go.name = label + " Slider";
		go.transform.SetParent(box, false);
		((RectTransform)go.transform).anchoredPosition = new Vector2(250f, y);
		Slider slider = go.GetComponent<Slider>();
		slider.minValue = min;
		slider.maxValue = max;
		slider.value = max;
		ControlSetup.StyleSlider(slider);
		UnityEventTools.AddPersistentListener(slider.onValueChanged, action);
		return slider;
	}

	static void MenuButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction action) {
		Button button = Button(name, parent, 0f, 520f, label, action);
		RectTransform rect = (RectTransform)button.transform;
		rect.sizeDelta = new Vector2(520f, 110f);
		rect.SetSiblingIndex(parent.Find("Play Button").GetSiblingIndex() + 1);
	}

	// Spec §7.6. The bar scales by height: 1080 units = the phone's short side, 360–411 dp, so 1 dp ≈ 3 units.
	// 144×144 touch areas (48 dp), 78-unit icons (26 dp), pause at the corner, sound 24 (8 dp) to its left, inside the
	// safe area, above the pause panel (60 > 50) so the sound toggle works while paused; dialogs (70) cover it.
	public const float UnitsPerDp = 3f, Touch = 48f * UnitsPerDp, Margin = 24f, Gap = 8f * UnitsPerDp, Icon = 26f * UnitsPerDp;

	/// <param name="pause">The level's pause controller, or null (menu, Ending: sound toggle only).</param>
	/// <param name="order">Canvas order; the Ending draws it under its own canvas so the fade-in covers it.</param>
	public static void TopBar(PauseController pause, int order = 60) {
		GameObject old = GameObject.Find("Top Bar Canvas");
		if (old != null)
			Object.DestroyImmediate(old);
		GameObject root = Canvas("Top Bar Canvas", order);
		RectTransform safe = Rect("Safe Area", root.transform, Vector2.zero);
		Stretch(safe);
		safe.gameObject.AddComponent<SafeAreaFitter>();

		Image soundIcon;
		Button sound = IconButton("Sound Button", safe, -(Margin + Touch + Gap), Sprite("sound_on"), out soundIcon);
		SoundToggle toggle = sound.gameObject.AddComponent<SoundToggle>();
		toggle.icon = soundIcon;
		toggle.soundOn = Sprite("sound_on");
		toggle.soundOff = Sprite("sound_off");
		RectTransform tip = Rect("Sound Tooltip", sound.transform, new Vector2(420f, 64f));
		tip.anchorMin = tip.anchorMax = tip.pivot = new Vector2(1f, 1f);
		tip.anchoredPosition = new Vector2(0f, -Touch - 8f);
		Image tipBack = tip.gameObject.AddComponent<Image>();
		tipBack.color = new Color32(14, 19, 26, 230);
		tipBack.raycastTarget = false;
		toggle.tooltip = tip.gameObject;
		toggle.tooltipText = Label("Tooltip Text", tip, Vector2.zero, new Vector2(400f, 60f), "SemiBold", 30);
		toggle.tooltipText.text = "Tắt hiệu ứng âm thanh";
		tip.gameObject.SetActive(false);
		// The toggle's listener first: muting happens before the click sound would play (UiTheme appends it).
		UnityEventTools.AddPersistentListener(sound.onClick, toggle.Toggle);

		if (pause != null) {
			Image pauseIcon;
			IconButton("Pause Button", safe, -Margin, Sprite("pause"), out pauseIcon);
			WirePauseButton(pause);
		}
	}

	/// <summary>Points the top bar's pause button at the level's PauseController (ControlSetup rebuilds that).</summary>
	public static void WirePauseButton(PauseController pause) {
		GameObject bar = GameObject.Find("Top Bar Canvas");
		Transform t = bar != null ? bar.transform.Find("Safe Area/Pause Button") : null;
		if (t == null)
			return;
		Button button = t.GetComponent<Button>();
		for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
			if (button.onClick.GetPersistentMethodName(i) == "Toggle")
				UnityEventTools.RemovePersistentListener(button.onClick, i);
		UnityEventTools.AddPersistentListener(button.onClick, pause.Toggle);
		pause.pauseButton = t.gameObject;
	}

	static Button IconButton(string name, Transform parent, float x, Sprite sprite, out Image icon) {
		RectTransform rect = Rect(name, parent, new Vector2(Touch, Touch));
		rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
		rect.anchoredPosition = new Vector2(x, -Margin);
		rect.gameObject.AddComponent<Image>();
		Button button = rect.gameObject.AddComponent<Button>();
		button.navigation = new Navigation { mode = Navigation.Mode.None };   // a mouse click must not leave it selected for Enter/Space
		RectTransform iconRect = Rect("Icon", rect, new Vector2(Icon, Icon));
		icon = iconRect.gameObject.AddComponent<Image>();
		icon.sprite = sprite;
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		return button;
	}

	static Sprite Sprite(string name) {
		string path = "Assets/ThirdParty/RoboLacLoi/Sprites/" + name + ".png";
		TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
		if (importer.textureType != TextureImporterType.Sprite) {
			importer.textureType = TextureImporterType.Sprite;
			importer.alphaIsTransparency = true;
			importer.mipmapEnabled = false;
			importer.SaveAndReimport();
		}
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	// The energy panel (MainCanvas, 800×600 reference) leaves the top-right corner to the buttons.
	static void ScorePanelToTheCentre() {
		RectTransform panel = (RectTransform)GameObject.Find("Score Canvas").transform;
		panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
		panel.anchoredPosition = new Vector2(0f, -22f);
		panel.sizeDelta = new Vector2(250f, panel.sizeDelta.y);
		if (PrefabUtility.IsPartOfPrefabInstance(panel))
			PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
	}

	// Largest body size from 40 down to 32 at which every page (tutorial, guide, level cards) fits the body rect.
	static int BodySize(Text body) {
		var pages = StoryText.Tutorial(LevelCatalog.First).Concat(StoryText.Guide()).Concat(LevelCatalog.All.Select(StoryText.LevelCard));
		for (int size = 40; size > 32; size--) {
			body.fontSize = size;
			TextGenerationSettings settings = body.GetGenerationSettings(body.rectTransform.sizeDelta);
			if (pages.All(p => new TextGenerator().GetPreferredHeight(p.body, settings) / body.pixelsPerUnit <= body.rectTransform.sizeDelta.y))
				return size;
		}
		return 32;
	}

	public static GameObject Canvas(string name, int order) {
		GameObject root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		Canvas canvas = root.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = order;
		CanvasScaler scaler = root.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 1f;
		return root;
	}

	public static RectTransform Rect(string name, Transform parent, Vector2 size) {
		RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
		rect.SetParent(parent, false);
		rect.sizeDelta = size;
		return rect;
	}

	static void Stretch(RectTransform rect) {
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = rect.offsetMax = Vector2.zero;
	}

	public static Text Label(string name, Transform parent, Vector2 position, Vector2 size, string weight, int fontSize) {
		RectTransform rect = Rect(name, parent, size);
		rect.anchoredPosition = position;
		Text text = rect.gameObject.AddComponent<Text>();
		text.font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "ChakraPetch-" + weight + ".ttf");
		text.fontSize = fontSize;
		text.alignment = TextAnchor.MiddleCenter;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.raycastTarget = false;
		text.color = Color.white;
		return text;
	}

	public static Button Button(string name, Transform parent, float x, float width, string label, UnityEngine.Events.UnityAction action) {
		RectTransform rect = Rect(name, parent, new Vector2(width, 100f));
		rect.anchoredPosition = new Vector2(x, -350f);
		rect.gameObject.AddComponent<Image>();
		Button button = rect.gameObject.AddComponent<Button>();
		Text text = Label(name.Replace("Button", "Text"), rect, Vector2.zero, Vector2.zero, "SemiBold", 40);
		text.rectTransform.anchorMin = Vector2.zero;
		text.rectTransform.anchorMax = Vector2.one;
		text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
		text.text = label;
		UnityEventTools.AddPersistentListener(button.onClick, action);
		return button;
	}
}
