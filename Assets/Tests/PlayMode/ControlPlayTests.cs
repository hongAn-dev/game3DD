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
}
