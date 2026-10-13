using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Support budget and selection; all items use the level shared reservation and ground placement services.</summary>
public class SupportSpawnDirector : MonoBehaviour {
    public static SupportSpawnDirector Current { get; private set; }
    public GameObject shieldPrefab, heal10Prefab, heal20Prefab, overdrivePrefab;
    public float intervalOverride = -1f;
    public float lifetimeOverride = -1f;
    public float[] weightsOverride;
    public float lowHealth = 40f;
    public float healNear = 18f;

    readonly List<SupportPickup> items = new List<SupportPickup>();
    readonly HashSet<int> pending = new HashSet<int>();
    readonly List<Vector3> pendingPoints = new List<Vector3>();
    readonly List<GameObject> inFlight = new List<GameObject>();
    LevelConfig config;
    EnergySpawnDirector energy;
    Health robot;
    SpawnRegistry registry;
    float elapsed, timer, next;
    bool firstShieldDone;
    public IReadOnlyList<SupportPickup> Items { get { items.RemoveAll(i => i == null); return items; } }
    public IReadOnlyList<Vector3> Pending { get { return pendingPoints; } }
    public int AliveCount { get { return registry != null ? registry.Count(SpawnRegistry.SpawnCategory.Support) : items.Count + pending.Count; } }

    public static SupportKind Choose(float[] weights, float roll) {
        float total = 0f; foreach (float w in weights) total += w;
        float pick = roll * total, sum = 0f;
        for (int i = 0; i < weights.Length; i++) { sum += weights[i]; if (weights[i] > 0f && pick < sum) return (SupportKind)i; }
        for (int i = weights.Length - 1; i >= 0; i--) if (weights[i] > 0f) return (SupportKind)i;
        return SupportKind.Shield;
    }
    void Awake() { Current = this; registry = GetComponent<SpawnRegistry>(); }
    void Start() {
        config = LevelCatalog.Get(SceneManager.GetActiveScene().name);
        energy = GetComponent<EnergySpawnDirector>();
        registry = SpawnRegistry.Current != null ? SpawnRegistry.Current : GetComponent<SpawnRegistry>();
        GameObject player = GameObject.FindWithTag("Player");
        robot = player != null ? player.GetComponent<Health>() : null;
        if (config == null || energy == null || registry == null) { enabled = false; return; }
        GameFlow.Changed += OnFlowChanged; ScheduleNext();
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
        pending.Clear(); pendingPoints.Clear();
        foreach (GameObject go in inFlight) if (go != null) Destroy(go);
        inFlight.Clear();
    }
    void ScheduleNext() { next = intervalOverride > 0f ? intervalOverride : Random.Range(config.supportIntervalMin, config.supportIntervalMax); timer = 0f; }
    bool LowHealth() { return robot != null && robot.healthPoints > 0f && robot.healthPoints <= lowHealth; }
    static bool IsHeal(SupportKind kind) { return kind == SupportKind.Heal10 || kind == SupportKind.Heal20; }
    bool HealNearRobot() {
        foreach (SupportPickup item in Items)
            if (IsHeal(item.kind) && energy.PathFromRobot(item.transform.position) <= healNear) return true;
        return false;
    }

    void Update() {
        if (!GameFlow.IsGameplayActive || config == null) return;
        elapsed += Time.deltaTime;
        if (!firstShieldDone && config.firstShieldAfter > 0f && elapsed >= config.firstShieldAfter && AliveCount < config.supportCap)
            firstShieldDone = TrySpawn(SupportKind.Shield, true, true);
        timer += Time.deltaTime;
        if (timer < (intervalOverride > 0f ? intervalOverride : next)) return;
        ScheduleNext();
        bool needHeal = LowHealth() && !HealNearRobot();
        if (AliveCount < config.supportCap) {
            TrySpawn(needHeal ? SupportKind.Heal20 : Choose(weightsOverride != null && weightsOverride.Length == 4 ? weightsOverride : config.supportWeights, Random.value), needHeal);
        } else if (needHeal && pending.Count == 0) {
            SupportPickup far = null;
            foreach (SupportPickup item in Items) {
                if (IsHeal(item.kind)) continue;
                float d = Vector3.Distance(item.transform.position, robot.transform.position);
                if (d >= 3f && (far == null || d > Vector3.Distance(far.transform.position, robot.transform.position))) far = item;
            }
            if (far != null) ReplaceWithHeal(far);
        }
    }

