using UnityEngine;

/// <summary>
/// Pause (spec §7.6): Escape/Android Back, the top-right pause button or losing focus pauses while playing
/// (GameFlow.Pause); the panel offers exactly Tiếp tục and Thoát về menu (ends the run). Back while paused resumes;
/// outside Playing/Paused (intro, results) it does nothing. The pause button shows only while playing.
/// </summary>
public class PauseController : MonoBehaviour {

	public GameObject overlay;
	public GameObject pauseButton;

	void Start() {
		Sync(GameFlow.State);
		GameFlow.Changed += Sync;
	}

	void OnDestroy() {
		GameFlow.Changed -= Sync;
	}

	void Update() {
		if (Input.GetKeyDown(KeyCode.Escape))
			Toggle();
		// GameFlow.ResetForScene does not raise Changed; keep the overlay and button honest every frame.
		if ((overlay != null && overlay.activeSelf != (GameFlow.State == FlowState.Paused))
			|| (pauseButton != null && pauseButton.activeSelf != GameFlow.IsGameplayActive))
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
		if (pauseButton != null)
			pauseButton.SetActive(state == FlowState.Playing);
	}
}
