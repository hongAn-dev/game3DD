using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pause menu: ESC, the mobile pause button or losing focus pauses while playing (GameFlow.Pause); the overlay offers
/// Resume, Main menu (ends the run) and the camera sensitivity slider.
/// </summary>
public class PauseController : MonoBehaviour {

	public GameObject overlay;
	public Slider sensitivitySlider;

	void Start() {
		if (sensitivitySlider != null)
			sensitivitySlider.value = ThirdPersonOrbitCamera.Sensitivity;
		Sync(GameFlow.State);
		GameFlow.Changed += Sync;
	}

	void OnDestroy() {
		GameFlow.Changed -= Sync;
	}

	void Update() {
		if (Input.GetKeyDown(KeyCode.Escape))
			Toggle();
		// GameFlow.ResetForScene does not raise Changed; keep the overlay honest every frame.
		if (overlay != null && overlay.activeSelf != (GameFlow.State == FlowState.Paused))
			Sync(GameFlow.State);
	}

	public void Toggle() {
		if (GameFlow.State == FlowState.Paused)
			Resume();
		else
			GameFlow.Pause();
	}

	public void Resume() {
		GameFlow.Resume();
	}

	public void ToMenu() {
		SceneRouter.ToMenu();
	}

	public void SetSensitivity(float value) {
		ThirdPersonOrbitCamera.Sensitivity = value;
	}

	void OnApplicationFocus(bool focused) {
		if (!focused)
			GameFlow.Pause();
	}

	void OnApplicationPause(bool paused) {
		if (paused)
			GameFlow.Pause();
	}

	void Sync(FlowState state) {
		if (overlay != null)
			overlay.SetActive(state == FlowState.Paused);
	}
}
