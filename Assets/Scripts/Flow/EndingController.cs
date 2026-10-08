using UnityEngine;

/// <summary>
/// Ending scene: shows the completion screen once. Plan 6 plays the cinematic first and calls Complete()
/// from both the timeline end and the skip button.
/// </summary>
public class EndingController : MonoBehaviour {

	public GameObject completionPanel;

	bool completed;

	void Start() {
		Complete();
	}

	public void Complete() {
		if (completed)
			return;
		completed = true;
		completionPanel.SetActive(true);
	}

	public void PlayAgain() {
		SceneRouter.StartCampaign(CampaignProgress.IsActiveRun ? CampaignProgress.Difficulty : GameSettings.difficulty);
	}

	public void BackToMenu() {
		SceneRouter.ToMenu();
	}
}
