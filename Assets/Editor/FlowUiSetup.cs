using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spec §7 UI: builds the story dialog ("Story Canvas") in Level1..4 and the main menu, wires it to GameManager and
/// removes the old timed intro card. The canvas scales by height (1080 units on every phone), so the 1440-wide box
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
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
		}
		var menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
		Dialog();
		EditorSceneManager.MarkSceneDirty(menu);
		EditorSceneManager.SaveScene(menu);
		Debug.Log("FlowUiSetup: done");
	}

	static DialogPanel Dialog() {
		GameObject old = GameObject.Find("Story Canvas");
		if (old != null)
			Object.DestroyImmediate(old);
		GameObject root = Canvas("Story Canvas", 40);

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
		dialog.nextButton = Button("Next Button", dialog.box, 480f, 340f, "Tiếp theo", dialog.Next);
		dialog.startButton = Button("Start Button", dialog.box, 480f, 340f, "Bắt đầu", dialog.Finish);
		backdrop.gameObject.SetActive(false);
		return dialog;
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
