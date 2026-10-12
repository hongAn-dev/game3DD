using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChallengeGameplayPlayTests {
	GameManager manager;
	GameObject playerObject;
	Health health;
	Ball ball;
	Rigidbody body;
	float savedFixedDelta;
	float savedMaximumDelta;

	IEnumerator LoadLevel1() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;
		yield return null;

		manager = GameManager.gm;
		playerObject = GameObject.FindWithTag("Player");
		Assert.IsNotNull(manager);
		Assert.IsNotNull(playerObject);
		health = playerObject.GetComponent<Health>();
		ball = playerObject.GetComponent<Ball>();
		body = playerObject.GetComponent<Rigidbody>();
		health.numberOfLives = 999;
		health.onLivesGone = Health.deathAction.doNothingWhenDead;
		body.isKinematic = true;
		body.velocity = Vector3.zero;
		body.angularVelocity = Vector3.zero;
		BallUserControl control = playerObject.GetComponent<BallUserControl>();
		if (control != null) control.enabled = false;

		EnemyDirector enemies = Object.FindObjectOfType<EnemyDirector>();
		if (enemies != null) enemies.enabled = false;
		EnergySpawnDirector energy = Object.FindObjectOfType<EnergySpawnDirector>();
		if (energy != null) energy.enabled = false;
		SupportSpawnDirector support = Object.FindObjectOfType<SupportSpawnDirector>();
		if (support != null) support.enabled = false;
		HarmfulSpawnDirector harmful = Object.FindObjectOfType<HarmfulSpawnDirector>();
		if (harmful != null) harmful.enabled = false;
		foreach (Treasure treasure in Object.FindObjectsOfType<Treasure>())
			Object.Destroy(treasure.gameObject);

		GameFlow.ResetForScene();
		manager.gameState = GameManager.gameStates.Playing;
		manager.canBeatLevel = false;
		manager.score = 0;
	}

	[TearDown]
	public void Cleanup() {
		GameFlow.ResetForScene();
		Time.fixedDeltaTime = savedFixedDelta > 0f ? savedFixedDelta : 0.02f;
		Time.maximumDeltaTime = savedMaximumDelta > 0f ? savedMaximumDelta : 0.3333333f;
	}

	GameObject Pickup(string path, Vector3 position) {
#if UNITY_EDITOR
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		Assert.IsNotNull(prefab, "missing pickup prefab: " + path);
		return Object.Instantiate(prefab, position, Quaternion.identity);
#else
		return null;
#endif
	}

	void Trigger(GameObject pickup) {
		Collider playerCollider = playerObject.GetComponent<Collider>();
		Assert.IsNotNull(playerCollider);
		pickup.SendMessage("OnTriggerEnter", playerCollider, SendMessageOptions.DontRequireReceiver);
	}

	IEnumerator VerifyPickupPair(int startingScore, bool energyFirst) {
		manager.canBeatLevel = true;
		manager.score = startingScore;
		int feedbacks = 0;
		int commits = 0;
		int committedDelta = int.MinValue;
		System.Action<int> feedback = _ => feedbacks++;
		System.Action<int, int> committed = (delta, score) => { commits++; committedDelta = delta; };
		manager.ScoreFeedback += feedback;
		manager.ScoreDeltaCommitted += committed;

		Vector3 staging = playerObject.transform.position + Vector3.up * 20f;
		GameObject energy = Pickup("Assets/Prefabs/Coin.prefab", staging);
		GameObject anti = Pickup("Assets/Prefabs/Harmful/AntiEnergy.prefab", staging + Vector3.right * 2f);
		Treasure treasure = energy.GetComponent<Treasure>();
		HarmfulPickup antiEffect = anti.GetComponent<HarmfulPickup>();
		Assert.IsNotNull(treasure);
		Assert.IsNotNull(antiEffect);
		treasure.value = 1;
		antiEffect.kind = HarmfulKind.AntiEnergy;

		if (energyFirst) {
			Trigger(energy);
			Trigger(anti);
		} else {
			Trigger(anti);
			Trigger(energy);
		}
		Assert.IsTrue(antiEffect.Consumed);

		float deadline = Time.time + 1f;
		while (commits == 0 && Time.time < deadline)
			yield return null;
		manager.ScoreFeedback -= feedback;
		manager.ScoreDeltaCommitted -= committed;

		int target = LevelCatalog.Get("Level1").energyTarget;
		Assert.AreEqual(startingScore, manager.score, "both pickup deltas commit as one physics-step total");
		Assert.AreEqual(1, commits);
		Assert.AreEqual(0, committedDelta);
		Assert.AreEqual(FlowState.Playing, GameFlow.State, "the intermediate +1 cannot win before -1 is included");
		Assert.AreEqual(GameManager.gameStates.Playing, manager.gameState);
		Assert.AreEqual(startingScore == 0 ? 0 : 1, feedbacks, "no loss feedback when score was already zero");
		Assert.Greater(target, 0);
	}

	[UnityTest]
	public IEnumerator EnergyThenAntiEnergyAtTargetMinusOneDoesNotWin() {
		yield return LoadLevel1();
		int target = LevelCatalog.Get("Level1").energyTarget;
		yield return VerifyPickupPair(target - 1, true);
	}

	[UnityTest]
	public IEnumerator AntiEnergyThenEnergyAtTargetMinusOneDoesNotWin() {
		yield return LoadLevel1();
		int target = LevelCatalog.Get("Level1").energyTarget;
		yield return VerifyPickupPair(target - 1, false);
	}

	[UnityTest]
	public IEnumerator EnergyThenAntiEnergyAtZeroClampsOnceWithoutFeedback() {
		yield return LoadLevel1();
		yield return VerifyPickupPair(0, true);
	}

	[UnityTest]
	public IEnumerator AntiEnergyThenEnergyAtZeroClampsOnceWithoutFeedback() {
		yield return LoadLevel1();
		yield return VerifyPickupPair(0, false);
	}

	[UnityTest]
	public IEnumerator AScoreSourceContributesOnlyOnce() {
		yield return LoadLevel1();
		GameObject source = new GameObject("Score delta source");
		int commits = 0;
		System.Action<int, int> committed = (delta, score) => commits++;
		manager.ScoreDeltaCommitted += committed;
		Assert.IsTrue(manager.QueueScoreDelta(source, 1));
		Assert.IsFalse(manager.QueueScoreDelta(source, -1), "the same source cannot contribute twice in one physics step");
		float deadline = Time.time + 1f;
		while (commits == 0 && Time.time < deadline)
			yield return null;
		manager.ScoreDeltaCommitted -= committed;
		Assert.AreEqual(1, commits);
		Assert.AreEqual(1, manager.score);
		Object.Destroy(source);
	}

	[UnityTest]
	public IEnumerator LateScoreCallbackAfterResultIsIgnored() {
		yield return LoadLevel1();
		manager.score = 2;
		int commits = 0;
		System.Action<int, int> committed = (delta, score) => commits++;
		manager.ScoreDeltaCommitted += committed;
		Assert.IsTrue(GameFlow.CompleteLevel());
		GameObject source = new GameObject("Late pickup");
		Assert.IsFalse(manager.QueueScoreDelta(source, 1));
		yield return null;
		manager.ScoreDeltaCommitted -= committed;
		Assert.AreEqual(2, manager.score);
		Assert.AreEqual(0, commits);
		Assert.AreEqual(FlowState.LevelComplete, GameFlow.State);
		Object.Destroy(source);
	}

	[UnityTest]
	public IEnumerator AntiEnergyAtZeroIsConsumedWithoutLossFeedback() {
		yield return LoadLevel1();
		manager.score = 0;
		int feedbacks = 0;
		System.Action<int> feedback = _ => feedbacks++;
		manager.ScoreFeedback += feedback;
		GameObject anti = Pickup("Assets/Prefabs/Harmful/AntiEnergy.prefab", playerObject.transform.position + Vector3.up * 20f);
		HarmfulPickup effect = anti.GetComponent<HarmfulPickup>();
		Assert.IsNotNull(effect);
		effect.kind = HarmfulKind.AntiEnergy;
		Trigger(anti);
		Assert.IsTrue(effect.Consumed, "the pickup is spent even at zero score");
		yield return new WaitForFixedUpdate();
		yield return null;
		manager.ScoreFeedback -= feedback;
		Assert.AreEqual(0, manager.score);
		Assert.AreEqual(0, feedbacks);
	}

	[UnityTest]
	public IEnumerator PoisonIsConsumedOnceDuringSharedImmunityAndShield() {
		yield return LoadLevel1();
		int damageEvents = 0;
		int blockedEvents = 0;
		health.Damaged += _ => damageEvents++;
		health.Blocked += () => blockedEvents++;
		Assert.IsTrue(health.TakeDamage(10f, DamageKind.EnemyAttack));
		Assert.Greater(health.InvulnerableLeft, 0f);

		GameObject immunePickup = Pickup("Assets/Prefabs/Harmful/Poison.prefab", playerObject.transform.position + Vector3.up * 20f);
		HarmfulPickup immuneEffect = immunePickup.GetComponent<HarmfulPickup>();
		Assert.IsNotNull(immuneEffect);
		immuneEffect.kind = HarmfulKind.Poison;
		immuneEffect.damage = 25;
		int immuneGone = 0;
		immuneEffect.Gone += _ => immuneGone++;
		Trigger(immunePickup);
		Trigger(immunePickup);
		Assert.IsTrue(immuneEffect.Consumed);
		Assert.AreEqual(1, immuneGone);
		Assert.AreEqual(90f, health.healthPoints, 0.01f, "the shared 0.8 s immunity blocks poison after an enemy hit");

		yield return new WaitForSeconds(Health.InvulnerableSeconds + 0.1f);
		ShieldEffect shield = playerObject.GetComponent<ShieldEffect>();
		if (shield == null) shield = playerObject.AddComponent<ShieldEffect>();
		shield.Activate();
		GameObject shieldPickup = Pickup("Assets/Prefabs/Harmful/Poison.prefab", playerObject.transform.position + Vector3.up * 20f);
		HarmfulPickup shieldEffect = shieldPickup.GetComponent<HarmfulPickup>();
		shieldEffect.kind = HarmfulKind.Poison;
		shieldEffect.damage = 25;
		int shieldGone = 0;
		shieldEffect.Gone += _ => shieldGone++;
		Trigger(shieldPickup);
		Trigger(shieldPickup);
		Assert.IsTrue(shieldEffect.Consumed);
		Assert.AreEqual(1, shieldGone);
		Assert.AreEqual(1, blockedEvents, "the shield blocks a poison hit through Health.TakeDamage");
		Assert.AreEqual(1, damageEvents, "blocked poison does not emit another damage event");
		Assert.AreEqual(90f, health.healthPoints, 0.01f);
	}

	[UnityTest]
	public IEnumerator SlowComposesWithBoostRefreshesFreezesAndClearsOnResult() {
		yield return LoadLevel1();
		savedFixedDelta = Time.fixedDeltaTime;
		float baseSpeed = ball.MaxSpeed;
		Assert.AreEqual(8.8f, baseSpeed, 0.01f);
		Overdrive boost = playerObject.GetComponent<Overdrive>();
		if (boost == null) boost = playerObject.AddComponent<Overdrive>();
		SlowStatus slow = playerObject.GetComponent<SlowStatus>();
		if (slow == null) slow = playerObject.AddComponent<SlowStatus>();
		boost.Activate();
		slow.Apply();
		Assert.AreEqual(8.58f, ball.CurrentMaxSpeed, 0.02f, "8.8 × 1.3 × 0.75");

		GameFlow.Pause();
		float pausedSlow = slow.Remaining;
		float pausedBoost = boost.Remaining;
		yield return new WaitForSecondsRealtime(0.5f);
		Assert.AreEqual(pausedSlow, slow.Remaining, 0.001f);
		Assert.AreEqual(pausedBoost, boost.Remaining, 0.001f);
		GameFlow.Resume();

		yield return new WaitForSeconds(1.2f);
		slow.Apply();
		Assert.AreEqual(SlowStatus.Duration, slow.Remaining, 0.02f, "repicking refreshes instead of stacking");
		Assert.AreEqual(8.58f, ball.CurrentMaxSpeed, 0.02f, "refresh never multiplies the slow factor");
		yield return new WaitForSeconds(2.2f);
		Assert.IsTrue(slow.Active, "the refreshed slow lasts to its own deadline");
		Assert.IsTrue(boost.Active);
		yield return new WaitForSeconds(1f);
		Assert.IsFalse(slow.Active, "slow expires before the original five-second boost");
		Assert.IsTrue(boost.Active);
		Assert.AreEqual(baseSpeed * Overdrive.SpeedBoost, ball.CurrentMaxSpeed, 0.03f);
		yield return new WaitForSeconds(1f);
		Assert.IsFalse(boost.Active);
		Assert.Greater(ball.CurrentMaxSpeed, baseSpeed, "boost easing remains independent after slow ends");
		yield return new WaitForSeconds(Overdrive.EaseOut + 0.1f);
		Assert.AreEqual(baseSpeed, ball.CurrentMaxSpeed, 0.03f);

		slow.Apply();
		Assert.IsTrue(slow.Active);
		Assert.IsTrue(GameFlow.Die("test result"));
		Assert.IsFalse(slow.Active, "result state clears the status timer");
		Assert.AreEqual(1f, ball.SlowMultiplier, 0.001f);
	}

	[UnityTest]
	public IEnumerator PostPhysicsHookCommitsBetweenCatchUpFixedSteps() {
		yield return LoadLevel1();
		Assert.IsTrue(GameManager.HasPostPhysicsScoreHook, "the PlayerLoop hook must follow PhysicsFixedUpdate");
		manager.canBeatLevel = false;
		manager.score = 0;
		savedFixedDelta = Time.fixedDeltaTime;
		savedMaximumDelta = Time.maximumDeltaTime;
		Time.fixedDeltaTime = 0.01f;
		Time.maximumDeltaTime = 0.5f;

		ChallengeFixedStepProducer producer = new GameObject("Fixed-step score producer").AddComponent<ChallengeFixedStepProducer>();
		producer.manager = manager;
		producer.startingScore = manager.score;
		int commits = 0;
		System.Action<int, int> committed = (delta, score) => commits++;
		manager.ScoreDeltaCommitted += committed;

		for (int i = 0; i < 12 && producer.maximumStepsPerFrame < 2; i++) {
			Thread.Sleep(50);
			yield return null;
		}
		yield return null;
		manager.ScoreDeltaCommitted -= committed;

		Assert.GreaterOrEqual(producer.maximumStepsPerFrame, 2, "one rendered frame performs multiple physics steps");
		Assert.AreEqual(0, producer.uncommittedPriorSteps, "each physics score was committed before the next FixedUpdate producer");
		Assert.AreEqual(producer.fixedSteps, commits, "each physics step has its own post-physics commit");
		Assert.AreEqual(producer.startingScore + producer.fixedSteps, manager.score);
		Object.Destroy(producer.gameObject);
	}
}

public sealed class ChallengeFixedStepProducer : MonoBehaviour {
	public GameManager manager;
	public int startingScore;
	public int fixedSteps;
	public int maximumStepsPerFrame;
	public int uncommittedPriorSteps;
	int lastFrame = -1;
	int stepsThisFrame;

	void FixedUpdate() {
		if (fixedSteps > 0 && manager.score != startingScore + fixedSteps)
			uncommittedPriorSteps++;
		if (lastFrame == Time.frameCount)
			stepsThisFrame++;
		else {
			lastFrame = Time.frameCount;
			stepsThisFrame = 1;
		}
		maximumStepsPerFrame = Mathf.Max(maximumStepsPerFrame, stepsThisFrame);
		fixedSteps++;
		manager.QueueScoreDelta(1);
	}
}
