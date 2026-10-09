using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine;

public class ControlTests {

	[Test]
	public void CameraRelativeMoveKeepsMagnitude() {
		Vector3 forward = Vector3.forward, right = Vector3.right;
		Assert.AreEqual(0.25f, BallUserControl.CameraRelative(0.25f, 0f, forward, right).magnitude, 0.001f);
		Assert.LessOrEqual(BallUserControl.CameraRelative(1f, 1f, forward, right).magnitude, 1.0001f);

		// A camera pitched down still yields a horizontal move of the stick's magnitude.
		Vector3 pitched = new Vector3(0f, -0.7f, 0.7f).normalized;
		Vector3 move = BallUserControl.CameraRelative(0f, 0.5f, pitched, right);
		Assert.AreEqual(0f, move.y, 0.0001f);
		Assert.AreEqual(0.5f, move.magnitude, 0.001f);
	}

	static PointerEventData Pointer(int id, Vector2 position, Vector2 delta) {
		return new PointerEventData(null) { pointerId = id, position = position, delta = delta };
	}

	static FixedJoystick MakeJoystick() {
		GameObject root = new GameObject("Joystick", typeof(RectTransform));
		GameObject handle = new GameObject("Handle", typeof(RectTransform));
		handle.transform.SetParent(root.transform, false);
		FixedJoystick joystick = root.AddComponent<FixedJoystick>();
		joystick.background = (RectTransform)root.transform;
		joystick.background.sizeDelta = new Vector2(200f, 200f);
		joystick.handle = (RectTransform)handle.transform;
		return joystick;
	}

	[Test]
	public void JoystickKeepsAnalogMagnitude() {
		Assert.AreEqual((0.25f - 0.1f) / 0.9f, FixedJoystick.ToInput(new Vector2(25f, 0f), 100f, 0.1f).magnitude, 0.001f);
		Assert.AreEqual(0f, FixedJoystick.ToInput(new Vector2(5f, 0f), 100f, 0.1f).magnitude, 0.0001f);
		Assert.AreEqual(1f, FixedJoystick.ToInput(new Vector2(300f, 0f), 100f, 0.1f).magnitude, 0.001f);
		Assert.AreEqual(1f, FixedJoystick.ToInput(new Vector2(100f, 100f), 100f, 0.1f).magnitude, 0.001f);
	}

	[Test]
	public void JoystickIgnoresOtherPointers() {
		FixedJoystick joystick = MakeJoystick();
		joystick.OnPointerDown(Pointer(1, new Vector2(80f, 0f), Vector2.zero));
		Vector2 held = joystick.inputVector;
		Assert.Greater(held.magnitude, 0.5f);

		joystick.OnDrag(Pointer(2, new Vector2(-80f, 0f), Vector2.zero));
		Assert.AreEqual(held, joystick.inputVector);
		joystick.OnPointerUp(Pointer(2, Vector2.zero, Vector2.zero));
		Assert.AreEqual(held, joystick.inputVector);
		joystick.OnPointerUp(Pointer(1, Vector2.zero, Vector2.zero));
		Assert.AreEqual(Vector2.zero, joystick.inputVector);
		Object.DestroyImmediate(joystick.gameObject);
	}

