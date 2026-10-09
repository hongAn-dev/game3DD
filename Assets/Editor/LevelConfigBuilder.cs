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

		// Zone names and hazards follow spec §5.
		Write("Level1", 1, "Bãi đáp hỏng", "Level2", 6, 3, 4, 2.5f, 3.5f, "Robo rơi xuống biển axit");
		Write("Level2", 2, "Trạm khai thác bỏ hoang", "Level3", 10, 5, 6, 2f, 3f, "Robo rơi xuống biển axit");
		Write("Level3", 3, "Vùng địa nhiệt", "Level4", 14, 7, 8, 1.5f, 2.5f, "Robo chạm dung nham");
		Write("Level4", 4, "Bãi phóng cũ", SceneRouter.EndingScene, 18, 8, 10, 1.5f, 2f, "Robo rơi xuống biển axit");
		// Energy batches (spec §5.1): cores per drop, upper bound included.
		Batch("Level1", 1, 1);
		Batch("Level2", 1, 2);
		Batch("Level3", 2, 3);
		Batch("Level4", 2, 4);
		// Support items (spec §6.3).
		Support("Level1", 1, 10f, 14f, new[] { 40f, 40f, 20f, 0f }, 5f);
		Support("Level2", 2, 8f, 12f, new[] { 30f, 35f, 20f, 15f }, -1f);
		Support("Level3", 3, 7f, 10f, new[] { 30f, 35f, 20f, 15f }, -1f);
		Support("Level4", 3, 6f, 9f, new[] { 30f, 35f, 20f, 15f }, -1f);
		// Enemies (spec §4.1-§4.3): caps (creep + boss), Normal speed/damage, creep timings, quiet time.
		Enemies("Level1", new[] { 1, 1, 1 }, 0, 5.0f, 0f, 10, 0, 0.65f, 1.00f, 8f);
		Enemies("Level2", new[] { 2, 3, 4 }, 0, 6.0f, 0f, 12, 0, 0.60f, 0.95f, 6f);
		Enemies("Level3", new[] { 4, 5, 6 }, 1, 6.8f, 6.5f, 15, 25, 0.55f, 0.90f, 5f);
		Enemies("Level4", new[] { 6, 7, 8 }, 1, 7.5f, 7.2f, 18, 30, 0.50f, 0.85f, 5f);
		AssetDatabase.SaveAssets();
		Debug.Log("LevelConfigBuilder: done");
	}

	static void Support(string id, int cap, float min, float max, float[] weights, float firstShield) {
		LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(Folder + "/" + id + ".asset");
		config.supportCap = cap;
		config.supportIntervalMin = min;
		config.supportIntervalMax = max;
		config.supportWeights = weights;
		config.firstShieldAfter = firstShield;
		EditorUtility.SetDirty(config);
	}

	static void Batch(string id, int min, int max) {
		LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(Folder + "/" + id + ".asset");
		config.energyBatchMin = min;
		config.energyBatchMax = max;
		EditorUtility.SetDirty(config);
	}

	static void Enemies(string id, int[] cap, int boss, float creepSpeed, float bossSpeed, int creepDamage, int bossDamage,
		float creepWindup, float creepRecover, float quietTime) {
		LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(Folder + "/" + id + ".asset");
		config.enemyCap = cap;
		config.bossCap = new[] { boss, boss, boss };
		config.creepSpeed = creepSpeed;
		config.bossSpeed = bossSpeed;
		config.creepDamage = creepDamage;
		config.bossDamage = bossDamage;
		config.creepWindup = creepWindup;
		config.creepRecover = creepRecover;
		config.quietTime = quietTime;
		EditorUtility.SetDirty(config);
	}

	static void Write(string id, int order, string name, string next, int target, int atStart, int cap,
		float intervalMin, float intervalMax, string hazardMessage) {
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
		config.hazardDeathMessage = hazardMessage;
		EditorUtility.SetDirty(config);
	}
}
