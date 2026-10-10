using UnityEngine;

/// <summary>
/// Player sound settings: independent SFX and Music toggles (default on), plus separate group volumes.
/// Effective gains derive from saved volume and mute state, never an already-scaled source volume.
/// </summary>
public static class SoundSettings {

	public const string SfxKey = "sfx_enabled";
	public const string MusicKey = "music_enabled";
	const string MusicVolumeKey = "volume_music", SfxVolumeKey = "volume_sfx", AmbienceKey = "volume_ambience";

	public static event System.Action Changed;

	static bool loaded, sfxEnabled, musicEnabled;
	static float music, sfx, ambience;

	public static void Reload() {
		sfxEnabled = PlayerPrefs.GetInt(SfxKey, 1) == 1;
		musicEnabled = PlayerPrefs.GetInt(MusicKey, 1) == 1;
		music = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
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
			PlayerPrefs.Save();
			Raise();
		}
	}

	public static bool MusicEnabled {
		get { Load(); return musicEnabled; }
		set { Load(); musicEnabled = value; PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); Raise(); }
	}

	public static float MusicVolume { get { Load(); return music; } set { Load(); music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicVolumeKey, music); Raise(); } }
	public static float MusicGain { get { Load(); return musicEnabled ? music : 0f; } }
	public static float SfxGain { get { Load(); return sfxEnabled ? sfx : 0f; } }

	public static float SfxVolume { get { Load(); return sfx; } set { Load(); sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxVolumeKey, sfx); Raise(); } }
	public static float AmbienceVolume { get { Load(); return ambience; } set { Load(); ambience = Mathf.Clamp01(value); PlayerPrefs.SetFloat(AmbienceKey, ambience); Raise(); } }

	static void Raise() {
		if (Changed != null)
			Changed();
	}
}
