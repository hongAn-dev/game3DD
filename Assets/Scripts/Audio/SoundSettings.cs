using UnityEngine;

/// <summary>
/// Player sound settings, saved on the device (spec §7.6, §8.2): the quick SFX toggle (SFX + UI, default on) and the
/// Music / SFX / Ambience volumes (0..1). Muting stops every SFX voice at once; unmuting replays nothing.
/// </summary>
public static class SoundSettings {

	public const string SfxKey = "sfx_enabled";
	const string MusicKey = "volume_music", SfxVolumeKey = "volume_sfx", AmbienceKey = "volume_ambience";

	public static event System.Action Changed;

	static bool loaded, sfxEnabled;
	static float music, sfx, ambience;

	public static void Reload() {
		sfxEnabled = PlayerPrefs.GetInt(SfxKey, 1) == 1;
		music = PlayerPrefs.GetFloat(MusicKey, 1f);
		sfx = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
		ambience = PlayerPrefs.GetFloat(AmbienceKey, 1f);
		loaded = true;
	}

	static void Load() {
		if (!loaded)
			Reload();
	}

	public static bool SfxEnabled {
		get { Load(); return sfxEnabled; }
		set {
			Load();
			sfxEnabled = value;
			PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
			if (!value)
				Sfx.StopAll();
			Raise();
		}
	}

	public static float MusicVolume { get { Load(); return music; } set { Load(); music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, music); Raise(); } }
	public static float SfxVolume { get { Load(); return sfx; } set { Load(); sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxVolumeKey, sfx); Raise(); } }
	public static float AmbienceVolume { get { Load(); return ambience; } set { Load(); ambience = Mathf.Clamp01(value); PlayerPrefs.SetFloat(AmbienceKey, ambience); Raise(); } }

	static void Raise() {
		if (Changed != null)
			Changed();
	}
}
