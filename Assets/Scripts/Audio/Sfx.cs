using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The one way to play a sound effect (spec §8.2): looks the clip up in AudioCatalog and plays it on a pooled 2D
/// voice. At most MaxVoices at once and MaxPerEvent of the same event, Cooldown seconds between two of one event;
/// important feedback (hits, boss, results) may take over the oldest decorative voice. Nothing plays while SFX are
/// muted. Voices survive scene loads only for UI clicks (the click that loaded the scene).
/// </summary>
public static class Sfx {

	public const int MaxVoices = 8;
	public const int MaxPerEvent = 2;
	public const float Cooldown = 0.1f;

	static SfxHost host;

	public static bool Play(SfxEvent e) {
		if (!SoundSettings.SfxEnabled)
			return false;
		if (host == null) {
			GameObject go = new GameObject("Sfx Voices");
			Object.DontDestroyOnLoad(go);
			host = go.AddComponent<SfxHost>();
		}
		return host.Play(e);
	}

	public static int ActiveVoices { get { return host != null ? host.Active : 0; } }

	public static void StopAll() {
		if (host != null)
			host.StopAll(false);
	}

	public static bool Important(SfxEvent e) {
		return e == SfxEvent.RobotHit || e == SfxEvent.RobotDown || e == SfxEvent.BossWarn || e == SfxEvent.BossStrike
			|| e == SfxEvent.Lose || e == SfxEvent.ShieldBlock;
	}
}

public class SfxHost : MonoBehaviour {

	AudioSource[] voices;
	SfxEvent[] playing;
	float[] started;
	float[] lastPlayed;
	AudioCatalog catalog;

	void Awake() {
		catalog = Resources.Load<AudioCatalog>("AudioCatalog");
		voices = new AudioSource[Sfx.MaxVoices];
		playing = new SfxEvent[Sfx.MaxVoices];
		started = new float[Sfx.MaxVoices];
		lastPlayed = new float[System.Enum.GetValues(typeof(SfxEvent)).Length];
		for (int i = 0; i < lastPlayed.Length; i++)
			lastPlayed[i] = -10f;
		for (int i = 0; i < voices.Length; i++) {
			voices[i] = gameObject.AddComponent<AudioSource>();
			voices[i].playOnAwake = false;
			voices[i].spatialBlend = 0f;
			voices[i].ignoreListenerPause = true;   // UI feedback still works in the pause menu
			voices[i].Stop();   // a freshly added source counts as playing (playOnAwake) until stopped
		}
		SceneManager.sceneLoaded += (scene, mode) => StopAll(true);
	}

	public int Active {
		get {
			int n = 0;
			foreach (AudioSource v in voices)
				if (v.isPlaying && v.clip != null)
					n++;
			return n;
		}
	}

	public bool Play(SfxEvent e) {
		AudioClip clip = catalog != null ? catalog.Clip(e) : null;
		if (clip == null || Time.unscaledTime - lastPlayed[(int)e] < Sfx.Cooldown)
			return false;
		int same = 0, free = -1, oldestDecor = -1;
		for (int i = 0; i < voices.Length; i++) {
			if (!voices[i].isPlaying || voices[i].clip == null) {
				if (free < 0)
					free = i;
				continue;
			}
			if (playing[i] == e)
				same++;
			if (!Sfx.Important(playing[i]) && (oldestDecor < 0 || started[i] < started[oldestDecor]))
				oldestDecor = i;
		}
		if (same >= Sfx.MaxPerEvent)
			return false;
		int voice = free >= 0 ? free : Sfx.Important(e) ? oldestDecor : -1;
		if (voice < 0)
			return false;
		AudioSource source = voices[voice];
		source.Stop();
		source.clip = clip;
		source.volume = catalog.Volume(e) * SoundSettings.SfxVolume;
		source.Play();
		playing[voice] = e;
		started[voice] = Time.unscaledTime;
		lastPlayed[(int)e] = Time.unscaledTime;
		return true;
	}

	public void StopAll(bool keepClicks) {
		for (int i = 0; i < voices.Length; i++)
			if (!(keepClicks && playing[i] == SfxEvent.UiClick))
				voices[i].Stop();
	}
}
