using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class OverdrivePlayTests {

	Ball ball;

	IEnumerator Load(string level = "Level2") {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene(level);
		yield return null;
		yield return null;
		GameObject player = GameObject.FindWithTag("Player");
		player.GetComponent<Health>().numberOfLives = 999;
		ball = player.GetComponent<Ball>();
		Object.FindObjectOfType<EnemyDirector>().enabled = false;   // no interference
	}

	Overdrive Boost() {
		Overdrive overdrive = ball.gameObject.AddComponent<Overdrive>();
		overdrive.Activate();
		return overdrive;
	}

	[UnityTest]
	public IEnumerator BoostRaisesTopSpeedForFiveSeconds() {
		yield return Load();
		float baseSpeed = ball.CurrentMaxSpeed;
		Boost();
		Assert.AreEqual(baseSpeed * 1.3f, ball.CurrentMaxSpeed, 0.01f);
		Assert.AreEqual(1.15f, ball.AccelMultiplier, 0.001f);
		Assert.AreEqual(baseSpeed, ball.MaxSpeed, 0.001f, "base value untouched");
		yield return new WaitForSeconds(4.5f);
		Assert.AreEqual(baseSpeed * 1.3f, ball.CurrentMaxSpeed, 0.01f, "still boosted at 4.5 s");
		yield return new WaitForSeconds(0.8f);
		Assert.Less(ball.CurrentMaxSpeed, baseSpeed * 1.3f, "easing back");
		Assert.Greater(ball.CurrentMaxSpeed, baseSpeed, "smoothly, not at once");
		yield return new WaitForSeconds(0.8f);
		Assert.AreEqual(baseSpeed, ball.CurrentMaxSpeed, 0.01f, "back to base");
		Assert.IsFalse(Object.FindObjectOfType<OverdriveIndicator>().content.activeSelf, "countdown hidden");
	}

	[UnityTest]
	public IEnumerator RepickRefreshesWithoutStacking() {
		yield return Load();
		float baseSpeed = ball.CurrentMaxSpeed;
		Overdrive overdrive = Boost();
		yield return new WaitForSeconds(2f);
		overdrive.Activate();
		Assert.AreEqual(Overdrive.Duration, overdrive.Remaining, 0.05f);
		yield return null;
		Assert.AreEqual(baseSpeed * 1.3f, ball.CurrentMaxSpeed, 0.01f, "no ×1.69");
	}

	[UnityTest]
	public IEnumerator PauseFreezesBoost() {
		yield return Load();
		Overdrive overdrive = Boost();
		yield return null;
		GameFlow.Pause();
		float before = overdrive.Remaining;
		yield return new WaitForSecondsRealtime(1.5f);
		Assert.AreEqual(before, overdrive.Remaining, 0.001f);
		GameFlow.Resume();
		// Dead/won keep timeScale 1: the timer must still hold.
		GameFlow.Die("test");
		before = overdrive.Remaining;
		yield return new WaitForSeconds(1f);
		Assert.AreEqual(before, overdrive.Remaining, 0.001f, "frozen after the level ended");
	}

	[UnityTest]
	public IEnumerator PickupDoesNotScore() {
		yield return Load();
		int score = GameManager.gm.score;
#if UNITY_EDITOR
		GameObject pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Overdrive.prefab"), ball.transform.position, Quaternion.identity);
#else
		GameObject pickup = null;
#endif
		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();
		yield return null;
		Assert.IsTrue(pickup == null, "taken");
		Overdrive overdrive = ball.GetComponent<Overdrive>();
		Assert.IsNotNull(overdrive);
		Assert.IsTrue(overdrive.Active);
		Assert.AreEqual(score, GameManager.gm.score, "not energy");
		Assert.IsTrue(Object.FindObjectOfType<OverdriveIndicator>().content.activeSelf, "countdown shown");
	}

	[UnityTest]
	public IEnumerator DirectorKeepsAtMostOneBoostAndReplacesItAfterPickup() {
		yield return Load();
		OverdriveDirector director = Object.FindObjectOfType<OverdriveDirector>();
		EnergySpawnDirector energy = Object.FindObjectOfType<EnergySpawnDirector>();
		director.intervalOverride = 0.1f;
		ball.GetComponent<Rigidbody>().isKinematic = true;   // stays away from the pickups
		yield return new WaitForSeconds(0.5f);
		GameObject first = director.Current;
		Assert.IsNotNull(first);
		Object.Destroy(first);   // as if the robot took it
		int max = 0;
		float end = Time.time + 1.5f;
		while (Time.time < end) {
			max = Mathf.Max(max, Object.FindObjectsOfType<SupportPickup>().Length);
			yield return null;
		}
		Assert.AreEqual(1, max, "one at a time");
		Assert.IsNotNull(director.Current, "a new one after the pickup");
		Assert.AreNotSame(first, director.Current);
		Assert.GreaterOrEqual(Vector3.Distance(director.Current.transform.position, ball.transform.position), director.minPlayerDistance);
		foreach (Vector3 core in energy.Occupied())
			if (core != director.Current.transform.position)
				Assert.GreaterOrEqual(Vector3.Distance(core, director.Current.transform.position), director.minCoreDistance);
	}

	// Energy drops must not land on the boost (and the boost not on a core that is still falling).
	[UnityTest]
	public IEnumerator CoresAndBoostNeverShareAPoint() {
		yield return Load();
		OverdriveDirector director = Object.FindObjectOfType<OverdriveDirector>();
		EnergySpawnDirector energy = Object.FindObjectOfType<EnergySpawnDirector>();
		ball.GetComponent<Rigidbody>().isKinematic = true;
		director.intervalOverride = 0.1f;
		energy.intervalOverride = 0.1f;
		float end = Time.time + 6f;
		while (Time.time < end) {
			if (director.Current != null)
				foreach (Treasure core in Object.FindObjectsOfType<Treasure>())
					Assert.Greater(Vector3.Distance(Flat(core.transform.position), Flat(director.Current.transform.position)), 1f, "core on the boost");
			if (director.Current != null && Time.frameCount % 20 == 0)
				Object.Destroy(director.Current);   // keep boosts coming back while cores drop
			yield return null;
		}
	}

	static Vector3 Flat(Vector3 v) {
		v.y = 0f;
		return v;
	}

	[UnityTest]
	public IEnumerator Level1HasNoBoost() {
		yield return Load("Level1");
		Assert.IsNull(Object.FindObjectOfType<OverdriveDirector>());
	}

	[UnityTest]
	public IEnumerator RetryClearsBoost() {
		yield return Load();
		Boost();
		SceneRouter.Retry();
		yield return null;
		yield return null;
		Ball fresh = GameObject.FindWithTag("Player").GetComponent<Ball>();
		Assert.IsNull(fresh.GetComponent<Overdrive>());
		Assert.AreEqual(fresh.MaxSpeed, fresh.CurrentMaxSpeed, 0.001f);
		Assert.IsFalse(Object.FindObjectOfType<OverdriveIndicator>().content.activeSelf, "countdown hidden after retry");
	}
}
