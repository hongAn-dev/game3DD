using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu extras (spec §7.3, §7.6, §8.2): "Hướng dẫn chơi" opens the how-to-play pages in the story dialog (Đóng
/// returns to the menu; no run starts), "Cài đặt" shows camera sensitivity and Music/SFX/Ambience volumes, saved on the
/// device as they move.
/// </summary>
public class MainMenuPanels : MonoBehaviour {

	public DialogPanel dialog;
	public GameObject settings;
	public Slider sensitivity;
	public Slider music;
	public Slider sfx;
	public Slider ambience;

	public void OpenGuide() {
		dialog.ShowClosable(StoryText.Guide(), "Đóng", null);
	}

	// Android Back / Escape closes the guide or the settings, never the app.
	void Update() {
		if (!Input.GetKeyDown(KeyCode.Escape))
			return;
		if (dialog.IsOpen)
			dialog.Finish();
		else if (settings.activeSelf)
			CloseSettings();
	}

	public void OpenSettings() {
		// Set the saved values before showing; the sliders' listeners write them straight back unchanged.
		sensitivity.value = ThirdPersonOrbitCamera.Sensitivity;
		music.value = SoundSettings.MusicVolume;
		sfx.value = SoundSettings.SfxVolume;
		ambience.value = SoundSettings.AmbienceVolume;
		settings.SetActive(true);
	}

	public void CloseSettings() {
		settings.SetActive(false);
		PlayerPrefs.Save();
	}

	public void SetSensitivity(float value) {
		ThirdPersonOrbitCamera.Sensitivity = value;
	}

	public void SetMusic(float value) {
		SoundSettings.MusicVolume = value;
	}

	public void SetSfx(float value) {
		SoundSettings.SfxVolume = value;
	}

	public void SetAmbience(float value) {
		SoundSettings.AmbienceVolume = value;
	}
}
