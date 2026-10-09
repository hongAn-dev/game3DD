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
		// Shield icon + seconds to the right of the bar (spec §6.2); hidden until a shield is active.
		RectTransform shield = Rect("Shield", bar, new Vector2(56f, 24f));
		shield.anchorMin = shield.anchorMax = new Vector2(1f, 0f);
		shield.pivot = new Vector2(0f, 0f);
		shield.anchoredPosition = new Vector2(2f, 0f);
		Image icon = Rect("Icon", shield, new Vector2(22f, 22f)).gameObject.AddComponent<Image>();
		((RectTransform)icon.transform).anchorMin = ((RectTransform)icon.transform).anchorMax = new Vector2(0f, 0.5f);
		((RectTransform)icon.transform).anchoredPosition = new Vector2(11f, 0f);
		icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ThirdParty/RoboLacLoi/Sprites/shield_icon.png");
		icon.preserveAspect = true;
		Text seconds = Rect("Seconds", shield, new Vector2(30f, 24f)).gameObject.AddComponent<Text>();
		((RectTransform)seconds.transform).anchorMin = ((RectTransform)seconds.transform).anchorMax = new Vector2(0f, 0.5f);
		((RectTransform)seconds.transform).anchoredPosition = new Vector2(40f, 0f);
		seconds.font = label.font;
		seconds.fontSize = 20;
		seconds.alignment = TextAnchor.MiddleLeft;
		seconds.color = new Color32(140, 190, 255, 255);
		seconds.text = "5";
		shield.gameObject.SetActive(false);
		health.shieldGroup = shield.gameObject;
		health.shieldSeconds = seconds;
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
