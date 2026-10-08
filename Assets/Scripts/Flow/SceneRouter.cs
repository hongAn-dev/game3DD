using UnityEngine.SceneManagement;

/// <summary>Scene routing by name, never by build order (spec §8).</summary>
public static class SceneRouter {

	public const string MainMenuScene = "MainMenu";
	public const string EndingScene = "Ending";

	public static void Load(string scene) {
		// One load per transition: a second click/tap in the same frame is ignored (ResetForScene clears it).
		if (GameFlow.State == FlowState.Transition)
			return;
		GameFlow.Enter(FlowState.Transition);
		UnityEngine.Time.timeScale = 1f;
		SceneManager.LoadScene(scene);
	}

	/// <summary>New Game: resets the campaign and starts the first level with its intro card.</summary>
	public static void StartCampaign(GameSettings.gameDifficulties difficulty) {
		CampaignProgress.BeginRun(difficulty);
		GameSettings.showIntroLevelMessage = true;
		Load(LevelCatalog.First.levelId);
	}

	public static void Next() {
		LevelConfig current = LevelCatalog.Get(SceneManager.GetActiveScene().name);
		if (current == null) {
			ToMenu();
			return;
		}
		if (current.IsFinal) {
			ToEnding();
			return;
		}
		GameSettings.showIntroLevelMessage = true;
		Load(current.nextScene);
	}

	/// <summary>Retry keeps completed levels; the reloaded level starts from score 0.</summary>
	public static void Retry() {
		Load(SceneManager.GetActiveScene().name);
	}

	/// <summary>Back to the menu ends the run (spec §3: leaving starts a new run).</summary>
	public static void ToMenu() {
		CampaignProgress.Reset();
		Load(MainMenuScene);
	}

	/// <summary>Plays the ending only after a full campaign; a debug Level4 win goes back to the menu.</summary>
	public static void ToEnding() {
		if (!CampaignProgress.CanPlayEnding) {
			UnityEngine.Debug.LogWarning("SceneRouter: campaign not complete (debug start?), returning to menu");
			ToMenu();
			return;
		}
		Load(EndingScene);
	}
}
