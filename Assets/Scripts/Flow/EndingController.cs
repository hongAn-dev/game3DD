using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Ending scene (spec §8): plays the cinematic timeline, shows the skip button after skipDelay seconds (click/touch or
/// Escape), and shows the completion screen once, from either the timeline end or the skip. Losing focus pauses the
/// timeline and audio; coming back resumes at the same time.
/// </summary>
public class EndingController : MonoBehaviour {

	public PlayableDirector director;
	public GameObject skipButton;
	public GameObject completionPanel;
	public float skipDelay = 1f;

	public bool Completed { get; private set; }
	public int CompletionCount { get; private set; }

	float elapsed;
	bool suspended;

	void Start() {
		Time.timeScale = 1f;
		AudioListener.pause = false;
		completionPanel.SetActive(false);
		if (skipButton != null)
			skipButton.SetActive(false);
		if (director != null) {
			director.extrapolationMode = DirectorWrapMode.Hold;
			director.Play();
		} else {
			Complete();
		}
	}

	void Update() {
		if (Completed || suspended)
			return;
		elapsed += Time.unscaledDeltaTime;
		if (skipButton != null && !skipButton.activeSelf && elapsed >= skipDelay)
			skipButton.SetActive(true);
		if (elapsed >= skipDelay && Input.GetKeyDown(KeyCode.Escape))
			Skip();
		else if (director != null && director.time >= director.duration - 0.02)
			Complete();
	}

	public void Skip() {
		Complete();
	}

	/// <summary>Shows the completion screen; the cinematic jumps to its last frame (space shot) and stops.</summary>
	public void Complete() {
		if (Completed)
			return;
		Completed = true;
		CompletionCount++;
		if (director != null) {
			director.time = director.duration;
			director.Evaluate();
			director.Pause();
		}
		if (skipButton != null)
			skipButton.SetActive(false);
		completionPanel.SetActive(true);
	}

	void OnApplicationFocus(bool focus) {
		Suspend(!focus);
	}

	void OnApplicationPause(bool paused) {
		Suspend(paused);
	}

	void Suspend(bool value) {
		if (suspended == value)
			return;
		suspended = value;
		AudioListener.pause = value;
		if (director == null || Completed)
			return;
		if (value)
			director.Pause();
		else
			director.Resume();
	}

	public void PlayAgain() {
		AudioListener.pause = false;
		SceneRouter.StartCampaign(CampaignProgress.IsActiveRun ? CampaignProgress.Difficulty : GameSettings.difficulty);
	}

	public void BackToMenu() {
		AudioListener.pause = false;
		SceneRouter.ToMenu();
	}
}
