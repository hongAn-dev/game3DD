using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// T0 measurement, not a regression test: run with -testFilter BaselineProbes.
public class BaselineProbes {

	[UnityTest]
	public IEnumerator RobotTopSpeedOnFlatGround() {
#if UNITY_EDITOR
		Scene scene = SceneManager.CreateScene("Probe");
		SceneManager.SetActiveScene(scene);
		GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
		ground.transform.localScale = new Vector3(400f, 1f, 400f);
		ground.transform.position = new Vector3(0f, -0.5f, 0f);
		new GameObject("Probe Camera").AddComponent<Camera>().tag = "MainCamera";

		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
		GameObject player = Object.Instantiate(prefab, new Vector3(0f, 0.6f, -150f), Quaternion.identity);
		// The probe drives Ball.Move itself; the user control would also call Move (and needs a joystick).
		Object.Destroy(player.GetComponent<BallUserControl>());
		Ball ball = player.GetComponent<Ball>();
		Rigidbody body = player.GetComponent<Rigidbody>();

		// The baseline Ball pushes with a constant force and only 0.1 drag, so it has no steady top speed;
		// log the speed reached after holding full input for 1, 2 and 4 seconds instead.
		float start = Time.time;
		var marks = new System.Collections.Generic.List<string>();
		float[] at = { 1f, 2f, 4f };
		int next = 0;
		float speed = 0f;
		while (next < at.Length) {
			yield return new WaitForFixedUpdate();
			ball.Move(Vector3.forward, false);
			Vector3 v = body.velocity;
			speed = new Vector2(v.x, v.z).magnitude;
			if (Time.time - start >= at[next]) {
				marks.Add(at[next] + "s=" + speed.ToString("F2"));
				next++;
			}
		}
		Debug.Log("PROBE V " + string.Join(" ", marks.ToArray()) + " m/s (full input from rest, robot diameter 1.0, no speed cap)");
		Assert.Greater(speed, 0.5f);
#else
		yield return null;
#endif
	}
}
