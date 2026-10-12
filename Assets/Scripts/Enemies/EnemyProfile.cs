using UnityEngine;

/// <summary>
/// One enemy's numbers for a level and difficulty (spec §4.1, §4.3), from LevelConfig's Normal values:
/// speed ×0.92 / ×1 / ×1.04 and damage ×0.8 / ×1 / ×1.2 (rounded half away from zero) for Easy / Normal / Hard;
/// creep windup range +0.10 s on Easy. Speed is absolute (m/s), never a share of the robot's speed.
/// </summary>
public struct EnemyProfile {

	// Boss strike timing is fixed; slam windup/recover comes from the level.
	public const float BossWindup = 0.65f, BossStrike = 0.15f, BossRecover = 1.1f;
	public const float CreepStrike = 0.10f, EasyExtraWindup = 0.10f;

	static readonly float[] SpeedMultiplier = { 0.92f, 1f, 1.04f };
	static readonly float[] DamageMultiplier = { 0.8f, 1f, 1.2f };

	public float speed;
	public int damage;
	public float windup, windupMax, strike, recover;
	public bool slam;   // guardian slam (all bosses)
	public bool dash;   // Level4 guardian also dashes
	public bool finalBoss; // Level4 contact is lethal, even before the attack windup

	public static EnemyProfile For(LevelConfig config, GameSettings.gameDifficulties difficulty, bool boss) {
		int d = (int)difficulty;
		var p = new EnemyProfile();
		p.speed = (boss ? config.bossSpeed : config.creepSpeed) * SpeedMultiplier[d];
		p.damage = (int)System.Math.Round((boss ? config.bossDamage : config.creepDamage) * DamageMultiplier[d], System.MidpointRounding.AwayFromZero);
		if (boss) {
			p.windup = config.bossWindup > 0f ? config.bossWindup : BossWindup;
			p.windupMax = p.windup;
			p.strike = BossStrike;
			p.recover = config.bossRecover > 0f ? config.bossRecover : BossRecover;
			p.slam = true;
			p.dash = config.order >= 4;
			p.finalBoss = config.IsFinal || config.order >= 4;
		} else {
			float easy = difficulty == GameSettings.gameDifficulties.Easy ? EasyExtraWindup : 0f;
			p.windup = config.creepWindup + easy;
			p.windupMax = Mathf.Max(p.windup, config.creepWindupMax + easy);
			p.strike = CreepStrike;
			p.recover = config.creepRecover;
		}
		return p;
	}
}
