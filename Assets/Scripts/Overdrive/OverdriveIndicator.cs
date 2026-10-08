using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD countdown for Overdrive: the bolt icon and the seconds left, hidden when it is not active.</summary>
public class OverdriveIndicator : MonoBehaviour {

	public GameObject content;
	public Text seconds;

	void Awake() {
		Show(0f);
	}

	public void Show(float remaining) {
		bool on = remaining > 0f;
		if (content != null && content.activeSelf != on)
			content.SetActive(on);
		if (on && seconds != null)
			seconds.text = Mathf.CeilToInt(remaining).ToString();
	}
}
