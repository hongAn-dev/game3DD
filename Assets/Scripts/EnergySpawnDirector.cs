using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the energy supply and selects safe/risk clusters from editor-authored ground candidates. A randomized
/// target + 2..5 spare cores are placed at the start and nothing spawns on a timer; cores come back only
/// when collected + on the map drops below the target (an anti-energy trap took one, a core was lost), so the
/// level stays winnable. A far core nobody picks up for staleDelay seconds still moves closer.
/// </summary>
public class EnergySpawnDirector : MonoBehaviour {
    public const int MaxAlive = 24;
    public const float FallbackDelay = 3f;
    public const float FallbackRetry = 1f;
    public static EnergySpawnDirector Current { get; private set; }

    public GameObject corePrefab;
    public Transform[] landingPoints = new Transform[0];
    public float intervalOverride = -1f;
    public float staleDelay = 12f;
    public float minPlayerDistance = 3f;
    public float minSpacing = 1.5f;
    public Vector2 nearPath = new Vector2(5f, 18f);

    LevelConfig config;
    readonly List<Treasure> alive = new List<Treasure>();
    readonly HashSet<int> pending = new HashSet<int>();
    readonly List<GameObject> inFlight = new List<GameObject>();
    float timer, nextInterval, emptyTimer, staleTimer;
    int lastScore = -1;
    int startingSupply;
    Transform player;
    Vector3 robotPoint;
    bool robotPointValid;
    SpawnRegistry registry;
    static readonly RaycastHit[] GroundHits = new RaycastHit[16];
    internal Vector3 FirstCorePoint { get; private set; }
    internal int StartingNearSafeCandidateCount { get; private set; }
    internal float[] StartingNearSafePaths { get; private set; } = new float[0];
    public int AliveCount { get { return registry != null ? registry.Count(SpawnRegistry.SpawnCategory.Energy) : alive.Count + pending.Count; } }
    /// <summary>Number of cores selected for this run (target + 2..5); useful for HUD/debug and QA.</summary>
    public int StartingSupply { get { return startingSupply; } }
    /// <summary>Runtime cap includes the largest configured random supply.</summary>
    public int Capacity { get { return config == null ? 0 : EnergyCapacity(); } }
    public int ReservedCount { get { alive.RemoveAll(t => t == null); return registry != null ? Mathf.Max(0, registry.Count(SpawnRegistry.SpawnCategory.Energy) - alive.Count) : pending.Count; } }

    internal void ForceBatch() { timer = float.MaxValue; }
    public static int AllowedAlive(int cap, int target, int score) {
        return score >= target ? 0 : Mathf.Max(0, Mathf.Min(cap, MaxAlive));
    }
    public static int SpawnCount(int batch, int allowed, int aliveCount, int reservedCount) {
        return Mathf.Max(0, Mathf.Min(batch, allowed - aliveCount - reservedCount));
    }
    public static int RandomBatch(int min, int max) { return Random.Range(min, max + 1); }
    /// <summary>Returns a valid starting supply with 2–5 spare cores (inclusive by default).</summary>
    public static int RandomSupply(int target, int minSpare, int maxSpare) {
        int lo = Mathf.Max(0, minSpare), hi = Mathf.Max(lo, maxSpare);
        return Mathf.Max(0, target) + Random.Range(lo, hi + 1);
    }
    public static float PathLength(NavMeshPath path) {
        float length = 0f; Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }

    void Awake() {
        if (Current != null && Current != this) { enabled = false; return; }
        Current = this;
        registry = SpawnRegistry.Current;
        if (registry == null) registry = GetComponent<SpawnRegistry>();
        if (registry == null) registry = gameObject.AddComponent<SpawnRegistry>();
    }
    void Start() {
        config = LevelCatalog.Get(SceneManager.GetActiveScene().name);
        GameObject p = GameObject.FindWithTag("Player");
        player = p != null ? p.transform : null;
        if (config == null) { enabled = false; return; }
        GameFlow.Changed += OnFlowChanged;
        StartingCores();
        ScheduleNext();
    }
    void OnDestroy() {
        GameFlow.Changed -= OnFlowChanged;
        if (Current == this) Current = null;
        if (registry != null) registry.ReleaseOwner(this);
    }
    void OnDisable() { CancelInFlight(); }
    void OnFlowChanged(FlowState state) {
        if (state == FlowState.Dead || state == FlowState.LevelComplete || state == FlowState.Transition) CancelInFlight();
    }
    void CancelInFlight() {
        StopAllCoroutines();
        foreach (int id in pending) registry.Release(id);
        pending.Clear();
        foreach (GameObject go in inFlight) if (go != null) Destroy(go);
        inFlight.Clear();
    }

