using System;
using NUnit.Framework;
using UnityEngine;

public class AudioTests {

	static readonly (SfxEvent e, float min, float max)[] Lengths = {
		(SfxEvent.UiClick, 0.05f, 0.15f), (SfxEvent.EnergyPickup, 0.15f, 0.30f), (SfxEvent.Heal, 0.25f, 0.45f),
		(SfxEvent.ShieldOn, 0.1f, 0.4f), (SfxEvent.ShieldBlock, 0.1f, 0.4f), (SfxEvent.ShieldOff, 0.1f, 0.4f),
		(SfxEvent.RobotHit, 0.15f, 0.30f), (SfxEvent.CreepAttack, 0.15f, 0.35f), (SfxEvent.BossWarn, 0.2f, 0.6f),
		(SfxEvent.BossStrike, 0.2f, 0.6f), (SfxEvent.Lose, 0.6f, 0.9f), (SfxEvent.LevelWin, 0.7f, 1.5f),
		(SfxEvent.CampaignWin, 0.7f, 1.5f), (SfxEvent.OverdriveOn, 0.2f, 0.4f), (SfxEvent.OverdriveOff, 0.2f, 0.4f),
	};

	// Spec §8.2: one clip per event, lengths from the table, no clipping (peak <= -1 dBFS).
	[Test]
	public void CatalogCoversEveryEvent() {
		AudioCatalog catalog = Resources.Load<AudioCatalog>("AudioCatalog");
		Assert.IsNotNull(catalog);
		foreach (SfxEvent e in Enum.GetValues(typeof(SfxEvent)))
			Assert.IsNotNull(catalog.Clip(e), e.ToString());
		foreach (var (e, min, max) in Lengths) {
			AudioClip clip = catalog.Clip(e);
			Assert.That(clip.length, Is.InRange(min, max), e + " length");
			float[] data = new float[clip.samples * clip.channels];
			Assert.IsTrue(clip.GetData(data, 0), e + " readable");
			float peak = 0f;
			foreach (float v in data)
				peak = Mathf.Max(peak, Mathf.Abs(v));
			Assert.LessOrEqual(peak, Mathf.Pow(10f, -1f / 20f), e + " peak");
		}
		Assert.Greater(catalog.Clip(SfxEvent.CampaignWin).length, catalog.Clip(SfxEvent.LevelWin).length, "campaign win is longer");
	}

	[Test]
	public void SettingsPersistAndDefaultOn() {
		PlayerPrefs.DeleteKey(SoundSettings.SfxKey);
		SoundSettings.Reload();
		Assert.IsTrue(SoundSettings.SfxEnabled, "on at first run");
		SoundSettings.SfxEnabled = false;
		Assert.AreEqual(0, PlayerPrefs.GetInt(SoundSettings.SfxKey, 1));
		SoundSettings.Reload();
		Assert.IsFalse(SoundSettings.SfxEnabled, "remembered");
		SoundSettings.SfxEnabled = true;
		SoundSettings.MusicVolume = 0.4f;
		SoundSettings.Reload();
		Assert.AreEqual(0.4f, SoundSettings.MusicVolume, 0.001f);
		SoundSettings.MusicVolume = 1f;
	}
}
