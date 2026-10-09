using UnityEngine;

/// <summary>
/// One enemy's numbers for a level and difficulty (spec §4.1, §4.3), from LevelConfig's Normal values:
/// speed ×0.9 / ×1 / ×1.1 and damage ×0.8 / ×1 / ×1.2 (rounded half away from zero) for Easy / Normal / Hard;
/// creep windup +0.10 s on Easy. Speed is absolute (m/s), never a share of the robot's speed.
/// </summary>
public struct EnemyProfile {

	// Boss slam timings (spec §4.3); the L4 dash comes with the boss attack controller.
	public const float BossWindup = 0.9f, BossStrike = 0.15f, BossRecover = 1.5f;
	public const float CreepStrike = 0.10f, EasyExtraWindup = 0.10f;

	static readonly float[] SpeedMultiplier = { 0.9f, 1f, 1.1f };
	static readonly float[] DamageMultiplier = { 0.8f, 1f, 1.2f };

	public float speed;
	public int damage;
	public float windup, strike, recover;
	public bool slam;   // guardian slam (all bosses)

	public static EnemyProfile For(LevelConfig config, GameSettings.gameDifficulties difficulty, bool boss) {
		int d = (int)difficulty;
		var p = new EnemyProfile();
		p.speed = (boss ? config.bossSpeed : config.creepSpeed) * SpeedMultiplier[d];
		p.damage = (int)System.Math.Round((boss ? config.bossDamage : config.creepDamage) * DamageMultiplier[d], System.MidpointRounding.AwayFromZero);
		if (boss) {
			p.windup = BossWindup;
			p.strike = BossStrike;
			p.recover = BossRecover;
			p.slam = true;
		} else {
			p.windup = config.creepWindup + (difficulty == GameSettings.gameDifficulties.Easy ? EasyExtraWindup : 0f);
			p.strike = CreepStrike;
			p.recover = config.creepRecover;
		}
		return p;
	}
}
