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

	[Header("Movement (absolute; EnemyDirector applies the level profile)")]
	public float speed = 6f;
	public float acceleration = 14f;
	public float turnSpeed = 300f;

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

	[Header("Guardian slam (spec §4.3): a drawn circle in front of the boss")]
	public bool canSlam;
	public float slamRadius = 2.4f;
	public float slamReach = 1.2f;

	[Header("L4 guardian dash (spec §4.3): straight, locked, telegraphed strip; alternates with the slam")]
	public bool canDash;
	public float dashLength = 5f;
	public float dashWidth = 1.5f;
	public float dashSpeed = 10.5f;
	public float dashWindup = 1.0f;
	public float dashRecover = 1.6f;
	public GameObject dashTelegraph;

	[Header("Legacy animation clips (optional)")]
	public string idleClip = "";
	public string runClip = "";
	public string attackClip = "";

	public Transform target;
	[Tooltip("Patrol stops (energy landing points near home), set by EnemyDirector; empty = wander near home.")]
	public Vector3[] patrolPoints = new Vector3[0];

	public EnemyState State { get; private set; }
	public float DistanceToTarget { get; private set; }
	public AttackKind CurrentAttack { get; private set; }

	NavMeshAgent agent;
	Animation anim;
	Health targetHealth;
	Rigidbody targetBody;
	Vector3 home, goal, lockedForward, lastCheckPosition, patrolPoint;
	float stateTime, repathTimer, stuckCheckTimer, stuckTime;
	bool hitDone;
	Vector3 slamCentre;
	float dashTravel, dashDone;
	bool nextIsDash;
	string currentClip = "";
	static readonly RaycastHit[] SightHits = new RaycastHit[16];

	/// <summary>Level/difficulty numbers (EnemyProfile). Call right after spawning; also works after Start.</summary>
	public void Configure(EnemyProfile profile) {
		speed = profile.speed;
		damage = profile.damage;
		windup = profile.windup;
		strike = profile.strike;
		recover = profile.recover;
		canSlam = profile.slam;
		canDash = profile.dash;
		if (agent != null)
			agent.speed = speed;
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
		agent.speed = speed;
		agent.acceleration = acceleration;
		agent.angularSpeed = turnSpeed;
		agent.stoppingDistance = attackRange * 0.5f;
		home = goal = patrolPoint = lastCheckPosition = transform.position;
		repathTimer = Random.Range(0f, repathInterval);   // spread path requests over frames
		if (telegraph != null)
			telegraph.SetActive(false);
		Enter(EnemyState.Idle);
	}

	// Chase token from the director (spec §4.2: at most 2 or 3 enemies chase at once); no director = no limit.
	bool MayChase() {
		return EnemyDirector.Current == null || EnemyDirector.Current.RequestChase(this);
	}

	void ReleaseToken() {
		if (EnemyDirector.Current != null)
			EnemyDirector.Current.ReleaseChase(this);
	}

	void OnDestroy() {
		ReleaseToken();
	}

	void Enter(EnemyState next) {
		State = next;
		if (next == EnemyState.Idle || next == EnemyState.Patrol || next == EnemyState.Return || next == EnemyState.Disabled)
			ReleaseToken();
		stateTime = 0f;
		bool attacking = next == EnemyState.Windup || next == EnemyState.Strike || next == EnemyState.Recover;
		if (agent.isOnNavMesh)
			agent.isStopped = attacking || next == EnemyState.Idle || next == EnemyState.Disabled;
		agent.updateRotation = !attacking;
		if (telegraph != null)
			telegraph.SetActive(next == EnemyState.Windup && CurrentAttack != AttackKind.Dash);
		if (dashTelegraph != null)
			dashTelegraph.SetActive(next == EnemyState.Windup && CurrentAttack == AttackKind.Dash);
		switch (next) {
		case EnemyState.Windup:
			lockedForward = FlatTo(target.position);
			hitDone = false;
			if (CurrentAttack == AttackKind.Slam) {
				// The circle is drawn at its real size where it will land, from the start of the windup.
				slamCentre = BossAttacks.SlamCentre(transform.position, lockedForward, slamReach);
				if (telegraph != null) {
					telegraph.transform.position = slamCentre + Vector3.up * 0.04f;
					telegraph.transform.localScale = Vector3.one;
				}
			}
			if (CurrentAttack == AttackKind.Dash) {
				dashTravel = DashTravel(lockedForward);
				dashDone = 0f;
				if (dashTelegraph != null) {
					// The strip shows exactly where the dash will go: its width and its (obstacle-limited) length.
					dashTelegraph.transform.position = transform.position + lockedForward * dashTravel * 0.5f + Vector3.up * 0.05f;
					dashTelegraph.transform.rotation = Quaternion.LookRotation(lockedForward);
					dashTelegraph.transform.localScale = new Vector3(dashWidth, 1f, dashTravel);
				}
			}
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
		DistanceToTarget = distance;

		switch (State) {
		case EnemyState.Idle:
			if (distance <= detectRadius && MayChase())
				Enter(EnemyState.Chase);
			else if (stateTime > 2f)
				Enter(EnemyState.Patrol);
			break;
		case EnemyState.Patrol:
			if (distance <= detectRadius && MayChase()) {
				Enter(EnemyState.Chase);
			} else if (stateTime > 6f || (!agent.pathPending && agent.remainingDistance < 1f)) {
				NavMeshHit hit;
				Vector3 random = patrolPoints.Length > 0 ? patrolPoints[Random.Range(0, patrolPoints.Length)]
					: home + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(2f, 8f);
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
			if (EnemyDirector.Current != null && !EnemyDirector.Current.HoldsChase(this)) {
				Enter(EnemyState.Patrol);   // a closer enemy took this token
				break;
			}
			Repath(true);
			CheckStuck();
			// Standing within the stopping distance the agent does not turn; face the robot ourselves (yaw only),
			// or an enemy that ends up beside or past the robot (after a dash) never lines up an attack.
			if (agent.velocity.sqrMagnitude < 0.25f)
				transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(FlatTo(target.position)), turnSpeed * Time.deltaTime);
			if (canSlam) {
				float angle = Vector3.Angle(transform.forward, FlatTo(target.position));
				bool slamReady = distance <= slamReach + slamRadius * 0.8f && angle <= 60f && Clear();
				bool dashReady = canDash && distance >= 2.5f && distance <= dashLength + 1f && angle <= 30f && Clear()
					&& DashTravel(FlatTo(target.position)) >= 2f;
				// L4 alternates: the other attack is preferred, but whichever is possible beats waiting.
				if (dashReady && (nextIsDash || !slamReady)) {
					CurrentAttack = AttackKind.Dash;
					Enter(EnemyState.Windup);
				} else if (slamReady) {
					CurrentAttack = AttackKind.Slam;
					Enter(EnemyState.Windup);
				}
			} else if (distance <= attackRange * 0.85f && Vector3.Angle(transform.forward, FlatTo(target.position)) <= attackAngle && Clear()) {
				CurrentAttack = AttackKind.Strike;
				Enter(EnemyState.Windup);
			}
			break;
		case EnemyState.Windup:
			transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(lockedForward), turnSpeed * Time.deltaTime);
			if (telegraph != null && CurrentAttack == AttackKind.Strike)
				telegraph.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, stateTime / windup);
			else if (telegraph != null)
				telegraph.transform.position = slamCentre + Vector3.up * 0.04f;   // stays on the ground while the boss turns
			if (stateTime >= (CurrentAttack == AttackKind.Dash ? dashWindup : windup))
				Enter(EnemyState.Strike);
			break;
		case EnemyState.Strike:
			if (CurrentAttack == AttackKind.Dash) {
				// Straight along the locked line; only the agent moves the root and it cannot leave the NavMesh.
				float step = Mathf.Min(dashSpeed * Time.deltaTime, dashTravel - dashDone);
				agent.Move(lockedForward * step);
				dashDone += step;
				if (!hitDone && BossAttacks.TouchesDash(transform.position, target.position, agent.radius + 0.7f))
					Hit();
				if (dashDone >= dashTravel - 0.001f) {
					nextIsDash = false;
					Enter(EnemyState.Recover);
				}
				break;
			}
			if (CurrentAttack == AttackKind.Slam) {
				if (!hitDone && BossAttacks.InSlam(slamCentre, slamRadius, target.position)
					&& BossAttacks.Clear(slamCentre + Vector3.up * 0.5f, target.position, SightHits))
					Hit();
			} else if (!hitDone && distance <= attackRange && Vector3.Angle(lockedForward, FlatTo(target.position)) <= attackAngle
				&& Mathf.Abs(target.position.y - transform.position.y) < 2.5f && Clear())
				Hit();
			if (stateTime >= strike) {
				if (CurrentAttack == AttackKind.Slam)
					nextIsDash = canDash;
				Enter(EnemyState.Recover);
			}
			break;
		case EnemyState.Recover:
			if (stateTime >= (CurrentAttack == AttackKind.Dash ? dashRecover : recover))
				Enter(distance <= loseRadius ? EnemyState.Chase : EnemyState.Return);
			break;
		case EnemyState.Return:
			Repath(false);
			// Back home, or at least 3 s of retreat, before chasing again (no chase/retreat flapping when stuck).
			if (distance <= detectRadius && stateTime > 3f && MayChase())
				Enter(EnemyState.Chase);
			else if (!agent.pathPending && agent.remainingDistance < 1f)
				Enter(EnemyState.Idle);
			break;
		}
	}

	// How far a dash along dir can go on continuous NavMesh (stops short of obstacles/edges), at most dashLength.
	float DashTravel(Vector3 dir) {
		NavMeshHit hit;
		Vector3 start = transform.position;
		if (NavMesh.Raycast(start, start + dir * dashLength, out hit, NavMesh.AllAreas))
			return Mathf.Max(0f, hit.distance - 0.3f);
		return dashLength;
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
