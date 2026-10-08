using UnityEditor;
using UnityEngine;

/// <summary>Writes Resources/Levels/Level1..4 with the spec §4.2/§6.2 starting values. Re-running overwrites them.</summary>
public static class LevelConfigBuilder {

	const string Folder = "Assets/Resources/Levels";

	[MenuItem("Tools/Robo Lac Loi/Write Level Configs")]
	public static void WriteDefaults() {
		if (!AssetDatabase.IsValidFolder("Assets/Resources"))
			AssetDatabase.CreateFolder("Assets", "Resources");
		if (!AssetDatabase.IsValidFolder(Folder))
			AssetDatabase.CreateFolder("Assets/Resources", "Levels");

		// Names stay the current zone names until Plan 4 re-themes the maps.
		Write("Level1", 1, "Bãi phế liệu", "Level2", 6, 3, 5, 5f, 7f, new[] { 2, 3, 4 }, new[] { 1, 1, 1 }, "Robo rơi xuống biển axit");
		Write("Level2", 2, "Khu công nghiệp bỏ hoang", "Level3", 10, 4, 6, 5f, 7f, new[] { 4, 5, 6 }, new[] { 1, 1, 1 }, "Robo rơi xuống biển axit");
		Write("Level3", 3, "Vùng hoang hóa", "Level4", 14, 5, 7, 4f, 6f, new[] { 6, 7, 8 }, new[] { 2, 2, 2 }, "Robo chạm dung nham");
		Write("Level4", 4, "Trạm căn cứ", SceneRouter.EndingScene, 18, 6, 8, 4f, 6f, new[] { 8, 9, 10 }, new[] { 2, 2, 2 }, "Robo rơi xuống biển axit");
		AssetDatabase.SaveAssets();
		Debug.Log("LevelConfigBuilder: done");
	}

	static void Write(string id, int order, string name, string next, int target, int atStart, int cap,
		float intervalMin, float intervalMax, int[] enemyCap, int[] bossCap, string hazardMessage) {
		string path = Folder + "/" + id + ".asset";
		LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
		if (config == null) {
			config = ScriptableObject.CreateInstance<LevelConfig>();
			AssetDatabase.CreateAsset(config, path);
		}
		config.levelId = id;
		config.order = order;
		config.displayName = name;
		config.nextScene = next;
		config.energyTarget = target;
		config.energyAtStart = atStart;
		config.energyCap = cap;
		config.energyIntervalMin = intervalMin;
		config.energyIntervalMax = intervalMax;
		config.enemyCap = enemyCap;
		config.bossCap = bossCap;
		config.hazardDeathMessage = hazardMessage;
		EditorUtility.SetDirty(config);
	}
}
