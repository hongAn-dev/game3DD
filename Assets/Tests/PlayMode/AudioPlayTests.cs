using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class AudioPlayTests {

	readonly List<SfxEvent> played = new List<SfxEvent>();

	void Record(SfxEvent e) {
		played.Add(e);
	}

	int Count(SfxEvent e) {
		return played.Count(p => p == e);
	}

	[UnitySetUp]
	public IEnumerator On() {
		played.Clear();
		Sfx.Played += Record;
		SoundSettings.SfxEnabled = true;
		Sfx.StopAll();
		yield return new WaitForSecondsRealtime(Sfx.Cooldown + 0.05f);   // no cooldown left over from the last test
	}

	[TearDown]
	public void Off() {
		Sfx.Played -= Record;
		GameFlow.ResetForScene();
		SoundSettings.SfxEnabled = true;
		Sfx.StopAll();
	}

	[UnityTest]
	public IEnumerator VoiceAndCooldownLimits() {
		Assert.IsTrue(Sfx.Play(SfxEvent.Lose));
		Assert.IsFalse(Sfx.Play(SfxEvent.Lose), "cooldown for the same event");
		yield return new WaitForSecondsRealtime(0.15f);
		Assert.IsTrue(Sfx.Play(SfxEvent.Lose));
		yield return new WaitForSecondsRealtime(0.15f);
		Assert.IsFalse(Sfx.Play(SfxEvent.Lose), "at most two voices of one event");
		foreach (SfxEvent e in System.Enum.GetValues(typeof(SfxEvent)))
			Sfx.Play(e);
		Assert.LessOrEqual(Sfx.ActiveVoices, Sfx.MaxVoices);
		yield return null;
	}

	[UnityTest]
	public IEnumerator MuteSilencesAtOnce() {
		Assert.IsTrue(Sfx.Play(SfxEvent.CampaignWin));
		Assert.AreEqual(1, Sfx.ActiveVoices);
		SoundSettings.SfxEnabled = false;
		Assert.AreEqual(0, Sfx.ActiveVoices, "silent at once");
		Assert.IsFalse(Sfx.Play(SfxEvent.Heal), "nothing new while muted");
		SoundSettings.SfxEnabled = true;
		Assert.AreEqual(0, Sfx.ActiveVoices, "no catch-up after unmuting");
		yield return null;
	}

	GameObject player;

	IEnumerator Level1() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;
		player = GameObject.FindWithTag("Player");
		Object.FindObjectOfType<EnemyDirector>().enabled = false;
		yield return new WaitForSeconds(1.2f);   // the robot has landed
		played.Clear();
	}

	// Sources that are not the Sfx voices, music or ambience (an explosion prefab's own sound would be one).
	static int StraySourcesPlaying() {
		return Object.FindObjectsOfType<AudioSource>().Count(a => a.isPlaying && a.GetComponent<SfxHost>() == null && a.GetComponent<SoundGroup>() == null);
	}

	[UnityTest]
	public IEnumerator EnergyPickupPlaysOneSound() {
		yield return Level1();
		Treasure core = Object.FindObjectOfType<Treasure>();
		player.GetComponent<Rigidbody>().position = core.transform.position;
		player.transform.position = core.transform.position;
		for (int i = 0; i < 4; i++)
			yield return new WaitForFixedUpdate();
		yield return null;
		Assert.IsTrue(core == null, "picked up");
		Assert.AreEqual(1, Count(SfxEvent.EnergyPickup));
		Assert.AreEqual(0, StraySourcesPlaying(), "no second sound from the effect");
		Assert.AreEqual(1, played.Count, "nothing else");
	}

	[UnityTest]
	public IEnumerator RobotHitAndHealSounds() {
		yield return Level1();
		Health health = player.GetComponent<Health>();
		health.TakeDamage(10f, DamageKind.EnemyAttack);
		health.TakeDamage(10f, DamageKind.EnemyAttack);   // invulnerable: no second hit sound
		Assert.AreEqual(1, Count(SfxEvent.RobotHit));
		yield return new WaitForSeconds(0.15f);
		health.Heal(5f);
		Assert.AreEqual(1, Count(SfxEvent.Heal));
		SupportPickup.Apply(SupportKind.Shield, player);
		SupportPickup.Apply(SupportKind.Overdrive, player);
		Assert.AreEqual(1, Count(SfxEvent.ShieldOn));
		Assert.AreEqual(1, Count(SfxEvent.OverdriveOn));
		yield return new WaitForSeconds(0.9f);
		health.TakeDamage(10f, DamageKind.EnemyAttack);
		Assert.AreEqual(1, Count(SfxEvent.ShieldBlock));
		yield return new WaitForSeconds(4.4f);
		Assert.AreEqual(1, Count(SfxEvent.ShieldOff), "shield ran out");
		Assert.AreEqual(1, Count(SfxEvent.OverdriveOff), "overdrive ran out");
	}

	[UnityTest]
	public IEnumerator WinFadesMusicByTimeAndPlaysOneCue() {
		yield return Level1();
		AudioSource music = GameManager.gm.backgroundMusic;
		float start = music.volume;
		Assert.Greater(start, 0f);
		GameManager.gm.Collect(GameManager.gm.BeatLevelScore);
		yield return null;
		float t0 = Time.unscaledTime;
		yield return new WaitForSecondsRealtime(GameManager.MusicFade * 0.5f);
		float half = music.volume / start, elapsed = (Time.unscaledTime - t0) / GameManager.MusicFade;
		Assert.That(half, Is.EqualTo(1f - elapsed).Within(0.2f), "linear in time, not per frame");
		yield return new WaitForSecondsRealtime(GameManager.MusicFade * 0.5f + 0.6f);
		Assert.AreEqual(0f, music.volume, 0.001f);
		Assert.AreEqual(1, Count(SfxEvent.LevelWin));
		Assert.AreEqual(0, Count(SfxEvent.Lose));
	}

	[UnityTest]
	public IEnumerator DeathPlaysOneLoseCue() {
		yield return Level1();
		player.GetComponent<Health>().TakeDamage(0f, DamageKind.FatalHazard);
		yield return new WaitForSecondsRealtime(GameManager.MusicFade + 0.6f);
		Assert.AreEqual(1, Count(SfxEvent.Lose));
		Assert.AreEqual(1, Count(SfxEvent.RobotDown));
		Assert.AreEqual(0, Count(SfxEvent.LevelWin));
		Assert.AreEqual(0f, GameManager.gm.backgroundMusic.volume, 0.001f);
	}
}
