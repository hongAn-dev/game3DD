using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ControlPlayTests {

	Ball ball;
	Rigidbody body;
	Vector3 offset;
	static int testIndex;

	// Flat test ground with a ledge at z = 40 (ground ends there).
	IEnumerator FlatGround() {
#if UNITY_EDITOR
		Scene scene = SceneManager.CreateScene("ControlTest" + Random.Range(0, 100000));
		SceneManager.SetActiveScene(scene);
		// Scenes left by earlier tests (e.g. BaselineProbes' 400 m ground) share the physics world, so each test
		// builds its ground in its own far-away strip instead of unloading scenes (that would unload the runner).
		offset = new Vector3(2000f + 500f * (testIndex++), 0f, 0f);
		GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
		ground.transform.localScale = new Vector3(200f, 1f, 200f);
		ground.transform.position = offset + new Vector3(0f, -0.5f, -60f);
		new GameObject("Test Camera").AddComponent<Camera>().tag = "MainCamera";
		GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"),
			offset + new Vector3(0f, 0.6f, -150f), Quaternion.identity);
		Object.Destroy(player.GetComponent<BallUserControl>());
		ball = player.GetComponent<Ball>();
		body = player.GetComponent<Rigidbody>();
		GameFlow.ResetForScene();
#endif
		yield return null;
	}

	float Horizontal() {
		return new Vector2(body.velocity.x, body.velocity.z).magnitude;
	}

	IEnumerator Push(Vector3 dir, float seconds) {
		float end = Time.time + seconds;
		while (Time.time < end) {
			yield return new WaitForFixedUpdate();
			ball.Move(dir, false);
		}
	}

	[UnityTest]
	public IEnumerator SpeedCapHoldsUnderFullInput() {
		yield return FlatGround();
		yield return Push(Vector3.forward, 4f);
		Assert.LessOrEqual(Horizontal(), ball.MaxSpeed + 0.2f);
		Assert.Greater(Horizontal(), ball.MaxSpeed * 0.8f);
	}

	[UnityTest]
	public IEnumerator ReleaseBrakesWithin1_5s() {
		yield return FlatGround();
		yield return Push(Vector3.forward, 3f);
		float cap = Horizontal();
		yield return Push(Vector3.zero, 1.5f);
		Assert.Less(Horizontal(), cap * 0.25f);
	}

	// Spec §3.2: 90% of top speed within 0.5 s on flat ground.
	[UnityTest]
	public IEnumerator ReachesNinetyPercentInHalfASecond() {
		yield return FlatGround();
		yield return Push(Vector3.zero, 0.3f);   // settle on the ground
		float start = Time.time;
		while (Horizontal() < ball.MaxSpeed * 0.9f && Time.time - start < 2f) {
			yield return new WaitForFixedUpdate();
			ball.Move(Vector3.forward, false);
		}
		Assert.AreEqual(11f, ball.MaxSpeed, 0.01f);
		Assert.LessOrEqual(Time.time - start, 0.52f);
	}

	// Spec §3.2: from 11 m/s, releasing the stick stops the robot in about 0.4 s and no more than 2.3 m.
	[UnityTest]
	public IEnumerator StopsWithin2_3Metres() {
		yield return FlatGround();
		yield return Push(Vector3.forward, 2f);
		Assert.Greater(Horizontal(), 10.8f);
		Vector3 from = body.position;
		float start = Time.time;
		while (Horizontal() > 0.05f && Time.time - start < 2f) {
			yield return new WaitForFixedUpdate();
			ball.Move(Vector3.zero, false);
		}
		Vector3 travel = body.position - from;
		travel.y = 0f;
		Assert.LessOrEqual(Time.time - start, 0.45f, "stop time");
		Assert.LessOrEqual(travel.magnitude, 2.3f, "stop distance");
	}

	// Overdrive top speed (11 × 1.3) is reached and the ball rolls without skidding (spin not capped below v/r).
	[UnityTest]
	public IEnumerator OverdriveSpeedRollsWithoutSkidding() {
		yield return FlatGround();
		ball.SpeedMultiplier = Overdrive.SpeedBoost;
		yield return Push(Vector3.forward, 2.5f);
		Assert.Greater(Horizontal(), ball.MaxSpeed * Overdrive.SpeedBoost - 0.1f);
		Assert.AreEqual(Horizontal() / 0.5f, body.angularVelocity.magnitude, 0.5f, "spin keeps up with the roll");
	}

	[UnityTest]
	public IEnumerator SpeedCapDoesNotClampFalling() {
		yield return FlatGround();
		body.position = offset + new Vector3(0f, 0.6f, 30f);
		float minVertical = 0f;
		float end = Time.time + 3f;
		while (Time.time < end) {
			yield return new WaitForFixedUpdate();
			ball.Move(Vector3.forward, false);
			minVertical = Mathf.Min(minVertical, body.velocity.y);
		}
		Assert.Less(minVertical, -5f);
	}

	ThirdPersonOrbitCamera OrbitRig(out Transform target) {
		offset = new Vector3(2000f + 500f * (testIndex++), 0f, 0f);
		target = new GameObject("Orbit Target").transform;
		target.position = offset;
		GameObject camGo = new GameObject("Orbit Camera");
		camGo.AddComponent<Camera>();
		ThirdPersonOrbitCamera orbit = camGo.AddComponent<ThirdPersonOrbitCamera>();
		orbit.target = target;
		orbit.yaw = 0f;
		orbit.pitch = 30f;
		orbit.distance = 8f;
		GameFlow.ResetForScene();
		return orbit;
	}

	[UnityTest]
	public IEnumerator OrbitCameraStopsAtObstacle() {
		Transform target;
		ThirdPersonOrbitCamera orbit = OrbitRig(out target);
		yield return null;
		float free = Vector3.Distance(orbit.transform.position, target.position + Vector3.up * 0.5f);
		Assert.AreEqual(8f, free, 0.3f);

		// A wall halfway between the pivot and the camera.
		GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
		wall.transform.position = Vector3.Lerp(target.position + Vector3.up * 0.5f, orbit.transform.position, 0.5f);
		wall.transform.localScale = new Vector3(10f, 10f, 0.5f);
		yield return new WaitForFixedUpdate();
		yield return null;
		float blocked = Vector3.Distance(orbit.transform.position, target.position + Vector3.up * 0.5f);
		Assert.Less(blocked, free * 0.6f);
		Assert.IsFalse(wall.GetComponent<Collider>().bounds.Contains(orbit.transform.position));
	}

	[UnityTest]
	public IEnumerator OrbitCameraIgnoresInputWhenNotPlaying() {
		Transform target;
		ThirdPersonOrbitCamera orbit = OrbitRig(out target);
		yield return null;
		GameFlow.Pause();
		orbit.AddLook(new Vector2(500f, 0f));
		yield return null;
		Assert.AreEqual(0f, orbit.yaw, 0.001f);
		GameFlow.Resume();
	}

	[UnityTest]
	public IEnumerator HalfStickGivesHalfSpeed() {
		yield return FlatGround();
		yield return Push(Vector3.forward * 0.5f, 5f);
		Assert.AreEqual(ball.MaxSpeed * 0.5f, Horizontal(), ball.MaxSpeed * 0.5f * 0.15f);
	}

	[UnityTest]
	public IEnumerator OrbitCameraIgnoresMovingActors() {
		Transform target;
		ThirdPersonOrbitCamera orbit = OrbitRig(out target);
		yield return null;
		float free = Vector3.Distance(orbit.transform.position, target.position + Vector3.up * 0.5f);

		// An enemy-like body (kinematic rigidbody) passing behind the ball must not yank the camera in.
		GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		enemy.transform.position = Vector3.Lerp(target.position + Vector3.up * 0.5f, orbit.transform.position, 0.5f);
		enemy.transform.localScale = Vector3.one * 2f;
		enemy.AddComponent<Rigidbody>().isKinematic = true;
		yield return new WaitForFixedUpdate();
		yield return null;
		Assert.AreEqual(free, Vector3.Distance(orbit.transform.position, target.position + Vector3.up * 0.5f), 0.3f);
	}

	IEnumerator LoadLevel1() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;
	}

	[UnityTest]
	public IEnumerator MobileControlsStayVisibleOffDevice() {
		yield return LoadLevel1();
		FixedJoystick joystick = Object.FindObjectOfType<FixedJoystick>();
		Assert.IsNotNull(joystick, "joystick active in the Editor/PC run");
		Assert.IsTrue(joystick.isActiveAndEnabled);
		Assert.IsNotNull(Object.FindObjectOfType<TouchLookArea>(), "look area active too");
	}

	[UnityTest]
	public IEnumerator JoystickResetsWhenNotPlaying() {
		yield return LoadLevel1();
		FixedJoystick joystick = Object.FindObjectOfType<FixedJoystick>();
		Canvas canvas = joystick.GetComponentInParent<Canvas>().rootCanvas;
		Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
		Vector2 centre = RectTransformUtility.WorldToScreenPoint(cam, joystick.background.position);
		var press = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
			pointerId = 0, position = centre + new Vector2(joystick.background.rect.width * 0.4f * canvas.scaleFactor, 0f) };
		joystick.OnPointerDown(press);
		Assert.Greater(joystick.inputVector.magnitude, 0.3f, "test steers the stick");
		GameFlow.Pause();
		yield return null;
		Assert.AreEqual(Vector2.zero, joystick.inputVector, "pause drops the held vector");
		Assert.IsFalse(joystick.Owned);
		GameFlow.Resume();
		yield return null;
		Assert.AreEqual(Vector2.zero, joystick.inputVector, "nothing stale after resume");
	}
}