	[Test]
	public void JoystickResetsOnFocusLoss() {
		FixedJoystick joystick = MakeJoystick();
		joystick.OnPointerDown(Pointer(1, new Vector2(80f, 0f), Vector2.zero));
		typeof(FixedJoystick).GetMethod("OnApplicationFocus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
			.Invoke(joystick, new object[] { false });
		Assert.AreEqual(Vector2.zero, joystick.inputVector);
		// The released pointer no longer owns the stick.
		joystick.OnDrag(Pointer(1, new Vector2(80f, 0f), Vector2.zero));
		Assert.AreEqual(Vector2.zero, joystick.inputVector);
		Object.DestroyImmediate(joystick.gameObject);
	}

	[Test]
	public void LookAreaIgnoresOtherPointers() {
		TouchLookArea look = new GameObject("Look", typeof(RectTransform)).AddComponent<TouchLookArea>();
		look.OnPointerDown(Pointer(3, Vector2.zero, Vector2.zero));
		look.OnDrag(Pointer(4, Vector2.zero, new Vector2(50f, 0f)));
		Assert.AreEqual(Vector2.zero, look.ConsumeDelta());
		look.OnDrag(Pointer(3, Vector2.zero, new Vector2(20f, -5f)));
		look.OnDrag(Pointer(3, Vector2.zero, new Vector2(10f, 0f)));
		Assert.AreEqual(new Vector2(30f, -5f), look.ConsumeDelta());
		Assert.AreEqual(Vector2.zero, look.ConsumeDelta());
		look.OnPointerUp(Pointer(3, Vector2.zero, Vector2.zero));
		look.OnDrag(Pointer(3, Vector2.zero, new Vector2(10f, 0f)));
		Assert.AreEqual(Vector2.zero, look.ConsumeDelta());
		Object.DestroyImmediate(look.gameObject);
	}

	[Test]
	public void PickInputUsesOneSource() {
		Assert.AreEqual(new Vector2(1f, 0f), BallUserControl.PickInput(new Vector2(1f, 0f), new Vector2(0f, 1f)), "keys win whole, no X from keys + Y from stick");
		Assert.AreEqual(new Vector2(0f, 0.6f), BallUserControl.PickInput(Vector2.zero, new Vector2(0f, 0.6f)));
		Assert.AreEqual(Vector2.zero, BallUserControl.PickInput(Vector2.zero, Vector2.zero));
		Assert.AreEqual(new Vector2(0f, 1f), BallUserControl.PickInput(new Vector2(0.005f, 0f), new Vector2(0f, 1f)), "a decaying key axis does not hide the stick");
	}

	// uGUI gives the left mouse button pointer id -1: it must own the stick like any finger.
	[Test]
	public void JoystickOwnsTheMousePointer() {
		FixedJoystick joystick = MakeJoystick();
		joystick.OnPointerDown(Pointer(-1, new Vector2(80f, 0f), Vector2.zero));
		Assert.IsTrue(joystick.Owned);
		Vector2 held = joystick.inputVector;
		Assert.Greater(held.magnitude, 0.5f);
		joystick.OnPointerDown(Pointer(3, new Vector2(-80f, 0f), Vector2.zero));
		joystick.OnDrag(Pointer(3, new Vector2(-80f, 0f), Vector2.zero));
		Assert.AreEqual(held, joystick.inputVector, "a second pointer cannot steal the stick");
		joystick.OnPointerUp(Pointer(3, Vector2.zero, Vector2.zero));
		Assert.IsTrue(joystick.Owned);
		joystick.OnDrag(Pointer(-1, new Vector2(0f, 90f), Vector2.zero));
		Assert.Greater(joystick.inputVector.y, 0.5f);
		joystick.OnPointerUp(Pointer(-1, Vector2.zero, Vector2.zero));
		Assert.IsFalse(joystick.Owned);
		Assert.AreEqual(Vector2.zero, joystick.inputVector);
		Object.DestroyImmediate(joystick.gameObject);
	}

	// Right/middle mouse drags are camera orbit/pan for the PC; they must not grab the stick or the look area.
	[Test]
	public void OnlyLeftButtonOrTouchTakesControl() {
		FixedJoystick joystick = MakeJoystick();
		var right = Pointer(-2, new Vector2(80f, 0f), Vector2.zero);
		right.button = PointerEventData.InputButton.Right;
		joystick.OnPointerDown(right);
		Assert.IsFalse(joystick.Owned);
		Assert.AreEqual(Vector2.zero, joystick.inputVector);
		TouchLookArea look = new GameObject("Look", typeof(RectTransform)).AddComponent<TouchLookArea>();
		var middle = Pointer(-3, Vector2.zero, Vector2.zero);
		middle.button = PointerEventData.InputButton.Middle;
		look.OnPointerDown(middle);
		Assert.IsFalse(look.Owned);
		Object.DestroyImmediate(joystick.gameObject);
		Object.DestroyImmediate(look.gameObject);
	}

	[Test]
	public void PauseBackdropBlocksTouches() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			PauseController pause = Object.FindObjectOfType<PauseController>(true);
			Assert.IsTrue(pause.overlay.GetComponent<UnityEngine.UI.Image>().raycastTarget, level.levelId + ": taps behind the pause menu must not reach the HUD");
		}
	}

	[Test]
	public void LookAreaOwnsTheMousePointer() {
		TouchLookArea look = new GameObject("Look", typeof(RectTransform)).AddComponent<TouchLookArea>();
		look.OnPointerDown(Pointer(-1, Vector2.zero, Vector2.zero));
		Assert.IsTrue(look.Owned);
		look.OnPointerDown(Pointer(5, Vector2.zero, Vector2.zero));
		look.OnDrag(Pointer(5, Vector2.zero, new Vector2(40f, 0f)));
		Assert.AreEqual(Vector2.zero, look.ConsumeDelta(), "the other finger does not turn the camera");
		look.OnDrag(Pointer(-1, Vector2.zero, new Vector2(12f, 3f)));
		Assert.AreEqual(new Vector2(12f, 3f), look.ConsumeDelta());
		look.OnPointerUp(Pointer(5, Vector2.zero, Vector2.zero));
		Assert.IsTrue(look.Owned);
		look.OnPointerUp(Pointer(-1, Vector2.zero, Vector2.zero));
		Assert.IsFalse(look.Owned);
		Object.DestroyImmediate(look.gameObject);
	}

	static readonly string[] Tuning = { "m_MaxSpeed", "m_AccelerationRate", "m_Brake", "m_UseTorque" };

	[Test]
	public void RobotTuningIs11_24_30() {
		var ball = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab").GetComponent<Ball>());
		Assert.AreEqual(11f, ball.FindProperty("m_MaxSpeed").floatValue);
		Assert.AreEqual(24f, ball.FindProperty("m_AccelerationRate").floatValue);
		Assert.AreEqual(30f, ball.FindProperty("m_Brake").floatValue);
		Assert.IsFalse(ball.FindProperty("m_UseTorque").boolValue);
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			GameObject player = GameObject.FindWithTag("Player");
			foreach (PropertyModification m in PrefabUtility.GetPropertyModifications(player) ?? new PropertyModification[0])
				Assert.IsFalse(m.target is Ball && System.Array.IndexOf(Tuning, m.propertyPath) >= 0,
					level.levelId + " overrides " + m.propertyPath + " on the robot");
		}
	}

	static bool Interactive(Transform t) {
		for (; t != null; t = t.parent)
			foreach (MonoBehaviour b in t.GetComponents<MonoBehaviour>())
				if (b is UnityEngine.UI.Selectable || b is IEventSystemHandler)
					return true;
		return false;
	}

	// Decorative images/texts and the minimap must not catch touches meant for the camera swipe.
	[Test]
	public void OnlyInteractiveGraphicsCatchRays() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			foreach (UnityEngine.UI.Graphic g in Object.FindObjectsOfType<UnityEngine.UI.Graphic>(true))
				Assert.IsFalse(g.raycastTarget && !Interactive(g.transform), level.levelId + ": " + g.name + " (" + g.GetType().Name + ") blocks touches");
		}
	}

	[Test]
	public void SafeAreaAnchorsForNotch() {
		Rect anchors = SafeAreaFitter.Anchors(new Rect(100f, 0f, 2200f, 1080f), new Vector2(2400f, 1080f));
		Assert.AreEqual(100f / 2400f, anchors.x, 0.0001f);
		Assert.AreEqual(0f, anchors.y, 0.0001f);
		Assert.AreEqual(2300f / 2400f, anchors.width, 0.0001f);
		Assert.AreEqual(1f, anchors.height, 0.0001f);
	}

	[Test]
	public void LookFullWidthTurns180() {
		Vector2 r = ThirdPersonOrbitCamera.ApplyLook(0f, 40f, new Vector2(1000f, 0f), 1000f, 1f, 15f, 65f);
		Assert.AreEqual(180f, r.x, 0.001f);
		Assert.AreEqual(40f, r.y, 0.001f);
	}

	[Test]
	public void LookClampsPitch() {
		Assert.AreEqual(65f, ThirdPersonOrbitCamera.ApplyLook(0f, 40f, new Vector2(0f, -5000f), 1000f, 1f, 15f, 65f).y, 0.001f);
		Assert.AreEqual(15f, ThirdPersonOrbitCamera.ApplyLook(0f, 40f, new Vector2(0f, 5000f), 1000f, 1f, 15f, 65f).y, 0.001f);
	}

	static readonly string[] Levels = { "Level1", "Level2", "Level3", "Level4" };

	[Test]
	public void EveryLevelUsesOrbitCameraAndFixedMinimap() {
		foreach (string level in Levels) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			Camera main = Object.FindObjectsOfType<Camera>(true).First(c => c.CompareTag("MainCamera"));
			ThirdPersonOrbitCamera orbit = main.GetComponent<ThirdPersonOrbitCamera>();
			Assert.IsNotNull(orbit, level);
			Assert.IsNotNull(orbit.target, level);
			Assert.AreEqual("Player", orbit.target.tag, level);
			SmoothFollow old = main.GetComponent<SmoothFollow>();
			Assert.IsTrue(old == null || !old.enabled, level);
			foreach (SmoothFollow follow in Object.FindObjectsOfType<SmoothFollow>(true))
				Assert.IsFalse(follow.AllowUserInput, level + "/" + follow.name);
		}
	}

	[Test]
	public void EveryLevelHasLookAreaSafeAreaAndPause() {
		foreach (string level in Levels) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			TouchLookArea look = Object.FindObjectsOfType<TouchLookArea>(true).SingleOrDefault();
			Assert.IsNotNull(look, level);
			RectTransform rect = (RectTransform)look.transform;
			Assert.AreEqual(0.5f, rect.anchorMin.x, 0.001f, level);
			Assert.AreEqual(1f, rect.anchorMax.x, 0.001f, level);
			FixedJoystick joystick = Object.FindObjectsOfType<FixedJoystick>(true).Single();
			Assert.IsNotNull(joystick.GetComponentsInParent<SafeAreaFitter>(true).FirstOrDefault(), level);
			Assert.IsNotNull(Object.FindObjectsOfType<PauseController>(true).SingleOrDefault(), level);
		}
	}

	[Test]
	public void PlayerUsesInterpolation() {
		GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
		Assert.AreEqual(RigidbodyInterpolation.Interpolate, player.GetComponent<Rigidbody>().interpolation);
	}

	[Test]
	public void JoystickIsAboutAFifthOfScreenHeight() {
		// Canvas height differs per aspect ratio (800-wide reference, width-matched): 450 at 16:9, 360 at 20:9.
		Assert.AreEqual(99f, ScreenShareSizer.Size(450f, 0.22f), 0.01f);
		foreach (string level in Levels) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			FixedJoystick joystick = Object.FindObjectsOfType<FixedJoystick>(true).Single();
			ScreenShareSizer sizer = joystick.GetComponent<ScreenShareSizer>();
			Assert.IsNotNull(sizer, level);
			Assert.That(sizer.heightShare, Is.InRange(0.20f, 0.25f), level);
		}
	}

	[Test]
	public void SensitivitySliderIsTouchSized() {
		foreach (string level in Levels) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			UnityEngine.UI.Slider slider = Object.FindObjectsOfType<PauseController>(true).Single().sensitivitySlider;
			Assert.GreaterOrEqual(((RectTransform)slider.transform).sizeDelta.y, 70f, level);
			Assert.GreaterOrEqual(slider.handleRect.sizeDelta.x, 60f, level);
			Assert.AreNotEqual(Color.white, slider.fillRect.GetComponent<UnityEngine.UI.Image>().color, level);
		}
	}
}