    void StartingCores() {
        foreach (Treasure old in FindObjectsOfType<Treasure>()) Destroy(old.gameObject);
        alive.Clear();
        startingSupply = RandomSupply(config.energyTarget, config.energySpare, config.energySpareMax);
        int want = Mathf.Min(startingSupply, AllowedAlive(EnergyCapacity(), config.energyTarget, Score()));
        Dictionary<SpawnPoint, float> paths;
        var candidates = GetCandidates(true, true, out paths);
        StartingNearSafePaths = candidates.Where(c => !c.IsRisk && CachedPath(c, paths) >= nearPath.x && CachedPath(c, paths) <= nearPath.y)
            .Select(c => CachedPath(c, paths)).ToArray();
        StartingNearSafeCandidateCount = StartingNearSafePaths.Length;
        List<SpawnPoint> selected = SpreadSelect(candidates, want, paths, true);
        for (int i = 0; i < selected.Count; i++) {
            if (i == 0) FirstCorePoint = selected[i].transform.position;
            StartSpawn(selected[i].transform.position);
        }
    }
    int Score() { return GameManager.gm != null ? GameManager.gm.score : 0; }
    int EnergyCapacity() {
        return Mathf.Min(MaxAlive, Mathf.Max(config.energyCap, config.EnergySupplyMax));
    }
    bool StartSpawn(Vector3 position) {
        int id;
        if (!registry.TryReserve(SpawnRegistry.SpawnCategory.Energy, this, position, GroundSpawn.FootprintRadius(corePrefab), EnergyCapacity(), out id)) return false;
        pending.Add(id); StartCoroutine(SpawnAt(position, id)); return true;
    }

    void ScheduleNext() {
        nextInterval = intervalOverride > 0f ? intervalOverride : Random.Range(config.energyIntervalMin, config.energyIntervalMax);
        timer = 0f;
    }

