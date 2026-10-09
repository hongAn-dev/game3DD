using UnityEngine;

/// <summary>The robot's feedback sounds: hit (or down on the lethal one), heal and shield block.</summary>
[RequireComponent(typeof(Health))]
public class RobotSounds : MonoBehaviour {

	Health health;

	void Awake() {
		health = GetComponent<Health>();
		health.Damaged += amount => Sfx.Play(health.healthPoints <= 0f ? SfxEvent.RobotDown : SfxEvent.RobotHit);
		health.Healed += amount => Sfx.Play(SfxEvent.Heal);
		health.Blocked += () => Sfx.Play(SfxEvent.ShieldBlock);
	}
}
