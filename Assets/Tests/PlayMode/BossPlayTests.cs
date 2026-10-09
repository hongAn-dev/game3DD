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

/// <summary>Boss attacks (spec §4.3) on a flat arena with a runtime NavMesh; the robot is held still unless moved.</summary>
public class BossPlayTests {

	GameObject player;
	Health health;
	NavMeshDataInstance navMesh;
	int hits;
	float lastDamage;

	IEnumerator Arena(params Vector3[][] walls) {
#if UNITY_EDITOR
		EditorSceneManager.LoadSceneInPlayMode("Assets/Tests/PlayMode/EnemyArena.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
		yield return null;
		yield return null;
		GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
		floor.transform.position = new Vector3(0f, -0.5f, 0f);
		floor.transform.localScale = new Vector3(60f, 1f, 60f);
		foreach (Vector3[] wall in walls)
			Wall(wall[0], wall[1]);
		navMesh = NavMesh.AddNavMeshData(NavMeshBake.Build(new Bounds(Vector3.zero, new Vector3(64f, 10f, 64f))));
#if UNITY_EDITOR
		player = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"), new Vector3(0f, 0.5f, 0f), Quaternion.identity);
#endif
		player.tag = "Player";
		Object.Destroy(player.GetComponent<BallUserControl>());
		player.GetComponent<Rigidbody>().isKinematic = true;
		health = player.GetComponent<Health>();
		hits = 0;
		health.Damaged += amount => { hits++; lastDamage = amount; };
		yield return null;
	}

	static GameObject Wall(Vector3 position, Vector3 size) {
		GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
		w.name = "Wall";
		w.transform.position = position;
		w.transform.localScale = size;
		return w;
	}

	[TearDown]
	public void Cleanup() {
		navMesh.Remove();
		GameFlow.ResetForScene();
	}

	EnemyBrain Boss(string level, Vector3 position) {
#if UNITY_EDITOR
		GameObject enemy = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy - Monster.prefab"),
			position, Quaternion.LookRotation(Flat(player.transform.position - position)));
		EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
		brain.target = player.transform;
		brain.Configure(EnemyProfile.For(LevelCatalog.Get(level), GameSettings.gameDifficulties.Normal, true));
		return brain;
#else
		return null;
#endif
	}

	static Vector3 Flat(Vector3 v) {
		v.y = 0f;
		return v;
	}

	void Move(Vector3 to) {
		player.GetComponent<Rigidbody>().position = to;
		player.transform.position = to;
	}

	IEnumerator WaitFor(EnemyBrain brain, EnemyState state, float timeout) {
		float end = Time.time + timeout;
		while (brain.State != state && Time.time < end)
			yield return null;
		Assert.AreEqual(state, brain.State, "never reached " + state);
	}

	[UnityTest]
	public IEnumerator BossSlamHitsInsideTheCircle() {
		yield return Arena();
		EnemyBrain boss = Boss("Level3", new Vector3(0f, 0f, 2.6f));
		yield return WaitFor(boss, EnemyState.Windup, 2f);
		Assert.AreEqual(AttackKind.Slam, boss.CurrentAttack);
		Assert.AreEqual(0, hits, "nothing before the slam lands");
		yield return WaitFor(boss, EnemyState.Recover, 2f);
		yield return null;
		Assert.AreEqual(1, hits);
		Assert.AreEqual(25f, lastDamage, 0.01f, "L3 Normal slam damage");
	}

	[UnityTest]
	public IEnumerator BossSlamMissesWhenRobotLeaves() {
		yield return Arena();
		EnemyBrain boss = Boss("Level3", new Vector3(0f, 0f, 2.6f));
		yield return WaitFor(boss, EnemyState.Windup, 2f);
		Move(new Vector3(0f, 0.5f, -3.5f));   // outside the 2.4 m circle centred 1.2 m in front of the boss
		yield return WaitFor(boss, EnemyState.Recover, 2f);
		yield return null;
		Assert.AreEqual(0, hits);
	}

	[UnityTest]
	public IEnumerator BossSlamDoesNotHitThroughWall() {
		yield return Arena();
		EnemyBrain boss = Boss("Level3", new Vector3(0f, 0f, 2.6f));
		yield return WaitFor(boss, EnemyState.Windup, 2f);
		Wall(new Vector3(0f, 1.5f, 0.75f), new Vector3(8f, 3f, 0.2f));   // raised between slam centre and robot mid-windup
		yield return WaitFor(boss, EnemyState.Recover, 2f);
		yield return null;
		Assert.AreEqual(0, hits);
	}

	[UnityTest]
	public IEnumerator SlamTelegraphShowsTheRealRadius() {
		yield return Arena();
		EnemyBrain boss = Boss("Level3", new Vector3(0f, 0f, 2.6f));
		yield return WaitFor(boss, EnemyState.Windup, 2f);
		yield return null;
		Bounds disc = boss.telegraph.GetComponentInChildren<Renderer>().bounds;
		Assert.AreEqual(boss.slamRadius * 2f, disc.size.x, 0.2f, "drawn at the real size");
		Vector3 centre = BossAttacks.SlamCentre(boss.transform.position, Flat(player.transform.position - boss.transform.position).normalized, boss.slamReach);
		Assert.Less(Vector3.Distance(Flat(disc.center), Flat(centre)), 0.3f, "drawn where it lands");
	}
}
