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

	IEnumerator WaitForDashWindup(EnemyBrain boss, float timeout) {
		float end = Time.time + timeout;
		while (!(boss.State == EnemyState.Windup && boss.CurrentAttack == AttackKind.Dash) && Time.time < end)
			yield return null;
		Assert.AreEqual(AttackKind.Dash, boss.CurrentAttack, "no dash windup");
		Assert.AreEqual(EnemyState.Windup, boss.State);
	}

	// Both attacks possible (2.8 m: inside slam range, beyond the 2.5 m dash minimum): after a slam the dash wins.
	[UnityTest]
	public IEnumerator L4BossAlternatesSlamAndDash() {
		yield return Arena();
		EnemyBrain boss = Boss("Level4", new Vector3(0f, 0f, 2.8f));
		var attacks = new System.Collections.Generic.List<AttackKind>();
		EnemyState last = boss.State;
		float end = Time.time + 8f;
		while (Time.time < end && attacks.Count < 2) {
			if (boss.State == EnemyState.Windup && last != EnemyState.Windup)
				attacks.Add(boss.CurrentAttack);
			last = boss.State;
			yield return null;
		}
		CollectionAssert.AreEqual(new[] { AttackKind.Slam, AttackKind.Dash }, attacks, "slam first (preferred when both are ready), then the dash");
	}

	[UnityTest]
	public IEnumerator DashKeepsItsLockedLine() {
		yield return Arena();
		EnemyBrain boss = Boss("Level4", new Vector3(0f, 0f, 4.5f));
		yield return WaitForDashWindup(boss, 3f);
		Vector3 start = boss.transform.position;
		Vector3 locked = Flat(player.transform.position - start).normalized;
		Move(new Vector3(3f, 0.5f, 0f));   // sidestep after the direction is locked
		yield return WaitFor(boss, EnemyState.Recover, 3f);
		Vector3 travel = Flat(boss.transform.position - start);
		Assert.Greater(travel.magnitude, 2f, "it dashed");
		Assert.Less(Vector3.Angle(travel, locked), 5f, "the dash does not bend toward the robot");
		Assert.AreEqual(0, hits, "dodged");
	}

	[UnityTest]
	public IEnumerator DashStopsBeforeObstacle() {
		yield return Arena(new[] { new Vector3(0f, 1.5f, 0.5f), new Vector3(20f, 3f, 0.2f) });
		Move(new Vector3(0f, 0.5f, 2f));
		EnemyBrain boss = Boss("Level4", new Vector3(0f, 0f, 5.5f));
		yield return WaitForDashWindup(boss, 3f);
		float startZ = boss.transform.position.z;
		Assert.Less(boss.dashTelegraph.transform.localScale.z, boss.dashLength - 0.5f, "the strip is cut short by the wall");
		yield return WaitFor(boss, EnemyState.Recover, 3f);
		Assert.Greater(boss.transform.position.z, 0.6f + 0.5f, "stopped before the wall, body not inside it");
	}

	[UnityTest]
	public IEnumerator DashHitsOnce() {
		yield return Arena();
		EnemyBrain boss = Boss("Level4", new Vector3(0f, 0f, 4.5f));
		yield return WaitForDashWindup(boss, 3f);
		yield return WaitFor(boss, EnemyState.Recover, 3f);
		yield return null;
		Assert.AreEqual(1, boss.HitAttempts, "one dash checks one hit, even while overlapping for several frames");
		Assert.AreEqual(1, hits, "one dash, one hit");
		Assert.AreEqual(30f, lastDamage, 0.01f, "L4 Normal dash damage");
	}

	[UnityTest]
	public IEnumerator DashTelegraphShowsWidthAndLength() {
		yield return Arena();
		EnemyBrain boss = Boss("Level4", new Vector3(0f, 0f, 4.5f));
		yield return WaitForDashWindup(boss, 3f);
		yield return null;
		Assert.IsTrue(boss.dashTelegraph.activeInHierarchy);
		Assert.IsFalse(boss.telegraph.activeInHierarchy, "not the slam circle");
		Bounds strip = boss.dashTelegraph.GetComponentInChildren<Renderer>().bounds;
		Assert.AreEqual(boss.dashWidth, strip.size.x, 0.15f, "width (dash runs along z)");
		Assert.That(strip.size.z, Is.InRange(2f, boss.dashLength + 0.05f), "length");
	}

	// The boss starts turned ~25° away: the strip must follow the locked line while the boss turns in the windup.
	[UnityTest]
	public IEnumerator DashStripStaysOnTheLockedLine() {
		yield return Arena();
		Vector3 at = new Vector3(0f, 0f, 4.5f);
#if UNITY_EDITOR
		GameObject enemy = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy - Monster.prefab"),
			at, Quaternion.LookRotation(Quaternion.Euler(0f, 25f, 0f) * Flat(player.transform.position - at)));
		EnemyBrain boss = enemy.GetComponent<EnemyBrain>();
		boss.target = player.transform;
		boss.Configure(EnemyProfile.For(LevelCatalog.Get("Level4"), GameSettings.gameDifficulties.Normal, true));
#else
		EnemyBrain boss = null;
#endif
		yield return WaitForDashWindup(boss, 3f);
		Vector3 locked = Flat(player.transform.position - boss.transform.position).normalized;
		yield return new WaitForSeconds(0.4f);   // the boss has finished turning
		Vector3 strip = Flat(boss.dashTelegraph.transform.forward).normalized;
		Assert.Less(Vector3.Angle(strip, locked), 3f, "strip points along the dash");
		Vector3 centre = Flat(boss.dashTelegraph.transform.position - boss.transform.position);
		Assert.Less(Vector3.Angle(centre, locked), 3f, "strip lies on the dash line");
	}

	// A robot on a 1.2 m block (another terrain level) inside the circle is not hit, even with a clear line.
	[UnityTest]
	public IEnumerator BossSlamIgnoresAnotherLevel() {
		yield return Arena(new[] { new Vector3(0f, 0.6f, -0.35f), new Vector3(2f, 1.2f, 1.3f) });
		Move(new Vector3(0f, 1.7f, 0f));
		EnemyBrain boss = Boss("Level3", new Vector3(0f, 0f, 2.6f));
		yield return WaitFor(boss, EnemyState.Windup, 2f);
		yield return WaitFor(boss, EnemyState.Recover, 2f);
		yield return null;
		Assert.AreEqual(0, hits, "not the same terrain level");
	}
}
