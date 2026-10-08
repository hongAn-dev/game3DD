using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Attack and navigation contract (spec §6.2/§6.3, replaces PatrolRobotKillsPlayerOnContact) on a flat arena scene
/// with a NavMesh baked at runtime. The robot has many lives so each hit can be counted.
/// </summary>
public class EnemyPlayTests {

	const int Lives = 99;
	GameObject player;
	Health health;
	NavMeshDataInstance navMesh;

	int Hits { get { return health == null ? 0 : Lives - health.numberOfLives; } }

	IEnumerator Arena(params Vector3[][] walls) {
#if UNITY_EDITOR
		EditorSceneManager.LoadSceneInPlayMode("Assets/Tests/PlayMode/EnemyArena.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
		yield return null;
		yield return null;
		GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
		floor.transform.position = new Vector3(0f, -0.5f, 0f);
		floor.transform.localScale = new Vector3(60f, 1f, 60f);
		foreach (Vector3[] wall in walls) {
			GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
			w.name = "Wall";
			w.transform.position = wall[0];
			w.transform.localScale = wall[1];
		}
		navMesh = NavMesh.AddNavMeshData(NavMeshBake.Build(new Bounds(Vector3.zero, new Vector3(64f, 10f, 64f))));
#if UNITY_EDITOR
		player = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"), new Vector3(0f, 0.5f, 0f), Quaternion.identity);
#endif
		player.tag = "Player";
		Object.Destroy(player.GetComponent<BallUserControl>());
		player.GetComponent<Rigidbody>().isKinematic = true;   // holds still unless a test moves it
		health = player.GetComponent<Health>();
		health.numberOfLives = Lives;
		yield return null;
	}

	[TearDown]
	public void Cleanup() {
		navMesh.Remove();
		GameFlow.ResetForScene();
	}

	EnemyBrain Spawn(string prefab, Vector3 position) {
#if UNITY_EDITOR
		GameObject enemy = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + prefab + ".prefab"),
			position, Quaternion.LookRotation(Flat(player.transform.position - position)));
		EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
		brain.target = player.transform;
		return brain;
#else
		return null;
#endif
	}

	static Vector3 Flat(Vector3 v) {
		v.y = 0f;
		return v;
	}

	IEnumerator WaitFor(EnemyBrain brain, EnemyState state, float timeout) {
		float end = Time.time + timeout;
		while (brain.State != state && Time.time < end)
			yield return null;
		Assert.AreEqual(state, brain.State, "never reached " + state);
	}

	[UnityTest]
	public IEnumerator StrikeHitsOnceInRange() {
		yield return Arena();
		EnemyBrain boss = Spawn("Enemy - Monster", new Vector3(0f, 0f, 1.6f));
		yield return WaitFor(boss, EnemyState.Windup, 2f);
		Assert.AreEqual(0, Hits, "no damage before the strike");
		yield return WaitFor(boss, EnemyState.Recover, 1f);
		yield return null;   // Health counts the life on its next Update
		Assert.AreEqual(1, Hits, "one strike = one hit");
	}

	[UnityTest]
	public IEnumerator DodgedStrikeMisses() {
		yield return Arena();
		EnemyBrain creep = Spawn("Enemy - Crater", new Vector3(0f, 0f, 1.1f));
		yield return WaitFor(creep, EnemyState.Windup, 2f);
		player.transform.position = new Vector3(0f, 0.5f, -5f);   // out of range before the strike
		yield return WaitFor(creep, EnemyState.Recover, 1f);
		yield return null;
		Assert.AreEqual(0, Hits);
	}

	// The boss starts inside its windup range (1.7 m < 0.85 × 2.18 m) and facing the robot, with a thin wall between
	// them: only the line-of-sight check can stop the attack. The wall is long, so walking around takes longer.
	[UnityTest]
	public IEnumerator NoStrikeThroughWall() {
		yield return Arena(new[] { new Vector3(0f, 1.5f, 0.55f), new Vector3(50f, 3f, 0.1f) });
		EnemyBrain boss = Spawn("Enemy - Monster", new Vector3(0f, 0f, 1.7f));
		yield return null;   // the agent snaps onto the NavMesh edge
		Assert.Less(Vector3.Distance(Flat(boss.transform.position), Flat(player.transform.position)), boss.attackRange * 0.85f, "starts inside the windup range");
		float end = Time.time + 2f;
		while (Time.time < end) {
			Assert.AreNotEqual(EnemyState.Windup, boss.State, "no windup while the wall is in between");
			yield return null;
		}
		Assert.AreEqual(0, Hits);
	}

	[UnityTest]
	public IEnumerator DeathDisablesEnemies() {
		yield return Arena();
		EnemyBrain boss = Spawn("Enemy - Monster", new Vector3(0f, 0f, 1.6f));
		yield return WaitFor(boss, EnemyState.Windup, 2f);
		Object.Destroy(player);   // what Health does when the robot's last life is gone
		yield return null;
		yield return null;
		Assert.AreEqual(EnemyState.Disabled, boss.State);
		Assert.IsFalse(boss.telegraph.activeSelf, "no attack warning behind the game over screen");
	}

	[UnityTest]
	public IEnumerator BodyContactDoesNotKill() {
		yield return Arena();
		EnemyBrain boss = Spawn("Enemy - Monster", new Vector3(0f, 0f, 0.9f));
		boss.enabled = false;
		player.GetComponent<Rigidbody>().isKinematic = false;
		yield return new WaitForSeconds(1f);
		Assert.AreEqual(0, Hits);
	}

	[UnityTest]
	public IEnumerator WinCancelsPendingStrike() {
		yield return Arena();
		EnemyBrain boss = Spawn("Enemy - Monster", new Vector3(0f, 0f, 1.6f));
		yield return WaitFor(boss, EnemyState.Windup, 2f);
		GameFlow.CompleteLevel();
		yield return new WaitForSeconds(1.2f);
		Assert.AreEqual(EnemyState.Disabled, boss.State);
		Assert.AreEqual(0, Hits);
	}

	[UnityTest]
	public IEnumerator PauseFreezesTheWindup() {
		yield return Arena();
		EnemyBrain boss = Spawn("Enemy - Monster", new Vector3(0f, 0f, 1.6f));
		yield return WaitFor(boss, EnemyState.Windup, 2f);
		GameFlow.Pause();
		yield return new WaitForSecondsRealtime(1.5f);
		Assert.AreEqual(EnemyState.Windup, boss.State, "frozen while paused");
		Assert.AreEqual(0, Hits);
		GameFlow.Resume();
	}

	// U-shaped wall open away from the robot: the creep inside has to walk out and around.
	[UnityTest]
	public IEnumerator ChasesAroundUWall() {
		yield return Arena(
			new[] { new Vector3(0f, 1f, 4f), new Vector3(8f, 2f, 0.4f) },     // back of the U, between creep and robot
			new[] { new Vector3(-4f, 1f, 7f), new Vector3(0.4f, 2f, 6f) },
			new[] { new Vector3(4f, 1f, 7f), new Vector3(0.4f, 2f, 6f) });
		EnemyBrain creep = Spawn("Enemy - Crater", new Vector3(0f, 0f, 6f));
		float end = Time.time + 10f;
		while (Time.time < end && Vector3.Distance(Flat(creep.transform.position), Flat(player.transform.position)) > creep.attackRange)
			yield return null;
		Assert.LessOrEqual(Vector3.Distance(Flat(creep.transform.position), Flat(player.transform.position)), creep.attackRange, "reached the robot around the wall");
	}
}
