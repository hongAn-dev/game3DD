using UnityEngine;

/// <summary>Soft UI tick for a button (wired to its onClick by UiTheme); silent when SFX are muted.</summary>
public class ClickSound : MonoBehaviour {
	public void Play() {
		Sfx.Play(SfxEvent.UiClick);
	}
}
