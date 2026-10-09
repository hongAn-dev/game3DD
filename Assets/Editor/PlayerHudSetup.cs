using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds "Robo HUD Canvas" (screen-space overlay, 1920x1080 reference) with the robot's HP bar (spec §6.2) to every
/// level: an 80 x 7 filled bar and the "HP/100" number above it, no raycasts. Re-runnable. Run UiTheme.Apply after.
/// </summary>
public static class PlayerHudSetup {

	[MenuItem("Tools/Robo Lac Loi/Apply Player HUD Setup")]
	public static void Apply() {
		foreach (LevelConfig level in LevelCatalog.All) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			GameObject old = GameObject.Find("Robo HUD Canvas");
			if (old != null)
				Object.DestroyImmediate(old);
			Build();
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
		}
		Debug.Log("PlayerHudSetup: done");
	}

	static void Build() {
		GameObject root = new GameObject("Robo HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
		Canvas canvas = root.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = -1;   // drawn before the HUD/mobile canvases (order 0), dialogs and the pause overlay
		CanvasScaler scaler = root.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		PlayerHealthBar health = root.AddComponent<PlayerHealthBar>();

		RectTransform bar = Rect("Robo Health Bar", root.transform, new Vector2(88f, 34f));
		bar.pivot = new Vector2(0.5f, 0f);   // the bar's bottom edge sits at the point above the robot
		Image back = Rect("Back", bar, new Vector2(80f, 7f)).gameObject.AddComponent<Image>();
		((RectTransform)back.transform).anchorMin = ((RectTransform)back.transform).anchorMax = new Vector2(0.5f, 0f);
		((RectTransform)back.transform).anchoredPosition = new Vector2(0f, 3.5f);
		back.color = new Color32(20, 24, 30, 200);
		Image fill = Rect("Fill", back.transform, new Vector2(80f, 7f)).gameObject.AddComponent<Image>();
		fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
		fill.type = Image.Type.Filled;
		fill.fillMethod = Image.FillMethod.Horizontal;
		fill.fillOrigin = (int)Image.OriginHorizontal.Left;
		fill.color = PlayerHealthBar.Green;
		Text label = Rect("Value", bar, new Vector2(88f, 24f)).gameObject.AddComponent<Text>();
		((RectTransform)label.transform).anchorMin = ((RectTransform)label.transform).anchorMax = new Vector2(0.5f, 0f);
		((RectTransform)label.transform).anchoredPosition = new Vector2(0f, 22f);
		label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ChakraPetch/ChakraPetch-Bold.ttf");
		label.fontSize = 20;
		label.alignment = TextAnchor.MiddleCenter;
		label.color = Color.white;
		label.text = "100/100";
		foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
			g.raycastTarget = false;
		health.bar = bar;
		health.fill = fill;
		health.label = label;
	}

	static RectTransform Rect(string name, Transform parent, Vector2 size) {
		GameObject go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = size;
		return rect;
	}
}
