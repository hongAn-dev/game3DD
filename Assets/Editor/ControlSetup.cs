using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires the spec §7 controls into Level1..4: orbit camera on the Main Camera (minimap keeps a fixed SmoothFollow),
/// safe-area joystick + right-half look area + pause button on the mobile canvas, a pause overlay canvas, the robot
/// tuning (11 m/s, acceleration 24, brake 30, no torque; no per-scene overrides) with Rigidbody interpolation, and no
/// raycasts on decorative graphics (they would swallow camera swipes). Idempotent. Run UiTheme.Apply afterwards.
/// </summary>
public static class ControlSetup {

	[MenuItem("Tools/Robo Lac Loi/Apply Control Setup")]
	public static void Apply() {
		GameObject contents = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
		try {
			contents.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.Interpolate;
			// 100 HP, one life per level (spec §6.1).
			Health health = contents.GetComponent<Health>();
			health.maxHealth = health.healthPoints = health.respawnHealthPoints = 100f;
			health.numberOfLives = 1;
			var ball = new SerializedObject(contents.GetComponent<Ball>());
			ball.FindProperty("m_MaxSpeed").floatValue = 11f;
			ball.FindProperty("m_AccelerationRate").floatValue = 24f;
			ball.FindProperty("m_Brake").floatValue = 30f;
			ball.FindProperty("m_UseTorque").boolValue = false;
			ball.ApplyModifiedPropertiesWithoutUndo();
			PrefabUtility.SaveAsPrefabAsset(contents, "Assets/Prefabs/Player.prefab");
		} finally {
			PrefabUtility.UnloadPrefabContents(contents);
		}

		foreach (LevelConfig level in LevelCatalog.All) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			PauseController pause = PauseCanvas();
			StyleSlider(pause.sensitivitySlider);
			OrbitCamera();
			MobileCanvas(pause);
			RobotUsesPrefabTuning();
			DecorationsIgnoreRays();
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log("ControlSetup: " + level.levelId);
		}
		Debug.Log("ControlSetup: done");
	}

	static void OrbitCamera() {
		Camera main = Object.FindObjectsOfType<Camera>(true).First(c => c.CompareTag("MainCamera"));
		GameObject player = GameObject.FindWithTag("Player");
		ThirdPersonOrbitCamera orbit = main.GetComponent<ThirdPersonOrbitCamera>();
		SmoothFollow follow = main.GetComponent<SmoothFollow>();
		if (orbit == null) {
			float distance = 6f, height = 5f;
			if (follow != null) {
				var so = new SerializedObject(follow);
				distance = so.FindProperty("distance").floatValue;
				height = so.FindProperty("height").floatValue;
			}
			orbit = main.gameObject.AddComponent<ThirdPersonOrbitCamera>();
			orbit.distance = Mathf.Sqrt(distance * distance + height * height);
			orbit.pitch = Mathf.Clamp(Mathf.Atan2(height, distance) * Mathf.Rad2Deg, orbit.minPitch, orbit.maxPitch);
			orbit.yaw = main.transform.eulerAngles.y;
		}
		if (follow != null)
			Object.DestroyImmediate(follow);
		orbit.target = player.transform;
		foreach (SmoothFollow other in Object.FindObjectsOfType<SmoothFollow>(true))
			other.AllowUserInput = false;
	}

	static readonly string[] Tuning = { "m_MaxSpeed", "m_AccelerationRate", "m_Brake", "m_UseTorque" };
	static readonly string[] HealthFields = { "maxHealth", "healthPoints", "respawnHealthPoints", "numberOfLives" };

	// Scenes must not override the robot tuning or HP, or prefab changes silently do nothing there.
	static void RobotUsesPrefabTuning() {
		GameObject player = GameObject.FindWithTag("Player");
		Revert(new SerializedObject(player.GetComponent<Ball>()), Tuning);
		Revert(new SerializedObject(player.GetComponent<Health>()), HealthFields);
	}

	static void Revert(SerializedObject target, string[] names) {
		foreach (string name in names) {
			SerializedProperty property = target.FindProperty(name);
			if (property != null && property.prefabOverride)
				PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
		}
	}

	static bool Interactive(Transform t) {
		for (; t != null; t = t.parent)
			foreach (MonoBehaviour b in t.GetComponents<MonoBehaviour>())
				if (b is Selectable || b is UnityEngine.EventSystems.IEventSystemHandler)
					return true;
		return false;
	}

	// Only buttons, sliders, the joystick and the look area catch touches; texts, frames and the minimap do not.
	static void DecorationsIgnoreRays() {
		foreach (Graphic graphic in Object.FindObjectsOfType<Graphic>(true))
			if (graphic.raycastTarget && !Interactive(graphic.transform)) {
				graphic.raycastTarget = false;
				if (PrefabUtility.IsPartOfPrefabInstance(graphic))
					PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
			}
	}

	// Unity's fake null breaks '??' on components, so check explicitly.
	static T GetOrAdd<T>(GameObject go) where T : Component {
		T component = go.GetComponent<T>();
		return component != null ? component : go.AddComponent<T>();
	}

	static RectTransform Child(Transform parent, string name) {
		Transform existing = parent.Find(name);
		if (existing != null)
			return (RectTransform)existing;
		GameObject go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Stretch(RectTransform rect, Vector2 min, Vector2 max) {
		rect.localScale = Vector3.one;
		rect.anchorMin = min;
		rect.anchorMax = max;
		rect.offsetMin = rect.offsetMax = Vector2.zero;
	}

	static void MobileCanvas(PauseController pause) {
		FixedJoystick joystick = Object.FindObjectsOfType<FixedJoystick>(true).Single();
		RectTransform mobile = (RectTransform)Object.FindObjectsOfType<RectTransform>(true).First(r => r.name == "Mobile Canvas");
		// Fill the parent canvas (it used to be a scaled 1920x1080 box that did not cover the screen).
		Stretch(mobile, Vector2.zero, Vector2.one);

		RectTransform look = Child(mobile, "Look Area");
		Stretch(look, new Vector2(0.5f, 0f), Vector2.one);
		look.SetAsFirstSibling();
		Image lookImage = GetOrAdd<Image>(look.gameObject);
		lookImage.color = new Color(0f, 0f, 0f, 0f);
		lookImage.raycastTarget = true;
		if (look.GetComponent<TouchLookArea>() == null)
			look.gameObject.AddComponent<TouchLookArea>();

		RectTransform safe = Child(mobile, "Safe Area");
		Stretch(safe, Vector2.zero, Vector2.one);
		safe.SetAsLastSibling();
		if (safe.GetComponent<SafeAreaFitter>() == null)
			safe.gameObject.AddComponent<SafeAreaFitter>();

		// Joystick bottom-left, about 22 % of the screen height (the parent canvas is 600 units tall).
		RectTransform stick = (RectTransform)joystick.transform;
		stick.SetParent(safe, false);
		stick.localScale = Vector3.one;
		stick.anchorMin = stick.anchorMax = Vector2.zero;
		stick.pivot = new Vector2(0.5f, 0.5f);
		stick.sizeDelta = new Vector2(132f, 132f);
		stick.anchoredPosition = new Vector2(96f, 96f);
		// Sized at runtime to ~22 % of the real canvas height (it varies with the aspect ratio); the handle follows.
		GetOrAdd<ScreenShareSizer>(stick.gameObject).heightShare = 0.22f;
		joystick.handle.anchorMin = new Vector2(0.29f, 0.29f);
		joystick.handle.anchorMax = new Vector2(0.71f, 0.71f);
		joystick.handle.sizeDelta = Vector2.zero;

		RectTransform buttonRect = Child(safe, "Pause Button");
		buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 1f);
		buttonRect.pivot = new Vector2(0f, 1f);
		buttonRect.sizeDelta = new Vector2(76f, 56f);
		buttonRect.anchoredPosition = new Vector2(12f, -58f);
		Image buttonImage = GetOrAdd<Image>(buttonRect.gameObject);
		Button button = GetOrAdd<Button>(buttonRect.gameObject);
		Text label = Label(buttonRect, "Pause Text", "II", 28);
		Stretch(label.rectTransform, Vector2.zero, Vector2.one);
		if (button.onClick.GetPersistentEventCount() == 0)
			UnityEventTools.AddPersistentListener(button.onClick, pause.Toggle);
	}

	static Text Label(Transform parent, string name, string value, int size) {
		RectTransform rect = Child(parent, name);
		Text text = GetOrAdd<Text>(rect.gameObject);
		text.text = value;
		text.fontSize = size;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ChakraPetch/ChakraPetch-SemiBold.ttf");
		return text;
	}

	static PauseController PauseCanvas() {
		PauseController existing = Object.FindObjectsOfType<PauseController>(true).FirstOrDefault();
		if (existing != null)
			return existing;

		GameObject root = new GameObject("Pause Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		Canvas canvas = root.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 50;
		CanvasScaler scaler = root.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;

		RectTransform overlay = Child(root.transform, "Pause Overlay");
		Stretch(overlay, Vector2.zero, Vector2.one);
		overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

		RectTransform box = Child(overlay, "BoxBackground");
		box.sizeDelta = new Vector2(820f, 620f);
		box.gameObject.AddComponent<Image>();
		RectTransform border = Child(overlay, "BoxBorder");
		border.sizeDelta = box.sizeDelta;
		border.gameObject.AddComponent<Image>();

		Text title = Label(overlay, "Pause Title", "TẠM DỪNG", 72);
		Place(title.rectTransform, 220f, new Vector2(740f, 100f));
		Text note = Label(overlay, "Pause Note", "Về menu sẽ bắt đầu lượt mới.", 30);
		Place(note.rectTransform, -265f, new Vector2(740f, 44f));
		Text sliderLabel = Label(overlay, "Sensitivity Label", "ĐỘ NHẠY CAMERA", 30);
		Place(sliderLabel.rectTransform, 130f, new Vector2(740f, 44f));

		GameObject sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
		sliderGo.name = "Sensitivity Slider";
		sliderGo.transform.SetParent(overlay, false);
		Place((RectTransform)sliderGo.transform, 80f, new Vector2(600f, 30f));
		Slider slider = sliderGo.GetComponent<Slider>();
		slider.minValue = 0.5f;
		slider.maxValue = 2f;
		slider.value = 1f;

		PauseController pause = root.AddComponent<PauseController>();
		pause.overlay = overlay.gameObject;
		pause.sensitivitySlider = slider;
		UnityEventTools.AddPersistentListener(slider.onValueChanged, pause.SetSensitivity);
		UnityEventTools.AddPersistentListener(MenuButton(overlay, "Resume Button", "TIẾP TỤC", -40f).onClick, pause.Resume);
		UnityEventTools.AddPersistentListener(MenuButton(overlay, "Main Menu Button", "MENU CHÍNH", -170f).onClick, pause.ToMenu);
		overlay.gameObject.SetActive(false);
		return pause;
	}

	// Touch-sized slider (80 tall, 64 px handle) with a visible cyan fill; DefaultControls gives plain white parts.
	static void StyleSlider(Slider slider) {
		((RectTransform)slider.transform).sizeDelta = new Vector2(600f, 80f);
		Image background = slider.transform.Find("Background").GetComponent<Image>();
		background.color = new Color32(40, 48, 58, 255);
		RectTransform backgroundRect = background.rectTransform;
		backgroundRect.anchorMin = new Vector2(0f, 0.4f);
		backgroundRect.anchorMax = new Vector2(1f, 0.6f);
		slider.fillRect.GetComponent<Image>().color = new Color32(46, 230, 230, 255);
		((RectTransform)slider.fillRect.parent).anchorMin = new Vector2(0f, 0.4f);
		((RectTransform)slider.fillRect.parent).anchorMax = new Vector2(1f, 0.6f);
		slider.handleRect.sizeDelta = new Vector2(64f, 0f);
		slider.handleRect.GetComponent<Image>().color = new Color32(240, 246, 250, 255);
	}

	static void Place(RectTransform rect, float y, Vector2 size) {
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = size;
		rect.anchoredPosition = new Vector2(0f, y);
	}

	static Button MenuButton(Transform parent, string name, string label, float y) {
		RectTransform rect = Child(parent, name);
		Place(rect, y, new Vector2(460f, 100f));
		rect.gameObject.AddComponent<Image>();
		Button button = rect.gameObject.AddComponent<Button>();
		Text text = Label(rect, name.Replace("Button", "Text").Trim(), label, 48);
		Stretch(text.rectTransform, Vector2.zero, Vector2.one);
		return button;
	}
}
