using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Top-right quick SFX switch (spec §7.6): flips SoundSettings.SfxEnabled (SFX + UI), swaps the speaker icon at once
/// and shows the next action ("Tắt/Bật hiệu ứng âm thanh") as a hover tooltip. Its own onClick listener runs before
/// the click sound, so muting plays no click.
/// </summary>
public class SoundToggle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

	public Image icon;
	public Sprite soundOn;
	public Sprite soundOff;
	public GameObject tooltip;
	public Text tooltipText;

	public string ActionLabel { get { return SoundSettings.SfxEnabled ? "Tắt hiệu ứng âm thanh" : "Bật hiệu ứng âm thanh"; } }

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
		SoundSettings.SfxEnabled = !SoundSettings.SfxEnabled;
		Show();
	}

	void Show() {
		if (icon != null)
			icon.sprite = SoundSettings.SfxEnabled ? soundOn : soundOff;
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
