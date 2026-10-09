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
	public int energyCap;
	public float energyIntervalMin;
	public float energyIntervalMax;

	[Header("Enemies per difficulty: Easy, Normal, Hard (enemyCap = creeps + bosses, alive + telegraphed)")]
	public int[] enemyCap = new int[3];
	public int[] bossCap = new int[3];

	[Header("Enemy Normal values (EnemyProfile scales them per difficulty)")]
	public float creepSpeed;
	public float bossSpeed;
	public int creepDamage;
	public int bossDamage;
	public float creepWindup;
	public float creepRecover;
	[Tooltip("Gameplay seconds after the intro before the first enemy.")]
	public float quietTime = 5f;

	[Header("Hazard")]
	public string hazardDeathMessage;

	public bool IsFinal { get { return nextScene == SceneRouter.EndingScene; } }
}
