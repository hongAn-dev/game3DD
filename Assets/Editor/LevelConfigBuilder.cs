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

		// Energy starts with a random target + 2..5 cores spread across authored regions; the
		// interval/batch values only matter for the respawn that keeps a level winnable.
		// Zone names follow spec §7.2 (scene IDs unchanged); hazards follow the first spec §5.
		Write("Level1", 1, "Đảo hoang", "Level2", 6, 8, 11, 1.5f, 2f, "Robo rơi xuống biển axit");
		Write("Level2", 2, "Trạm khai thác bỏ hoang", "Level3", 10, 12, 15, 1f, 1.5f, "Robo rơi xuống biển axit");
		Write("Level3", 3, "Thung lũng dung nham", "Level4", 14, 16, 19, 0.8f, 1.2f, "Robo chạm dung nham");
		Write("Level4", 4, "Bãi phóng bỏ hoang", SceneRouter.EndingScene, 18, 20, 23, 0.8f, 1f, "Robo rơi xuống biển axit");
		// Energy batches (spec §5.1): cores per drop, upper bound included.
		Batch("Level1", 2, 3);
		Batch("Level2", 3, 4);
		Batch("Level3", 4, 6);
		Batch("Level4", 5, 7);
		// Support items (spec §6.3).
		Support("Level1", 1, 10f, 14f, new[] { 40f, 40f, 20f, 0f }, 5f);
		Support("Level2", 2, 8f, 12f, new[] { 30f, 35f, 20f, 15f }, -1f);
		Support("Level3", 3, 7f, 10f, new[] { 30f, 35f, 20f, 15f }, -1f);
		Support("Level4", 3, 6f, 9f, new[] { 30f, 35f, 20f, 15f }, -1f);
		// Enemies (spec §4.1-§4.3): caps (creep + boss), Normal speed/damage, creep timings, quiet time.
		Enemies("Level1", new[] { 2, 2, 3 }, new[] { 0, 0, 0 }, 6.8f, 0f, 15, 0, 0.45f, 0.80f, 8f);
		Enemies("Level2", new[] { 4, 5, 6 }, new[] { 0, 0, 0 }, 7.5f, 0f, 20, 0, 0.35f, 0.70f, 6f);
		Enemies("Level3", new[] { 7, 10, 12 }, new[] { 1, 2, 2 }, 8.0f, 7.8f, 25, 35, 0.30f, 0.65f, 5f);
		Enemies("Level4", new[] { 11, 15, 17 }, new[] { 2, 3, 3 }, 8.4f, 8.2f, 30, 40, 0.25f, 0.60f, 5f);
		Challenge("Level1", new[] { 1, 2, 2 }, 1, 1, 20, 0.15f, 0, 0, 0, 8f, 12f);
		Challenge("Level2", new[] { 2, 3, 3 }, 2, 1, 40, 0.30f, 2, 1, 15, 8f, 12f);
		Challenge("Level3", new[] { 3, 4, 5 }, 3, 2, 64, 0.45f, 4, 2, 20, 6f, 9f);
		Challenge("Level4", new[] { 4, 5, 6 }, 4, 3, 88, 0.55f, 6, 3, 25, 5f, 8f);
		AssetDatabase.SaveAssets();
		Debug.Log("LevelConfigBuilder: done");
	}

	static void Challenge(string id, int[] chasers, int initialBatch, int batchMax, int points, float risk,
		int trapCap, int trapStart, int trapDamage, float trapMin, float trapMax) {
		LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(Folder + "/" + id + ".asset");
		config.maxChasers = chasers;
		config.enemyInitialBatch = initialBatch;
		config.enemyBatchMax = batchMax;
		config.bossSpacing = 8f;
		config.energyPointMinimum = points;
		config.energyRiskFraction = risk;
		config.creepWindupMax = config.creepWindup + 0.10f;
		config.bossWindup = config.order == 4 ? 0.55f : 0.65f;
		config.bossRecover = config.order == 4 ? 0.95f : 1.10f;
		config.harmfulCap = trapCap;
		config.harmfulAtStart = trapStart;
		config.harmfulDamage = trapDamage;
		config.harmfulIntervalMin = trapMin;
		config.harmfulIntervalMax = trapMax;
		config.harmfulLifetime = 20f;
		EditorUtility.SetDirty(config);
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

	static void Enemies(string id, int[] cap, int[] boss, float creepSpeed, float bossSpeed, int creepDamage, int bossDamage,
		float creepWindup, float creepRecover, float quietTime) {
		LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(Folder + "/" + id + ".asset");
		config.enemyCap = cap;
		config.bossCap = boss;
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
		config.timeLimitSeconds = order == 1 ? 30f : order == 2 ? 105f : order == 3 ? 210f : 300f;
		config.energyTarget = target;
		config.energyAtStart = atStart;
		config.energySpare = atStart - target;
		config.energySpareMax = 5;
		config.energyCap = cap;
		config.energyIntervalMin = intervalMin;
		config.energyIntervalMax = intervalMax;
		config.hazardDeathMessage = hazardMessage;
		EditorUtility.SetDirty(config);
	}
}
