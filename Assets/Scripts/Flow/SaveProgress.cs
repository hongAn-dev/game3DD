using UnityEngine;

/// <summary>
/// Campaign progress kept on the device (PlayerPrefs): the best energy collected in each level (capped at its target)
/// and the last difficulty. A level is completed when its best reaches the target; Level N is unlocked when it is the
/// first level or Level N−1 is completed. "Chơi mới" clears it only once a new run actually starts.
/// </summary>
public static class SaveProgress {

	const string BestKey = "progress_best_", DifficultyKey = "progress_difficulty";

	public static int Best(LevelConfig level) {
		return Mathf.Clamp(PlayerPrefs.GetInt(BestKey + level.levelId, 0), 0, level.energyTarget);
	}

	/// <summary>Keeps the higher of the saved and this run's energy (capped at the target).</summary>
	public static void RecordBest(LevelConfig level, int collected) {
		int best = Mathf.Clamp(collected, 0, level.energyTarget);
		if (best <= Best(level))
			return;
		PlayerPrefs.SetInt(BestKey + level.levelId, best);
		PlayerPrefs.Save();
	}

	public static bool Completed(LevelConfig level) {
		return Best(level) >= level.energyTarget;
	}

	public static bool Unlocked(LevelConfig level) {
		LevelConfig previous = null;
		foreach (LevelConfig l in LevelCatalog.All)
			if (l.order < level.order && (previous == null || l.order > previous.order))
				previous = l;
		return previous == null || Completed(previous);
	}

	/// <summary>Energy over the whole campaign: Σ best, out of Σ targets (6 + 10 + 14 + 18 = 48).</summary>
	public static int TotalEnergy() {
		int total = 0;
		foreach (LevelConfig l in LevelCatalog.All)
			total += Best(l);
		return total;
	}

	public static int TotalTarget() {
		int total = 0;
		foreach (LevelConfig l in LevelCatalog.All)
			total += l.energyTarget;
		return total;
	}

	public static GameSettings.gameDifficulties Difficulty {
		get { return (GameSettings.gameDifficulties)PlayerPrefs.GetInt(DifficultyKey, (int)GameSettings.gameDifficulties.Normal); }
		set { PlayerPrefs.SetInt(DifficultyKey, (int)value); PlayerPrefs.Save(); }
	}

	/// <summary>New Game: every level back to locked except the first.</summary>
	public static void Reset() {
		foreach (LevelConfig l in LevelCatalog.All)
			PlayerPrefs.DeleteKey(BestKey + l.levelId);
		PlayerPrefs.Save();
	}
}
