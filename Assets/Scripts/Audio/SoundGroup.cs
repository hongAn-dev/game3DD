using UnityEngine;

/// <summary>
/// Puts an AudioSource (music, ambience bed, cinematic sound) in a volume group: its authored volume is scaled by the
/// group's setting, live. SFX-group sources (Ending cinematic) also go silent when SFX are muted.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundGroup : MonoBehaviour {

	public enum Group { Music, Ambience, Sfx }

	public Group group;
	[Tooltip("Authored volume before the group setting.")]
	public float baseVolume = 1f;

	float fade = 1f;

	/// <summary>Runtime fade (0..1, e.g. music fading out on a result) kept across settings changes.</summary>
	public float Fade {
		get { return fade; }
		set { fade = Mathf.Clamp01(value); Apply(); }
	}

	AudioSource source;

	void Awake() {
		source = GetComponent<AudioSource>();
		Apply();
		SoundSettings.Changed += Apply;
	}

	void OnDestroy() {
		SoundSettings.Changed -= Apply;
	}

	public float GroupVolume() {
		switch (group) {
		case Group.Music: return SoundSettings.MusicGain;
		case Group.Ambience: return SoundSettings.SfxEnabled ? SoundSettings.AmbienceVolume : 0f;
		default: return SoundSettings.SfxEnabled ? SoundSettings.SfxVolume : 0f;
		}
	}

	// Timeline clip fades live in its audio playables. Reapply the authored source gain after
	// track evaluation so Timeline cannot overwrite the user volume or group mute.
	void LateUpdate() { Apply(); }
	void ApplyMute() {
		if (source != null) source.mute = group == Group.Music ? !SoundSettings.MusicEnabled : !SoundSettings.SfxEnabled;
	}

	public void Apply() {
		ApplyMute();
		if (source != null)
			source.volume = baseVolume * GroupVolume() * fade;
	}
}
