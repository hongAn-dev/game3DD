using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>What hurt the robot: enemy strikes respect invulnerability (and the shield); hazards always kill.</summary>
public enum DamageKind { EnemyAttack, FatalHazard }

/// <summary>
/// A game object health handler. The single owner of HP (spec §6.1): damage goes through TakeDamage with its kind,
/// heals through Heal (clamped, never revives). After an enemy hit that cost HP the object is invulnerable to enemy
/// attacks for InvulnerableSeconds of gameplay time. Nothing is taken while gameplay is not Playing.
/// </summary>
public class Health : MonoBehaviour {
	
	public enum deathAction {loadLevelWhenDead,doNothingWhenDead};

	public const float InvulnerableSeconds = 0.8f;

	public float maxHealth = 100f;
	public float healthPoints = 1f;
	// Base health points.
	public float respawnHealthPoints = 1f;		

	// Lives and variables for respawning.
	public int numberOfLives = 1;
	public bool isAlive = true;	

	public GameObject explosionPrefab;
	
	public deathAction onLivesGone = deathAction.doNothingWhenDead;
	
	public string LevelToLoad = "";
	
	private Vector3 respawnPosition;
	private Quaternion respawnRotation;

	public float InvulnerableLeft { get; private set; }
	public event System.Action<float> Damaged;
	public event System.Action<float> Healed;
	public event System.Action Blocked;   // an enemy attack hit the shield

	

	/// <summary>
	/// Use this for initialization.
	/// </summary>
	void Start () {
		// store initial position as respawn location
		respawnPosition = transform.position;
		respawnRotation = transform.rotation;

		// default to current scene
		if (LevelToLoad=="")
		{
			LevelToLoad = SceneManager.GetActiveScene().name;
		}
	}
	
	/// <summary>
	/// Update is called once per frame.
	/// </summary>
	void Update () {
		if (GameFlow.IsGameplayActive && InvulnerableLeft > 0f)
			InvulnerableLeft = Mathf.Max(0f, InvulnerableLeft - Time.deltaTime);
		// If the object is 'dead'.
		if (healthPoints <= 0) {
			// Decrement # of lives, update lives GUI.
			numberOfLives--;
			
			if (explosionPrefab!=null) {
				Instantiate (explosionPrefab, transform.position, Quaternion.identity);
			}

			// Respawn.
			if (numberOfLives > 0) { 
				// Reset the player to respawn position.
				transform.position = respawnPosition;
				transform.rotation = respawnRotation;
				// Give the player full health again.
				healthPoints = respawnHealthPoints;
			}
			// ALL lives are gone.
			else { 
				isAlive = false;
				
				switch(onLivesGone)
				{
				case deathAction.loadLevelWhenDead:
					SceneManager.LoadScene(LevelToLoad);
					break;
				case deathAction.doNothingWhenDead:
					// do nothing, death must be handled in another way elsewhere
					break;
				}
				Destroy(gameObject);
			}
		}
	}

	/// <summary>
	/// Damage of the given kind. Returns true when it cost HP. Enemy attacks are ignored during the invulnerability
	/// window (and start it); a fatal hazard empties HP regardless. Nothing happens outside Playing or once dead.
	/// </summary>
	public bool TakeDamage(float amount, DamageKind kind) {
		if (!GameFlow.IsGameplayActive || healthPoints <= 0f)
			return false;
		if (kind == DamageKind.FatalHazard) {
			amount = healthPoints;
		} else {
			if (InvulnerableLeft > 0f || amount <= 0f)
				return false;
			ShieldEffect shield = GetComponent<ShieldEffect>();
			if (shield != null && shield.Active) {
				if (Blocked != null)
					Blocked();
				return false;   // the attack is spent on the shield
			}
			InvulnerableLeft = InvulnerableSeconds;
		}
		float before = healthPoints;
		healthPoints = Mathf.Clamp(healthPoints - amount, 0f, maxHealth);
		if (Damaged != null)
			Damaged(before - healthPoints);   // what was actually lost
		return true;
	}

	/// <summary>Heals up to maxHealth. Returns false (nothing consumed) when full, dead or not Playing.</summary>
	public bool Heal(float amount) {
		if (!GameFlow.IsGameplayActive || healthPoints <= 0f || healthPoints >= maxHealth || amount <= 0f)
			return false;
		float before = healthPoints;
		healthPoints = Mathf.Min(maxHealth, healthPoints + amount);
		if (Healed != null)
			Healed(healthPoints - before);   // what was actually restored
		return true;
	}

	/// <summary>Legacy raw damage (old Damage component, non-player objects). The robot uses TakeDamage.</summary>
	public void ApplyDamage(float amount) {	
		healthPoints = healthPoints - amount;	
	}

	/// <summary>Legacy raw heal; the robot uses Heal (clamped).</summary>
	public void ApplyHeal(float amount) {
		healthPoints = Mathf.Min(maxHealth, healthPoints + amount);
	}

	/// <summary>
	/// Add an extra life to the game object that this script instance is attached.
	/// </summary>
	public void ApplyBonusLife(int amount) {
		numberOfLives = numberOfLives + amount;
	}

	/// <summary>
	/// Update respawn.
	/// </summary>
	public void updateRespawn(Vector3 newRespawnPosition, Quaternion newRespawnRotation) {
		respawnPosition = newRespawnPosition;
		respawnRotation = newRespawnRotation;
	}
}
