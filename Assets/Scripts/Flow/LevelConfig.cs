using UnityEngine;

/// <summary>
/// Data for one playable level. Assets live in Resources/Levels and are written by LevelConfigBuilder.
/// Difficulty-indexed arrays use GameSettings.gameDifficulties order: Easy, Normal, Hard.
/// </summary>
[CreateAssetMenu(menuName = "Robo Lac Loi/Level Config")]
public class LevelConfig : ScriptableObject {
	public string levelId;
	public int order;
	public string displayName;
	public string nextScene;

	[Header("Energy (one core = +1)")]
	public int energyTarget;
	public int energyAtStart;
	[Tooltip("Cores on the map beyond the target (fixed supply: target + spare, all placed at the start).")]
	public int energySpare = 2;
	/// <summary>Cores placed at the start of the level.</summary>
	public int EnergySupply { get { return energyTarget + energySpare; } }
	public int energyCap;
	public float energyIntervalMin;
	public float energyIntervalMax;
	[Tooltip("Cores per ground-spawn batch, both bounds included.")]
	public int energyBatchMin = 1;
	public int energyBatchMax = 1;
	public int energyPointMinimum = 20;
	[Range(0f, 1f)] public float energyRiskFraction = 0.15f;

	[Header("Enemies per difficulty: Easy, Normal, Hard (enemyCap = creeps + bosses, alive + telegraphed)")]
	public int[] enemyCap = new int[3];
	public int[] bossCap = new int[3];
	public int[] maxChasers = new[] { 1, 2, 2 };
	public int enemyInitialBatch = 1;
	public int enemyBatchMax = 1;
	public float bossSpacing = 8f;

	[Header("Enemy Normal values (EnemyProfile scales them per difficulty)")]
	public float creepSpeed;
	public float bossSpeed;
	public int creepDamage;
	public int bossDamage;
	public float creepWindup;
	public float creepWindupMax;
	public float bossWindup = 0.65f;
	public float bossRecover = 1.10f;
	public float creepRecover;
	[Tooltip("Gameplay seconds after the intro before the first enemy.")]
	public float quietTime = 5f;

	[Header("Support items (spec §6.3): budget alive + reserved, interval, weights shield/heal10/heal20/overdrive")]
	public int supportCap;
	public float supportIntervalMin;
	public float supportIntervalMax;
	public float[] supportWeights = new float[4];
	[Tooltip("Seconds into play for a first shield (Level1); < 0 = none.")]
	public float firstShieldAfter = -1f;

	[Header("Harmful ground pickups: alive + reserved")]
	public int harmfulCap;
	public int harmfulAtStart;
	public int harmfulDamage;
	public float harmfulIntervalMin = 8f;
	public float harmfulIntervalMax = 12f;
	public float harmfulLifetime = 20f;

	[Header("Hazard")]
	public string hazardDeathMessage;

	public bool IsFinal { get { return nextScene == SceneRouter.EndingScene; } }
}
