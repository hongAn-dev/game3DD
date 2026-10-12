using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Quick independent background-music switch. Does not seek or restart the music source.</summary>
public class MusicToggle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
	public Image icon;
	public Sprite musicOn;
	public Sprite musicOff;
	public GameObject tooltip;
	public Text tooltipText;

	public string ActionLabel { get { return SoundSettings.MusicEnabled ? "Tắt nhạc nền" : "Bật nhạc nền"; } }

	void OnEnable() {
		Show();
		SoundSettings.Changed += Show;
		if (tooltip != null)
			tooltip.SetActive(false);
	}

	void OnDisable() {
		SoundSettings.Changed -= Show;
	}

	public void Toggle() {
		SoundSettings.MusicEnabled = !SoundSettings.MusicEnabled;
		PlayerPrefs.Save();
		Show();
	}

	void Show() {
		if (icon != null)
			icon.sprite = SoundSettings.MusicEnabled ? musicOn : musicOff;
		if (tooltipText != null)
			tooltipText.text = ActionLabel;
	}

	public void OnPointerEnter(PointerEventData eventData) {
		if (tooltip != null)
			tooltip.SetActive(true);
	}

	public void OnPointerExit(PointerEventData eventData) {
		if (tooltip != null)
			tooltip.SetActive(false);
	}
}