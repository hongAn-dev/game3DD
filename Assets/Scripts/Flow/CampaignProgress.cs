using System.Collections.Generic;

/// <summary>
/// Session-only campaign state (spec §3): difficulty and completed levels. Not saved to disk.
/// </summary>
public static class CampaignProgress {

	static readonly HashSet<string> completed = new HashSet<string>();
	static readonly HashSet<string> introsSeen = new HashSet<string>();

	/// <summary>Set by New Game: the first level opens with the tutorial (consumed by GameManager).</summary>
	public static bool TutorialPending { get; set; }

	public static GameSettings.gameDifficulties Difficulty { get; private set; }
	public static bool IsActiveRun { get; private set; }

	public static void BeginRun(GameSettings.gameDifficulties difficulty) {
		completed.Clear();
		introsSeen.Clear();
		TutorialPending = false;
		Difficulty = difficulty;
		GameSettings.difficulty = difficulty;
		IsActiveRun = true;
	}

	/// <summary>True only the first time a level is completed in this run.</summary>
	public static bool CompleteLevel(string levelId) {
		return completed.Add(levelId);
	}

	public static bool IsCompleted(string levelId) {
		return completed.Contains(levelId);
	}

	/// <summary>Intro cards already read in this run (Retry does not show them again).</summary>
	public static bool IntroSeen(string levelId) {
		return introsSeen.Contains(levelId);
	}

	public static void MarkIntroSeen(string levelId) {
		introsSeen.Add(levelId);
	}

	public static int CompletedCount { get { return completed.Count; } }

	public static int FuelPercent { get { return System.Math.Min(100, completed.Count * 25); } }

	public static bool CanPlayEnding {
		get {
			if (!IsActiveRun || LevelCatalog.All.Count == 0)
				return false;
			foreach (LevelConfig level in LevelCatalog.All)
				if (!completed.Contains(level.levelId))
					return false;
			return true;
		}
	}

	public static void Reset() {
		completed.Clear();
		introsSeen.Clear();
		TutorialPending = false;
		IsActiveRun = false;
		Difficulty = GameSettings.gameDifficulties.Easy;
	}
}
