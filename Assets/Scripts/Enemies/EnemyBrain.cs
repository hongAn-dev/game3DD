using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Idle, Patrol, Chase, Windup, Strike, Recover, Return, Disabled }

/// <summary>
/// One enemy's behaviour (spec §6.2/§6.3). The NavMeshAgent is the only thing that moves the root (yaw only).
/// Idle/Patrol → Chase (detect radius, lost beyond loseRadius) → Windup (stops, locks its direction, shows the
/// telegraph) → Strike (the only moment that can hit: at most one hit, in range/angle/line of sight) → Recover.
/// Stuck for stuckRepath seconds → local repath, for stuckReturn seconds → Return home. Paused: frozen. Dead/won:
/// Disabled, so a pending strike never lands.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBrain : MonoBehaviour {

	[Header("Speed = factor × robot top speed (Easy, Normal, Hard)")]
	public float[] speedFactor = { 0.8f, 0.95f, 1.05f };
	public float acceleration = 14f;
	public float turnSpeed = 360f;

	[Header("Senses")]
	public float detectRadius = 26f;
	public float loseRadius = 38f;
	public float repathInterval = 0.2f;
	public float stuckRepath = 1.5f;
	public float stuckReturn = 3f;

	[Header("Attack (world metres, robot centre to enemy root, flat)")]
	public float attackRange = 1.6f;
	public float attackAngle = 45f;
	public float windup = 0.35f;
	public float strike = 0.1f;
	public float recover = 0.7f;
	public int damage = 10;
	public string hitCause = "Robo bị quái vật đánh trúng";
	public GameObject telegraph;

	[Header("Legacy animation clips (optional)")]
	public string idleClip = "";
	public string runClip = "";
	public string attackClip = "";

	public Transform target;

	public EnemyState State { get; private set; }

	NavMeshAgent agent;
	Animation anim;
	Health targetHealth;
	Rigidbody targetBody;
	Vector3 home, goal, lockedForward, lastCheckPosition, patrolPoint;
	float stateTime, repathTimer, stuckCheckTimer, stuckTime;
	bool hitDone;
	string currentClip = "";
	static readonly RaycastHit[] SightHits = new RaycastHit[16];

	public static float Speed(float[] factor, GameSettings.gameDifficulties difficulty, float robotTopSpeed) {
		return factor[(int)difficulty] * robotTopSpeed;
	}

	void Start() {
		agent = GetComponent<NavMeshAgent>();
		anim = GetComponentInChildren<Animation>();
		if (target == null) {
			GameObject player = GameObject.FindWithTag("Player");
			if (player != null)
				target = player.transform;
		}
		if (target != null) {
			targetHealth = target.GetComponent<Health>();
			targetBody = target.GetComponent<Rigidbody>();
		}
		Ball ball = target != null ? target.GetComponent<Ball>() : null;
		agent.speed = Speed(speedFactor, GameSettings.difficulty, ball != null ? ball.MaxSpeed : 9f);
		agent.acceleration = acceleration;
		agent.angularSpeed = turnSpeed;
		agent.stoppingDistance = attackRange * 0.5f;
		home = goal = patrolPoint = lastCheckPosition = transform.position;
		repathTimer = Random.Range(0f, repathInterval);   // spread path requests over frames
		if (telegraph != null)
			telegraph.SetActive(false);
		Enter(EnemyState.Idle);
	}

	void Enter(EnemyState next) {
		State = next;
		stateTime = 0f;
		bool attacking = next == EnemyState.Windup || next == EnemyState.Strike || next == EnemyState.Recover;
		if (agent.isOnNavMesh)
			agent.isStopped = attacking || next == EnemyState.Idle || next == EnemyState.Disabled;
		agent.updateRotation = !attacking;
		if (telegraph != null)
			telegraph.SetActive(next == EnemyState.Windup);
		switch (next) {
		case EnemyState.Windup:
			lockedForward = FlatTo(target.position);
			hitDone = false;
			PlayClip(attackClip);
			break;
		case EnemyState.Chase:
		case EnemyState.Patrol:
		case EnemyState.Return:
			stuckTime = 0f;
			lastCheckPosition = transform.position;
			PlayClip(runClip);
			break;
		default:
			if (next != EnemyState.Strike)
				PlayClip(idleClip);
			break;
		}
	}

	void Update() {
		if (State == EnemyState.Disabled)
			return;
		if (!GameFlow.IsGameplayActive) {
			// Paused/intro freeze time; a finished level stops every enemy and drops any pending strike.
			if (GameFlow.State != FlowState.Paused && GameFlow.State != FlowState.Intro)
				Enter(EnemyState.Disabled);
			return;
		}
		if (target == null || !target.gameObject.activeInHierarchy) {
			Enter(EnemyState.Disabled);   // the robot was destroyed (death) or hidden (win)
			return;
		}
		if (!agent.isOnNavMesh) {
			NavMeshHit near;
			if (NavMesh.SamplePosition(transform.position, out near, 2f, NavMesh.AllAreas))
				agent.Warp(near.position);
			else
				Enter(EnemyState.Disabled);
			return;
		}
		stateTime += Time.deltaTime;
		UpdateRunSpeed();
		float distance = FlatDistance(target.position);

		switch (State) {
		case EnemyState.Idle:
			if (distance <= detectRadius)
				Enter(EnemyState.Chase);
			else if (stateTime > 2f)
				Enter(EnemyState.Patrol);
			break;
		case EnemyState.Patrol:
			if (distance <= detectRadius) {
				Enter(EnemyState.Chase);
			} else if (stateTime > 6f || (!agent.pathPending && agent.remainingDistance < 1f)) {
				NavMeshHit hit;
				Vector3 random = home + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(2f, 8f);
				if (NavMesh.SamplePosition(random, out hit, 2f, NavMesh.AllAreas))
					agent.SetDestination(hit.position);
				stateTime = 0f;
			}
			break;
		case EnemyState.Chase:
			if (distance > loseRadius) {
				Enter(EnemyState.Return);
				break;
			}
			Repath(true);
			CheckStuck();
			if (distance <= attackRange * 0.85f && Vector3.Angle(transform.forward, FlatTo(target.position)) <= attackAngle && Clear())
				Enter(EnemyState.Windup);
			break;
		case EnemyState.Windup:
			transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(lockedForward), turnSpeed * Time.deltaTime);
			if (telegraph != null)
				telegraph.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, stateTime / windup);
			if (stateTime >= windup)
				Enter(EnemyState.Strike);
			break;
		case EnemyState.Strike:
			if (!hitDone && distance <= attackRange && Vector3.Angle(lockedForward, FlatTo(target.position)) <= attackAngle
				&& Mathf.Abs(target.position.y - transform.position.y) < 2.5f && Clear())
				Hit();
			if (stateTime >= strike)
				Enter(EnemyState.Recover);
			break;
		case EnemyState.Recover:
			if (stateTime >= recover)
				Enter(distance <= loseRadius ? EnemyState.Chase : EnemyState.Return);
			break;
		case EnemyState.Return:
			Repath(false);
			// Back home, or at least 3 s of retreat, before chasing again (no chase/retreat flapping when stuck).
			if (distance <= detectRadius && stateTime > 3f)
				Enter(EnemyState.Chase);
			else if (!agent.pathPending && agent.remainingDistance < 1f)
				Enter(EnemyState.Idle);
			break;
		}
	}

	// Nearest NavMesh point under the robot; while it is airborne or off the mesh the last valid point is kept.
	Vector3 TargetGround() {
		NavMeshHit hit;
		if (NavMesh.SamplePosition(target.position, out hit, 3f, NavMesh.AllAreas))
			goal = hit.position;
		return goal;
	}

	// The robot's ground point (chase) or home (return), refreshed every repathInterval only.
	void Repath(bool chase) {
		repathTimer -= Time.deltaTime;
		if (repathTimer > 0f)
			return;
		repathTimer = repathInterval;
		agent.SetDestination(chase ? TargetGround() : home);
	}

	void CheckStuck() {
		stuckCheckTimer += Time.deltaTime;
		if (stuckCheckTimer < 0.5f)
			return;
		bool moved = (transform.position - lastCheckPosition).magnitude > 0.4f;
		bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
		stuckTime = moved || arrived ? 0f : stuckTime + stuckCheckTimer;
		stuckCheckTimer = 0f;
		lastCheckPosition = transform.position;
		if (stuckTime >= stuckReturn) {
			Enter(EnemyState.Return);
		} else if (stuckTime >= stuckRepath && agent.isOnNavMesh) {
			NavMeshHit hit;
			Vector3 aside = transform.position + Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f) * FlatTo(goal) * 3f;
			if (NavMesh.SamplePosition(aside, out hit, 2f, NavMesh.AllAreas))
				agent.SetDestination(hit.position);
			repathTimer = 0.6f;
		}
	}

	// Line of sight from the enemy's chest to the robot, ignoring the enemy, the robot and other moving bodies.
	bool Clear() {
		Vector3 from = transform.position + Vector3.up * 0.6f;
		Vector3 to = target.position;
		int count = Physics.RaycastNonAlloc(from, to - from, SightHits, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < count; i++) {
			Collider c = SightHits[i].collider;
			if (c.transform.IsChildOf(transform) || c.attachedRigidbody != null)
				continue;
			return false;
		}
		return true;
	}

	void Hit() {
		hitDone = true;
		if (targetHealth == null || targetHealth.healthPoints <= 0f)
			return;
		if (targetHealth.TakeDamage(damage, DamageKind.EnemyAttack) && targetHealth.healthPoints <= 0f)
			GameFlow.ReportDeathCause(hitCause);   // only the strike that ends the run names the cause
	}

	Vector3 FlatTo(Vector3 point) {
		Vector3 d = point - transform.position;
		d.y = 0f;
		return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
	}

	float FlatDistance(Vector3 point) {
		Vector3 d = point - transform.position;
		d.y = 0f;
		return d.magnitude;
	}

	// Cross-fades only when the clip changes (spec §6.3).
	void PlayClip(string clip) {
		if (anim == null || string.IsNullOrEmpty(clip) || clip == currentClip || anim.GetClip(clip) == null)
			return;
		currentClip = clip;
		anim.CrossFade(clip, 0.15f);
	}

	// While moving states play the run clip, its speed follows the real velocity; standing still shows idle.
	void UpdateRunSpeed() {
		if (anim == null || (State != EnemyState.Chase && State != EnemyState.Patrol && State != EnemyState.Return))
			return;
		float speed = agent.velocity.magnitude;
		PlayClip(speed < 0.3f && !string.IsNullOrEmpty(idleClip) ? idleClip : runClip);
		AnimationState run = string.IsNullOrEmpty(runClip) ? null : anim[runClip];
		if (run != null)
			run.speed = Mathf.Clamp(speed / Mathf.Max(agent.speed, 0.1f), 0.3f, 1.5f);
	}
}
