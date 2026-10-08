using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Ordered access to the LevelConfig assets in Resources/Levels.</summary>
public static class LevelCatalog {

	static List<LevelConfig> levels;

	public static IReadOnlyList<LevelConfig> All {
		get {
			if (levels == null)
				levels = Resources.LoadAll<LevelConfig>("Levels").OrderBy(l => l.order).ToList();
			return levels;
		}
	}

	public static LevelConfig First { get { return All.Count > 0 ? All[0] : null; } }

	public static LevelConfig Get(string sceneName) {
		return All.FirstOrDefault(l => l.levelId == sceneName);
	}
}
