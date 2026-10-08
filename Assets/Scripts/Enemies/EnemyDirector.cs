using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Sole owner of a level's enemy budget (spec §6.2): alive + telegraphed enemies never exceed
/// LevelConfig.enemyCap[difficulty], of which at most bossCap[difficulty] are bosses. Nothing spawns during the first
/// quietTime seconds; Level1's boss waits until the robot has practised (bossAfterScore cores or bossAfterTime
/// seconds). Every spawn shows a ground telegraph for telegraphTime on a spawn point at least minPlayerDistance from
/// the robot. Runs only while GameFlow is Playing.
/// </summary>
public class EnemyDirector : MonoBehaviour {

	public GameObject bossPrefab;
	public GameObject creepPrefab;
	public Material telegraphMaterial;
	public Transform[] spawnPoints = new Transform[0];

	public float quietTime = 5f;
	public float intervalMin = 4f;
	public float intervalMax = 7f;
	[Tooltip("Seconds between spawns; < 0 uses intervalMin..intervalMax (tests set it).")]
	public float intervalOverride = -1f;
	public float telegraphTime = 0.7f;
	public float minPlayerDistance = 6f;
	public int bossAfterScore = 0;
	public float bossAfterTime = 8f;

	readonly List<EnemyBrain> bosses = new List<EnemyBrain>();
	readonly List<EnemyBrain> creeps = new List<EnemyBrain>();
	int pendingBosses, pendingCreeps;
	int cap, bossCap;
	float elapsed, timer, nextInterval;
	Transform player;

	public int AliveCount { get { Prune(); return bosses.Count + creeps.Count + pendingBosses + pendingCreeps; } }
	public int BossCount { get { Prune(); return bosses.Count + pendingBosses; } }

	void Prune() {
		bosses.RemoveAll(b => b == null);
		creeps.RemoveAll(c => c == null);
	}

	void Start() {
		LevelConfig config = LevelCatalog.Get(SceneManager.GetActiveScene().name);
		if (config == null) {
			enabled = false;
			return;
		}
		int d = (int)GameSettings.difficulty;
		cap = config.enemyCap[d];
		bossCap = Mathf.Min(config.bossCap[d], cap);
		GameObject p = GameObject.FindWithTag("Player");
		player = p != null ? p.transform : null;
		ScheduleNext();
	}

	void ScheduleNext() {
		nextInterval = intervalOverride > 0f ? intervalOverride : Random.Range(intervalMin, intervalMax);
		timer = 0f;
	}

	bool BossAllowed() {
		int score = GameManager.gm != null ? GameManager.gm.score : 0;
		return elapsed >= bossAfterTime || (bossAfterScore > 0 && score >= bossAfterScore);
	}

	void Update() {
		if (!GameFlow.IsGameplayActive)
			return;
		elapsed += Time.deltaTime;
		if (elapsed < quietTime)
			return;
		timer += Time.deltaTime;
		if (timer < (intervalOverride > 0f ? intervalOverride : nextInterval))
			return;
		ScheduleNext();
		if (AliveCount >= cap)
			return;
		bool boss = BossCount < bossCap && BossAllowed() && bossPrefab != null;
		if (!boss && (creepPrefab == null || creeps.Count + pendingCreeps >= cap - bossCap))
			return;
		Vector3 point;
		if (PickPoint(out point))
			StartCoroutine(Spawn(boss, point));
	}

	// A random spawn point on the NavMesh that is far enough from the robot.
	bool PickPoint(out Vector3 point) {
		var options = new List<Vector3>();
		foreach (Transform t in spawnPoints) {
			NavMeshHit hit;
			if (t != null && (player == null || Vector3.Distance(t.position, player.position) >= minPlayerDistance)
				&& NavMesh.SamplePosition(t.position, out hit, 1.5f, NavMesh.AllAreas))
				options.Add(hit.position);
		}
		point = options.Count > 0 ? options[Random.Range(0, options.Count)] : Vector3.zero;
		return options.Count > 0;
	}

	IEnumerator Spawn(bool boss, Vector3 point) {
		if (boss) pendingBosses++; else pendingCreeps++;
		GameObject marker = Marker(point);
		float t = 0f;
		while (t < telegraphTime) {
			t += Time.deltaTime;   // frozen while paused
			float size = Mathf.Lerp(0.5f, 1.6f, t / telegraphTime);
			if (marker != null)
				marker.transform.localScale = new Vector3(size, 0.01f, size);
			yield return null;
		}
		Destroy(marker);
		if (boss) pendingBosses--; else pendingCreeps--;
		if (!GameFlow.IsGameplayActive)
			yield break;
		GameObject enemy = Instantiate(boss ? bossPrefab : creepPrefab, point, Quaternion.LookRotation(player != null ? Flat(player.position - point) : Vector3.forward));
		EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
		(boss ? bosses : creeps).Add(brain);
	}

	static Vector3 Flat(Vector3 v) {
		v.y = 0f;
		return v.sqrMagnitude > 0.001f ? v : Vector3.forward;
	}

	GameObject Marker(Vector3 point) {
		GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		marker.name = "Enemy Spawn Marker";
		DestroyImmediate(marker.GetComponent<Collider>());   // never blocks the robot, not even for a frame
		marker.transform.position = point + Vector3.up * 0.03f;
		marker.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
		if (telegraphMaterial != null)
			marker.GetComponent<Renderer>().sharedMaterial = telegraphMaterial;
		return marker;
	}

	void OnDisable() {
		StopAllCoroutines();
		pendingBosses = pendingCreeps = 0;
	}
}