    void UpdateRobotPoint() {
        if (player == null) return;
        float ground = float.MinValue;
        int count = Physics.RaycastNonAlloc(player.position + Vector3.up, Vector3.down, GroundHits, 60f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (GroundHits[i].collider.attachedRigidbody == null) ground = Mathf.Max(ground, GroundHits[i].point.y);
        NavMeshHit nav;
        if (ground > float.MinValue && player.position.y - ground < 2f
            && NavMesh.SamplePosition(new Vector3(player.position.x, ground, player.position.z), out nav, 3f, NavMesh.AllAreas)) {
            robotPoint = nav.position; robotPointValid = true;
        } else if (!robotPointValid && ground > float.MinValue
            && NavMesh.SamplePosition(new Vector3(player.position.x, ground, player.position.z), out nav, 3f, NavMesh.AllAreas)) {
            robotPoint = nav.position; robotPointValid = true;
        }
    }

    public float PathFromRobot(Vector3 point) {
        UpdateRobotPoint();
        NavMeshHit nav; var path = new NavMeshPath();
        if (!robotPointValid || !NavMesh.SamplePosition(point, out nav, 1.5f, NavMesh.AllAreas)
            || !NavMesh.CalculatePath(robotPoint, nav.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            return float.MaxValue;
        return PathLength(path);
    }

    List<SpawnPoint> GetCandidates(bool includeRisk, bool includeDynamicRisk) {
        Dictionary<SpawnPoint, float> ignored;
        return GetCandidates(includeRisk, includeDynamicRisk, out ignored);
    }

    List<SpawnPoint> GetCandidates(bool includeRisk, bool includeDynamicRisk, out Dictionary<SpawnPoint, float> pathCache) {
        UpdateRobotPoint();
        var result = new List<SpawnPoint>();
        pathCache = new Dictionary<SpawnPoint, float>();
        if (!robotPointValid || player == null) return result;
        EnemyBrain[] enemies = includeDynamicRisk ? FindObjectsOfType<EnemyBrain>() : new EnemyBrain[0];
        HarmfulPickup[] traps = includeDynamicRisk ? FindObjectsOfType<HarmfulPickup>() : new HarmfulPickup[0];
        Treasure[] cores = includeDynamicRisk ? FindObjectsOfType<Treasure>() : new Treasure[0];
        HazardVolume[] hazards = FindObjectsOfType<HazardVolume>();
        float footprint = GroundSpawn.FootprintRadius(corePrefab);
        foreach (Transform t in landingPoints) {
            if (t == null || Vector3.Distance(t.position, player.position) < Mathf.Max(minPlayerDistance, PlayerRadius() + footprint + 0.25f)) continue;
            SpawnPoint metadata = t.GetComponent<SpawnPoint>();
            if (metadata == null) metadata = t.gameObject.AddComponent<SpawnPoint>();
            if (includeDynamicRisk && OverlapsEnemyStrike(t.position, footprint, enemies)) continue;
            metadata.dynamicRiskFlags = includeDynamicRisk ? DynamicRiskAt(t.position, enemies, traps, cores) : SpawnRisk.None;
            if (!includeRisk && metadata.IsRisk) continue;
            if (registry.IsOccupied(t.position, footprint)) continue;
            RaycastHit ground;
            if (!Physics.Raycast(t.position + Vector3.up * 2f, Vector3.down, out ground, 5f, ~0, QueryTriggerInteraction.Ignore)
                || ground.normal.y < 0.7f || WalkableGround.InHazard(ground.point - Vector3.up * 0.1f, hazards)) continue;
            if (!metadata.IsRisk && metadata.edgeClearance > 0f && metadata.edgeClearance < SafeEdgeClearance()) continue;
            if (metadata.IsRisk && !ValidRiskFootprint(t.position, metadata, hazards)) continue;
            if (registry.IsOccupied(t.position, footprint)) continue;
            NavMeshHit nav; var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(ground.point, out nav, 1.5f, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(robotPoint, nav.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            result.Add(metadata);
            pathCache[metadata] = PathLength(path);
        }
        return result;
    }

    float SafeEdgeClearance() { return 2.5f; }
    float PlayerRadius() {
        Collider c=player!=null?player.GetComponent<Collider>():null;
        return c!=null?Mathf.Max(c.bounds.extents.x,c.bounds.extents.z):.5f;
    }
    internal bool ValidRiskFootprint(Vector3 center, SpawnPoint point, HazardVolume[] hazards) {
        float playerRadius = PlayerRadius();
        float footprint = GroundSpawn.FootprintRadius(corePrefab);
        float minimum = playerRadius + footprint + 0.25f;
        bool edgeRisk = (point.EffectiveRisk & SpawnRisk.Edge) != 0;
        if (edgeRisk && (point.edgeClearance < Mathf.Max(1.2f, minimum) || point.edgeClearance > 1.8f)) return false;

        RaycastHit ground;
        if (!WalkableGround.Ground(center, out ground, hazards)) return false;
        if (edgeRisk) {
            Vector3 escape = point.escapeDirection;
            if (escape.sqrMagnitude < 0.01f || !HasGroundedClearCorridor(center, escape, 1.5f, playerRadius, hazards)) return false;
        }

        // Risk should leave two non-opposing lanes around the pickup, so it cannot become
        // the only route through a narrow passage. The edge escape lane counts as one.
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        var clear = new List<Vector3>();
        foreach (Vector3 direction in directions) {
            if (HasGroundedClearCorridor(center, direction, 1.5f, playerRadius, hazards))
                clear.Add(direction);
        }
        if (edgeRisk) {
            Vector3 escape = point.escapeDirection.normalized;
            if (!clear.Any(d => Vector3.Dot(d, escape) > 0.9f)) clear.Add(escape);
        }
        for (int i = 0; i < clear.Count; i++) {
            for (int j = i + 1; j < clear.Count; j++) {
                float alignment = Vector3.Dot(clear[i], clear[j]);
                if (alignment > -0.5f && alignment < 0.5f) return true;
            }
        }
        return false;
    }

    internal bool HasGroundedClearCorridor(Vector3 center, Vector3 direction, float length, float radius, HazardVolume[] hazards) {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return false;
        direction.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        float halfWidth = Mathf.Max(0.2f, radius);
        float step = 0.25f;
        int samples = Mathf.CeilToInt(length / step);
        Vector3 previousBottom = Vector3.zero, previousTop = Vector3.zero;
        bool havePrevious = false;
        for (int i = 0; i <= samples; i++) {
            float distance = length * i / samples;
            Vector3 sample = center + direction * distance;
            RaycastHit ground;
            if (!WalkableGround.Ground(sample, out ground, hazards)) return false;
            for (int sideIndex = -1; sideIndex <= 1; sideIndex++) {
                Vector3 across = ground.point + side * (halfWidth * sideIndex);
                RaycastHit acrossGround;
                if (!WalkableGround.Ground(across, out acrossGround, hazards)
                    || Mathf.Abs(acrossGround.point.y - ground.point.y) > 0.45f) return false;
            }

            // Sweep an upright player-sized capsule just above the floor to reject walls,
            // rocks and other solid obstructions along the full route.
            float capsuleRadius = Mathf.Max(0.2f, radius);
            float playerHeight = Mathf.Max(1.4f, player != null && player.GetComponent<Collider>() != null
                ? player.GetComponent<Collider>().bounds.size.y : 1.8f);
            Vector3 bottom = ground.point + Vector3.up * (capsuleRadius + 0.04f);
            Vector3 top = ground.point + Vector3.up * (playerHeight - capsuleRadius);
            if (top.y <= bottom.y) top = bottom + Vector3.up * 0.5f;
            if (Physics.CheckCapsule(bottom, top, capsuleRadius, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (havePrevious) {
                Vector3 delta = bottom - previousBottom;
                float sweptDistance = delta.magnitude;
                if (sweptDistance > 0.001f && Physics.CapsuleCast(previousBottom, previousTop, capsuleRadius,
                    delta / sweptDistance, sweptDistance, ~0, QueryTriggerInteraction.Ignore)) return false;
            }
            previousBottom = bottom; previousTop = top; havePrevious = true;
        }
        return true;
    }
    SpawnRisk DynamicRiskAt(Vector3 p, EnemyBrain[] enemies, HarmfulPickup[] traps, Treasure[] cores = null) {
        SpawnRisk risk = SpawnRisk.None;
        foreach (EnemyBrain e in enemies) {
            if (e == null) continue;
            float d = Vector3.Distance(p, e.transform.position);
            bool boss = e.canSlam || e.canDash;
            float riskNear = boss ? 4f : 3f;
            if (d >= riskNear && d <= (boss ? 7f : 6f)) risk |= SpawnRisk.Enemy;
        }
        foreach (HarmfulPickup trap in traps) {
            if (trap == null) continue;
            float d = Vector3.Distance(p, trap.transform.position);
            if (d >= 1.5f && d <= 2.5f) { risk |= SpawnRisk.Trap; break; }
        }
        // Keep harmful pickups close to some energy drops. This also marks an energy
        // point as a deliberate risk candidate without creating an unreachable trap.
        if (cores != null) foreach (Treasure core in cores) {
            if (core == null) continue;
            float d = Vector3.Distance(p, core.transform.position);
            if (d >= 1.8f && d <= 4.5f) { risk |= SpawnRisk.Trap; break; }
        }
        return risk;
    }
    static bool OverlapsEnemyStrike(Vector3 p, float footprint, EnemyBrain[] enemies) {
        foreach (EnemyBrain e in enemies) {
            if (e == null) continue;
            float d = Vector3.Distance(p, e.transform.position);
            bool boss = e.canSlam || e.canDash;
            float strikeRadius = boss ? Mathf.Max(e.slamReach + e.slamRadius, e.dashWidth * 0.5f) : e.attackRange;
            if (d <= strikeRadius + footprint) return true;
        }
        return false;
    }

    public bool TryGetCandidate(bool allowRisk, bool near, float minPath, float maxPath, out SpawnPoint point, out float pathLength, bool visible = false) {
        Dictionary<SpawnPoint, float> paths;
        var candidates = GetCandidates(allowRisk, true, out paths);
        if (visible) candidates.RemoveAll(c => !IsVisible(c.transform.position));
        candidates.RemoveAll(p => { float d; return !paths.TryGetValue(p, out d) || d < minPath || d > maxPath; });
        return TryPick(candidates, near, false, out point, out pathLength, paths);
    }
    bool IsVisible(Vector3 point) {
        Camera cam=Camera.main; if(cam==null) return false;
        Vector3 v=cam.WorldToViewportPoint(point+Vector3.up*.5f); if(v.z<=0f||v.x<.08f||v.x>.92f||v.y<.08f||v.y>.92f)return false;
        Vector3 from=cam.transform.position,to=point+Vector3.up*.5f; RaycastHit hit;
        return !Physics.Raycast(from,to-from,out hit,Vector3.Distance(from,to)-.3f,~0,QueryTriggerInteraction.Ignore)||hit.collider.attachedRigidbody!=null;
    }
    /// <summary>
    /// A free walkable point 5–9 m straight ahead of the camera, on screen and reachable (first shield fallback
    /// when the whole energy supply takes the visible landing points).
    /// </summary>
    public bool TryPointAhead(float radius, out Vector3 point) {
        point = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null || player == null) return false;
        Vector3 ahead = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        HazardVolume[] hazards = FindObjectsOfType<HazardVolume>();
        // A fan of directions inside the view (the straight-ahead spot can be off the NavMesh, e.g. a pad or slope).
        foreach (float d in new[] { 6f, 5f, 7f, 4f, 8f, 9f })
            foreach (float angle in new[] { 0f, -20f, 20f, -35f, 35f }) {
                RaycastHit ground;
                Vector3 probe = player.position + Quaternion.Euler(0f, angle, 0f) * ahead * d;
                if (!WalkableGround.Ground(probe, out ground, hazards) || registry.IsOccupied(ground.point, radius)
                    || !IsVisible(ground.point) || PathFromRobot(ground.point) == float.MaxValue) continue;
                point = ground.point;
                return true;
            }
        return false;
    }
    public bool TryGetRiskCandidate(bool near, float minPath, float maxPath, out SpawnPoint point, out float pathLength) {
        Dictionary<SpawnPoint, float> paths;
        var candidates = GetCandidates(true, true, out paths);
        candidates.RemoveAll(c => !c.IsRisk);
        candidates.RemoveAll(c => { float d; return !paths.TryGetValue(c, out d) || d<minPath || d>maxPath; });
        return TryPick(candidates, near, false, out point, out pathLength, paths);
    }
    public bool PickSupportPoint(bool near, bool visible, out Vector3 point) {
        SpawnPoint candidate; float path;
        if (TryGetCandidate(false, near, nearPath.x, nearPath.y, out candidate, out path, visible)) { point = candidate.transform.position; return true; }
        point = Vector3.zero; return false;
    }

    bool TryPick(List<SpawnPoint> candidates, bool near, bool spread, out SpawnPoint point, out float pathLength, Dictionary<SpawnPoint, float> pathCache = null) {
        point = null; pathLength = float.MaxValue;
        if (candidates == null || candidates.Count == 0) return false;
        var measured = new List<KeyValuePair<SpawnPoint, float>>();
        foreach (SpawnPoint c in candidates) {
            float distance;
            if (pathCache == null) distance = PathFromRobot(c.transform.position);
            else if (!pathCache.TryGetValue(c, out distance)) continue;
            measured.Add(new KeyValuePair<SpawnPoint, float>(c, distance));
        }
        measured.RemoveAll(x => x.Value == float.MaxValue);
        if (measured.Count == 0) return false;
        if (near) measured.Sort((a,b) => Mathf.Abs(a.Value - Mid(nearPath)).CompareTo(Mathf.Abs(b.Value - Mid(nearPath))));
        else if (spread) measured.Sort((a,b) => b.Value.CompareTo(a.Value));
        else measured = measured.OrderBy(_ => Random.value).ToList();
        int bound = near || spread ? 1 : Mathf.Min(4, measured.Count);
        var chosen = measured[Random.Range(0, bound)];
        point = chosen.Key; pathLength = chosen.Value; return true;
    }
    static float Mid(Vector2 v) { return (v.x + v.y) * .5f; }

    void Update() {
        if (!GameFlow.IsGameplayActive || config == null) return;
        UpdateRobotPoint();
        alive.RemoveAll(t => t == null);
        int count = AliveCount, score = Score();
        int allowed = AllowedAlive(EnergyCapacity(), config.energyTarget, score);
        if (score != lastScore) { lastScore = score; staleTimer = 0f; }
        // Respawn only to keep the level winnable: collected + on the map must reach the target (+1 spare).
        int missing = Missing(config.energyTarget, score, count);
        if (allowed > 0 && missing > 0) {
            emptyTimer += Time.deltaTime;
            if (emptyTimer >= FallbackDelay) {
                emptyTimer = 0f;
                Dictionary<SpawnPoint, float> pathCache;
                var candidates = GetCandidates(true, true, out pathCache);
                List<SpawnPoint> selected = SpreadSelect(candidates, missing, pathCache, true);   // the first one near the robot
                if (selected.Count == 0 && !TrySpawnNear()) emptyTimer = FallbackDelay - FallbackRetry;
                foreach (SpawnPoint point in selected) StartSpawn(point.transform.position);
                return;
            }
        } else emptyTimer = 0f;
        ReplaceStaleCore();
    }

    /// <summary>Cores to bring back so collected + on the map is the target plus one spare; 0 while enough remain.</summary>
    public static int Missing(int target, int score, int onMap) {
        return score + onMap >= target ? 0 : target + 1 - score - onMap;
    }

    static float CachedPath(SpawnPoint p, Dictionary<SpawnPoint, float> paths) { float d; return paths.TryGetValue(p, out d) ? d : float.MaxValue; }

    [Tooltip("Cores keep at least this far apart; small maps step it down a metre at a time (never below minSpread).")]
    public float spreadSpacing = 8f;
    public const float MinSpread = 3f;

    /// <summary>The spacing the last selection achieved (8 m on large maps, less on a small island).</summary>
    public float LastSpacing { get; private set; }

    // Risk for energy placement: map edge / enemies / real traps, but not "beside another core" (the harmful
    // director's tag), which made new cores land next to old ones.
    static bool EnergyRisk(SpawnPoint c) {
        return (c.riskFlags | (c.dynamicRiskFlags & ~SpawnRisk.Trap)) != SpawnRisk.None;
    }

    /// <summary>
    /// Spreads cores over the whole map (playtest): every core at least spreadSpacing (8 m) from the others and
    /// from cores already on the map; when the map cannot fit that many, the spacing steps down a metre at a time.
    /// Farthest-point picks (random among the 3 farthest) keep the coverage even; a level's energyRiskFraction of
    /// the picks come from risky ground. The first core of a level stays 5–18 m from the robot.
    /// </summary>
    List<SpawnPoint> SpreadSelect(List<SpawnPoint> candidates, int count, Dictionary<SpawnPoint, float> paths, bool nearFirst) {
        List<SpawnPoint> best = new List<SpawnPoint>();
        if (count <= 0 || candidates.Count == 0) return best;
        for (float spacing = spreadSpacing; spacing >= MinSpread - 0.01f; spacing -= 1f) {
            List<SpawnPoint> picks = SpreadAt(candidates, count, paths, nearFirst, spacing);
            if (picks.Count > best.Count) { best = picks; LastSpacing = spacing; }
            if (picks.Count >= count) break;
        }
        return best;
    }

    List<SpawnPoint> SpreadAt(List<SpawnPoint> candidates, int count, Dictionary<SpawnPoint, float> paths, bool nearFirst, float spacing) {
        var selected = new List<SpawnPoint>();
        var pool = new List<SpawnPoint>(candidates);
        var anchors = alive.Where(t => t != null).Select(t => t.transform.position).ToList();
        System.Func<SpawnPoint, float> gap = c => anchors.Count == 0 ? float.MaxValue : anchors.Min(a => Vector3.Distance(a, c.transform.position));
        if (nearFirst) {
            var near = pool.Where(c => !EnergyRisk(c) && CachedPath(c, paths) >= nearPath.x && CachedPath(c, paths) <= nearPath.y && gap(c) >= spacing).ToList();
            if (near.Count > 0) {
                SpawnPoint first = near[Random.Range(0, near.Count)];
                selected.Add(first); pool.Remove(first); anchors.Add(first.transform.position);
            }
        }
        int risky = Mathf.Clamp(Mathf.RoundToInt(count * Mathf.Clamp01(config.energyRiskFraction)), 0, count);
        var slots = new List<bool>();
        for (int i = selected.Count; i < count; i++) slots.Add(i - selected.Count < risky);
        slots = slots.OrderBy(_ => Random.value).ToList();
        foreach (bool risk in slots) {
            var open = pool.Where(c => gap(c) >= spacing).ToList();
            if (open.Count == 0) break;
            List<SpawnPoint> kind = open.Where(c => EnergyRisk(c) == risk).ToList();
            if (kind.Count == 0) kind = open;
            var choices = kind.OrderByDescending(gap).Take(3).ToList();
            SpawnPoint pick = choices[Random.Range(0, choices.Count)];
            selected.Add(pick); pool.Remove(pick); anchors.Add(pick.transform.position);
        }
        return selected;
    }

    bool TrySpawnNear() {
        SpawnPoint point; float path;
        if (!TryGetCandidate(false, true, nearPath.x, nearPath.y, out point, out path)) return false;
        int id;
        if (!registry.TryReserve(SpawnRegistry.SpawnCategory.Energy, this, point.transform.position, GroundSpawn.FootprintRadius(corePrefab), EnergyCapacity(), out id)) return false;
        pending.Add(id); StartCoroutine(SpawnAt(point.transform.position, id)); return true;
    }
    IEnumerator SpawnAt(Vector3 point, int reservation) {
        GameObject core = Instantiate(corePrefab, point, Quaternion.identity);
        pending.Remove(reservation);
        inFlight.Add(core);
        Collider trigger = core.GetComponent<Collider>();
        RaycastHit ground;
        if (!GroundSpawn.PlaceAtGround(core.transform, point, trigger, out ground) || !registry.CommitAlive(reservation, core)) {
            Destroy(core); registry.Release(reservation); inFlight.Remove(core); yield break;
        }
        SpawnLease lease = core.GetComponent<SpawnLease>();
        if (lease == null) lease = core.AddComponent<SpawnLease>();
        lease.Bind(registry, reservation);
        Treasure treasure = core.GetComponent<Treasure>();
        if (treasure != null) alive.Add(treasure);
        yield return GroundSpawn.ScaleIn(core.transform, trigger, GroundSpawn.ScaleInDuration);
        if (core != null) registry.UpdatePosition(core, core.transform.position);
        inFlight.Remove(core);
    }
    void ReplaceStaleCore() {
        if (AliveCount == 0) { staleTimer = 0f; return; }
        staleTimer += Time.deltaTime;
        if (staleTimer < staleDelay) return;
        staleTimer = 0f;
        bool nearExists = false; Treasure far = null; float farPath = -1f;
        foreach (Treasure t in FindObjectsOfType<Treasure>()) {
            float path = PathFromRobot(t.transform.position);
            nearExists |= path <= nearPath.y;
            if (path > farPath && Vector3.Distance(t.transform.position, player.position) >= 3f && !Visible(t.transform.position)) { far = t; farPath = path; }
        }
        if (nearExists || far == null) return;
        SpawnPoint point; float d;
        if (!TryGetCandidate(false, true, nearPath.x, nearPath.y, out point, out d)) return;
        // Same-frame replacement: release the stale slot, then reserve the selected near point atomically.
        registry.ReleaseAlive(far.gameObject);
        alive.Remove(far.GetComponent<Treasure>());
        Destroy(far.gameObject);
        int id;
        if (registry.TryReserve(SpawnRegistry.SpawnCategory.Energy, this, point.transform.position, GroundSpawn.FootprintRadius(corePrefab), EnergyCapacity(), out id)) {
            pending.Add(id); StartCoroutine(SpawnAt(point.transform.position, id));
        }
    }
    bool Visible(Vector3 p) {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.z > 0f && v.x >= .05f && v.x <= .95f && v.y >= .05f && v.y <= .95f;
    }
    public List<Vector3> Occupied() { return registry != null ? registry.OccupiedPoints() : new List<Vector3>(); }
}
