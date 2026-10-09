using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class AudioPlayTests {

	[UnitySetUp]
	public IEnumerator On() {
		SoundSettings.SfxEnabled = true;
		Sfx.StopAll();
		yield return new WaitForSecondsRealtime(Sfx.Cooldown + 0.05f);   // no cooldown left over from the last test
	}

	[TearDown]
	public void Off() {
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
}
