using UnityEngine;

/// <summary>Event → clip mapping (Resources/AudioCatalog), filled by AudioSetup from the synthesized set.</summary>
[CreateAssetMenu(menuName = "Robo Lac Loi/Audio Catalog")]
public class AudioCatalog : ScriptableObject {

	public AudioClip[] clips = new AudioClip[System.Enum.GetValues(typeof(SfxEvent)).Length];
	public float[] volumes = new float[System.Enum.GetValues(typeof(SfxEvent)).Length];

	public AudioClip Clip(SfxEvent e) {
		int i = (int)e;
		return clips != null && i < clips.Length ? clips[i] : null;
	}

	public float Volume(SfxEvent e) {
		int i = (int)e;
		return volumes != null && i < volumes.Length && volumes[i] > 0f ? volumes[i] : 1f;
	}
}
