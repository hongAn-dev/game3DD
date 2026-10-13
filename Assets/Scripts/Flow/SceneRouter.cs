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

	/// <summary>New Game: resets the campaign and starts the first level with the tutorial (or only its intro card).</summary>
	public static void StartCampaign(GameSettings.gameDifficulties difficulty, bool tutorial) {
		// The saved progress is cleared only here, once a new run really starts (not when "Chơi mới" is tapped).
		SaveProgress.Reset();
		SaveProgress.Difficulty = difficulty;
		CampaignProgress.BeginRun(difficulty);
		CampaignProgress.TutorialPending = tutorial;
		GameSettings.showIntroLevelMessage = true;
		Load(LevelCatalog.First.levelId);
	}

	/// <summary>"Chơi tiếp": an unlocked level with the last difficulty; earlier completed levels count for the
	/// Ending. Opens with the level's intro card, no tutorial.</summary>
	public static void ContinueAt(LevelConfig level) {
		if (level == null || !SaveProgress.Unlocked(level))
			return;
		CampaignProgress.BeginRun(SaveProgress.Difficulty);
		foreach (LevelConfig l in LevelCatalog.All)
			if (l.order < level.order && SaveProgress.Completed(l))
				CampaignProgress.CompleteLevel(l.levelId);
		GameSettings.showIntroLevelMessage = true;
		Load(level.levelId);
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
