using System.Collections.Generic;

/// <summary>
/// Session-only campaign state (spec §3): difficulty and completed levels. Not saved to disk.
/// </summary>
public static class CampaignProgress {

	static readonly HashSet<string> completed = new HashSet<string>();

	public static GameSettings.gameDifficulties Difficulty { get; private set; }
	public static bool IsActiveRun { get; private set; }

	public static void BeginRun(GameSettings.gameDifficulties difficulty) {
		completed.Clear();
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
		IsActiveRun = false;
		Difficulty = GameSettings.gameDifficulties.Easy;
	}
}
