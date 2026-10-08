using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds Assets/Scenes/Ending.unity: space backdrop and the completion screen (spec §8, no gameplay).
/// Plan 6 adds the spaceship cinematic in front of the completion panel. Run UiTheme.Apply afterwards.
/// </summary>
public static class EndingSceneBuilder {

	const string ScenePath = "Assets/Scenes/Ending.unity";

	[MenuItem("Tools/Robo Lac Loi/Build Ending Scene")]
	public static void Build() {
		var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
		Camera camera = Camera.main;
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = new Color32(5, 8, 16, 255);

		new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
		GameObject canvasGo = new GameObject("Ending Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
		CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;

		GameObject panel = new GameObject("Completion Panel", typeof(RectTransform));
		panel.transform.SetParent(canvasGo.transform, false);
		RectTransform panelRect = (RectTransform)panel.transform;
		panelRect.anchorMin = Vector2.zero;
		panelRect.anchorMax = Vector2.one;
		panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
		panel.SetActive(false);

		Font bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ChakraPetch/ChakraPetch-Bold.ttf");
		Font medium = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ChakraPetch/ChakraPetch-Medium.ttf");
		Label(panel.transform, "Title", "ROBO ĐÃ THOÁT KHỎI HÀNH TINH", bold, 84, 260f, new Vector2(1700f, 120f));
		Label(panel.transform, "Subtitle", "Hành trình tiếp tục.", medium, 48, 160f, new Vector2(1200f, 70f));

		EndingController controller = canvasGo.AddComponent<EndingController>();
		controller.completionPanel = panel;
		UnityEventTools.AddPersistentListener(Button(panel.transform, "Play Again Button", "CHƠI LẠI TỪ ĐẦU", bold, -40f).onClick, controller.PlayAgain);
		UnityEventTools.AddPersistentListener(Button(panel.transform, "Main Menu Button", "VỀ MENU", bold, -180f).onClick, controller.BackToMenu);

		EditorSceneManager.SaveScene(scene, ScenePath);
		if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
			EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
		Debug.Log("EndingSceneBuilder: done");
	}

	static Text Label(Transform parent, string name, string value, Font font, int size, float y, Vector2 box) {
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = box;
		rect.anchoredPosition = new Vector2(0f, y);
		Text text = go.GetComponent<Text>();
		text.text = value;
		text.font = font;
		text.fontSize = size;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		return text;
	}

	static Button Button(Transform parent, string name, string label, Font font, float y) {
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(460f, 110f);
		rect.anchoredPosition = new Vector2(0f, y);
		Text text = Label(go.transform, name.Replace("Button", "Text").Trim(), label, font, 48, 0f, new Vector2(420f, 90f));
		RectTransform textRect = text.rectTransform;
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = textRect.offsetMax = Vector2.zero;
		return go.GetComponent<Button>();
	}
}
