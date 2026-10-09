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

public class HealthPlayTests {

	GameObject player;
	Health health;
	NavMeshDataInstance navMesh;
	int hits;

	IEnumerator Arena() {
#if UNITY_EDITOR
		EditorSceneManager.LoadSceneInPlayMode("Assets/Tests/PlayMode/EnemyArena.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
		yield return null;
		yield return null;
		GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
		floor.transform.position = new Vector3(0f, -0.5f, 0f);
		floor.transform.localScale = new Vector3(60f, 1f, 60f);
		navMesh = NavMesh.AddNavMeshData(NavMeshBake.Build(new Bounds(Vector3.zero, new Vector3(64f, 10f, 64f))));
#if UNITY_EDITOR
		player = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"), new Vector3(0f, 0.5f, 0f), Quaternion.identity);
#endif
		player.tag = "Player";
		Object.Destroy(player.GetComponent<BallUserControl>());
		player.GetComponent<Rigidbody>().isKinematic = true;
		health = player.GetComponent<Health>();
		hits = 0;
		health.Damaged += amount => hits++;
		yield return null;
	}

	[TearDown]
	public void Cleanup() {
		navMesh.Remove();
		GameFlow.ResetForScene();
	}

	EnemyBrain Creep(Vector3 position) {
#if UNITY_EDITOR
		GameObject enemy = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy - Crater.prefab"),
			position, Quaternion.LookRotation(-position));
		EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
		brain.target = player.transform;
		return brain;
#else
		return null;
#endif
	}

	// Two creeps striking in the same frame cost one hit: the 0.8 s invulnerability is global, not per enemy.
	[UnityTest]
	public IEnumerator GroupHitCostsOneHitPerWindow() {
		yield return Arena();
		EnemyBrain a = Creep(new Vector3(1.1f, 0f, 0f)), b = Creep(new Vector3(-1.1f, 0f, 0f));
		float end = Time.time + 3f;
		while (Time.time < end && !(a.State == EnemyState.Recover && b.State == EnemyState.Recover))
			yield return null;
		yield return null;
		Assert.AreEqual(1, hits, "one hit per invulnerability window");
		Assert.AreEqual(100f - a.damage, health.healthPoints, 0.01f);
		Assert.IsTrue(health.isAlive, "100 HP survives a creep hit");
	}

	[UnityTest]
	public IEnumerator InvulnerabilityUsesGameplayTime() {
		yield return Arena();
		Assert.IsTrue(health.TakeDamage(10f, DamageKind.EnemyAttack));
		GameFlow.Pause();
		yield return new WaitForSecondsRealtime(1.2f);
		GameFlow.Resume();
		Assert.IsFalse(health.TakeDamage(10f, DamageKind.EnemyAttack), "window frozen while paused");
		yield return new WaitForSeconds(Health.InvulnerableSeconds + 0.1f);
		Assert.IsTrue(health.TakeDamage(10f, DamageKind.EnemyAttack));
		Assert.AreEqual(80f, health.healthPoints, 0.01f);
	}
}