    GameObject PrefabFor(SupportKind kind) {
        switch (kind) {
            case SupportKind.Shield: return shieldPrefab;
            case SupportKind.Heal10: return heal10Prefab;
            case SupportKind.Heal20: return heal20Prefab;
            default: return overdrivePrefab;
        }
    }
    bool TrySpawn(SupportKind kind, bool near, bool visible = false) {
        GameObject prefab = PrefabFor(kind);
        if (prefab == null) return false;
        SpawnPoint point; float path;
        bool found = energy.TryGetCandidate(false, near, near ? 5f : 0f, near ? 18f : float.MaxValue, out point, out path, visible);
        // With the whole energy supply on the map the near visible points can all be taken (first shield, L1):
        // relax the path band but stay on screen; with no free visible point it simply tries again next frame.
        if (!found && visible)
            found = energy.TryGetCandidate(false, near, 0f, float.MaxValue, out point, out path, true)
                // Points beside energy cores count as "trap risk" (they invite harmful items); a shield there is fine.
                || energy.TryGetCandidate(true, near, 0f, float.MaxValue, out point, out path, true);
        if (!found && kind == SupportKind.Heal20 && near)
            found = energy.TryGetCandidate(false, true, 0f, float.MaxValue, out point, out path, visible);
        Vector3 ahead;
        if (!found && visible && energy.TryPointAhead(GroundSpawn.FootprintRadius(prefab), out ahead))
            return ReserveAndSpawn(kind, prefab, ahead);
        if (!found) return false;
        return ReserveAndSpawn(kind, prefab, point.transform.position);
    }
    void ReplaceWithHeal(SupportPickup far) {
        SpawnPoint point; float path;
        bool found = energy.TryGetCandidate(false, true, 5f, 18f, out point, out path);
        if (!found) found = energy.TryGetCandidate(false, true, 0f, float.MaxValue, out point, out path);
        if (!found) return;
        GameObject prefab = PrefabFor(SupportKind.Heal20);
        if (prefab == null) return;
        // Atomically replace the old reservation so the support cap remains strict.
        int id;
        if(!registry.TryReplaceAlive(far.gameObject, SpawnRegistry.SpawnCategory.Support, this,
            point.transform.position, GroundSpawn.FootprintRadius(prefab), config.supportCap, out id)) return;
        far.Finish();
        pending.Add(id);pendingPoints.Add(point.transform.position);
        StartCoroutine(Spawn(prefab,SupportKind.Heal20,point.transform.position,id));
    }
    bool ReserveAndSpawn(SupportKind kind, GameObject prefab, Vector3 point) {
        int id;
        if (!registry.TryReserve(SpawnRegistry.SpawnCategory.Support, this, point, GroundSpawn.FootprintRadius(prefab), config.supportCap, out id)) return false;
        pending.Add(id); pendingPoints.Add(point); StartCoroutine(Spawn(prefab, kind, point, id)); return true;
    }
    IEnumerator Spawn(GameObject prefab, SupportKind kind, Vector3 point, int id) {
        GameObject item = Instantiate(prefab, point, Quaternion.identity);
        pending.Remove(id); pendingPoints.Remove(point); inFlight.Add(item);
        Collider trigger = item.GetComponent<Collider>();
        RaycastHit ground;
        if (!GroundSpawn.PlaceAtGround(item.transform, point, trigger, out ground) || !registry.CommitAlive(id, item)) {
            Destroy(item); registry.Release(id); inFlight.Remove(item); yield break;
        }
        SpawnLease lease = item.GetComponent<SpawnLease>();
        if (lease == null) lease = item.AddComponent<SpawnLease>();
        lease.Bind(registry, id);
        SupportPickup pickup = item.GetComponent<SupportPickup>();
        pickup.kind = kind;
        if (lifetimeOverride > 0f) pickup.lifetime = lifetimeOverride;
        pickup.Gone += p => { items.Remove(p); registry.ReleaseAlive(p.gameObject); };
        items.Add(pickup);
        yield return GroundSpawn.ScaleIn(item.transform, trigger, GroundSpawn.ScaleInDuration);
        if(item!=null)registry.UpdatePosition(item,item.transform.position);
        inFlight.Remove(item);
    }
}
