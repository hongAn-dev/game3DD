using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu extras (spec §7.3, §7.6, §8.2): "Hướng dẫn chơi" opens the how-to-play pages in the story dialog (Đóng
/// returns to the menu; no run starts), "Cài đặt" shows camera sensitivity and Music/SFX/Ambience volumes, saved on the
/// device as they move. "Chơi tiếp" lists the four levels: unlocked ones start with the last difficulty, locked ones
/// show a padlock and "Khoá" and cannot be tapped. The campaign energy total ("Năng lượng: x/48") sits top right.
/// </summary>
public class MainMenuPanels : MonoBehaviour {

	public DialogPanel dialog;
	public GameObject settings;
	public Slider sensitivity;
	public Slider music;
	public Slider sfx;
	public Slider ambience;
	public GameObject levelSelect;
	public Button[] levelButtons = new Button[0];
	public Text[] levelLabels = new Text[0];
	public Image[] levelLocks = new Image[0];
	public Text totalEnergy;

	void Start() {
		ShowTotal();
		RefreshContinueButton();
	}

	void RefreshContinueButton() {
		GameObject go = GameObject.Find("Continue Button");
		Button button = go != null ? go.GetComponent<Button>() : null;
		if (button != null)
			button.interactable = SaveProgress.HasAnyProgress();
	}

	void ShowTotal() {
		if (totalEnergy != null)
			totalEnergy.text = StoryText.EnergyLabel(SaveProgress.TotalEnergy(), SaveProgress.TotalTarget());
	}

	public void OpenLevels() {
		if (!SaveProgress.HasAnyProgress()) {
			RefreshContinueButton();
			return;
		}
		for (int i = 0; i < levelButtons.Length && i < LevelCatalog.All.Count; i++) {
			LevelConfig level = LevelCatalog.All[i];
			bool open = SaveProgress.Unlocked(level);
			levelButtons[i].interactable = open;
			levelLocks[i].gameObject.SetActive(!open);
			levelLabels[i].text = open
				? "Màn " + level.order + ": " + level.displayName + "  (" + SaveProgress.Best(level) + "/" + level.energyTarget + ")"
				: "Màn " + level.order + ": Khoá";
		}
		levelSelect.SetActive(true);
	}

	public void CloseLevels() {
		levelSelect.SetActive(false);
	}

	public void PlayLevel(int index) {
		if (index >= 0 && index < LevelCatalog.All.Count)
			SceneRouter.ContinueAt(LevelCatalog.All[index]);
	}

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
		else if (levelSelect != null && levelSelect.activeSelf)
			CloseLevels();
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
