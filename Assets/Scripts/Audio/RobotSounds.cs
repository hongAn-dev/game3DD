using UnityEngine;

/// <summary>The robot's feedback sounds: hit, heal and shield block. The lethal hit is silent: GameManager's Lose is the one death cue.</summary>
[RequireComponent(typeof(Health))]
public class RobotSounds : MonoBehaviour {

	Health health;

	void Awake() {
		health = GetComponent<Health>();
		health.Damaged += amount => { if (health.healthPoints > 0f) Sfx.Play(SfxEvent.RobotHit); };
		health.Healed += amount => Sfx.Play(SfxEvent.Heal);
		health.Blocked += () => Sfx.Play(SfxEvent.ShieldBlock);
	}
}
