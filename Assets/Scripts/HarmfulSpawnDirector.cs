using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Owns harmful pickup budget and warning pulse; placements reserve shared level footprints.</summary>
public class HarmfulSpawnDirector : MonoBehaviour {
    public GameObject poisonPrefab, antiEnergyPrefab, magneticPrefab;
    public float pulseDuration = 0.5f;
    public float intervalOverride = -1f;
    public float lifetimeOverride = -1f;
    public static HarmfulSpawnDirector Current { get; private set; }

    LevelConfig config;
    EnergySpawnDirector energy;
    SpawnRegistry registry;
    readonly List<HarmfulPickup> items = new List<HarmfulPickup>();
    readonly HashSet<int> pending = new HashSet<int>();
    readonly List<GameObject> spawning = new List<GameObject>();
    float timer, next;
    public int AliveCount { get { return registry != null ? registry.Count(SpawnRegistry.SpawnCategory.Harmful) : items.Count + pending.Count; } }

    void Awake() { Current=this; }
    void Start() {
        config=LevelCatalog.Get(SceneManager.GetActiveScene().name);
        energy=GetComponent<EnergySpawnDirector>(); registry=SpawnRegistry.Current!=null?SpawnRegistry.Current:GetComponent<SpawnRegistry>();
		if(config==null||energy==null||registry==null||EffectiveCap()<=0){enabled=false;return;}
        GameFlow.Changed+=OnFlowChanged;
        // Energy cores are created by their director in Start. Wait one frame so
        // initial penalties can deliberately surround real drops instead of falling
        // back to an arbitrary risk point when script execution order changes.
        StartCoroutine(SpawnInitial());
        Schedule();
    }
    IEnumerator SpawnInitial() {
        yield return null;
		for(int i=0;i<EffectiveAtStart()&&i<EffectiveCap();i++)
            TrySpawn(RandomKind(), true);
    }
    void OnDestroy() { GameFlow.Changed-=OnFlowChanged; if(Current==this)Current=null; if(registry!=null)registry.ReleaseOwner(this); }
    void OnDisable() { CancelPending(); }
    void OnFlowChanged(FlowState state) {
        if(state!=FlowState.Dead&&state!=FlowState.LevelComplete&&state!=FlowState.Transition)return;
        CancelPending();
        foreach(HarmfulPickup item in new List<HarmfulPickup>(items))if(item!=null)item.Finish();
        items.Clear();
    }
    void CancelPending() {
        StopAllCoroutines();
        foreach(int id in pending)registry.Release(id);
        pending.Clear();
        foreach(GameObject go in spawning)if(go!=null)Destroy(go);
        spawning.Clear();
    }
    void Schedule() {
		float lo=intervalOverride>0f?intervalOverride:EffectiveIntervalMin();
		float hi=intervalOverride>0f?intervalOverride:EffectiveIntervalMax();
        next=Random.Range(lo,hi); timer=0f;
    }

	// Keep authored challenge values intact while giving the live game a much denser, map-wide hazard field.
	int EffectiveCap() { return Mathf.Max(6, config.energyCap + 8); }
	int EffectiveAtStart() { return Mathf.Clamp(Mathf.Max(config.harmfulAtStart, Mathf.CeilToInt(EffectiveCap() * 0.25f)), 0, EffectiveCap()); }
	float EffectiveIntervalMin() { return Mathf.Max(1.25f, config.harmfulIntervalMin * 0.35f); }
	float EffectiveIntervalMax() { return Mathf.Max(2f, config.harmfulIntervalMax * 0.5f); }

    HarmfulKind RandomKind() {
        float roll=Random.value;
		return roll<.45f?HarmfulKind.Poison:roll<.55f?HarmfulKind.AntiEnergy:HarmfulKind.Magnetic;
    }
    GameObject Prefab(HarmfulKind kind) {
        switch(kind){case HarmfulKind.Poison:return poisonPrefab;case HarmfulKind.AntiEnergy:return antiEnergyPrefab;default:return magneticPrefab;}
    }
    void Update() {
        if(!GameFlow.IsGameplayActive)return;
        items.RemoveAll(i=>i==null);
        timer+=Time.deltaTime;
        if(timer<next)return;
        Schedule();
		if(AliveCount>=EffectiveCap())return;
        TrySpawn(RandomKind(),false);
    }
    bool TrySpawn(HarmfulKind kind,bool initial) {
        GameObject prefab=Prefab(kind);
        if(prefab==null)return false;
        SpawnPoint point; float path;
		// Hazards are deliberately distributed across every reachable map region, not just energy-risk flags.
		if(!energy.TryGetCandidate(false,false,0f,float.MaxValue,out point,out path)
			&& !energy.TryGetRiskCandidate(false,initial?10f:3f,float.MaxValue,out point,out path))return false;
        int id;
		if(!registry.TryReserve(SpawnRegistry.SpawnCategory.Harmful,this,point.transform.position,GroundSpawn.FootprintRadius(prefab),EffectiveCap(),out id))return false;
        pending.Add(id);
        StartCoroutine(SpawnAndWarn(kind,prefab,point.transform.position,id));
        return true;
    }
    int Damage() {
        float scale=GameSettings.difficulty==GameSettings.gameDifficulties.Easy ? 0.8f :
            GameSettings.difficulty==GameSettings.gameDifficulties.Hard ? 1.2f : 1f;
        return Mathf.RoundToInt(config.harmfulDamage*scale);
    }
    IEnumerator SpawnAndWarn(HarmfulKind kind,GameObject prefab,Vector3 point,int id) {
        GameObject item=Instantiate(prefab,point,Quaternion.identity);
        pending.Remove(id); spawning.Add(item);
        Collider trigger=item.GetComponent<Collider>();
        if(trigger==null)trigger=item.GetComponentInChildren<Collider>();
        RaycastHit ground;
        if(!GroundSpawn.PlaceAtGround(item.transform,point,trigger,out ground)||!registry.CommitAlive(id,item)) {
            Destroy(item);registry.Release(id);spawning.Remove(item);yield break;
        }
        SpawnLease lease=item.GetComponent<SpawnLease>();
        if(lease==null)lease=item.AddComponent<SpawnLease>();
        lease.Bind(registry,id);
        HarmfulPickup pickup=item.GetComponent<HarmfulPickup>();
        pickup.kind=kind;
        pickup.damage=Damage();
		pickup.lifetime=lifetimeOverride>0f?lifetimeOverride:Mathf.Max(35f,config.harmfulLifetime);
        pickup.Gone+=p=>{items.Remove(p);registry.ReleaseAlive(p.gameObject);};
        items.Add(pickup);
        Vector3 full=item.transform.localScale;
        if(trigger!=null)trigger.enabled=false;
        float elapsed=0f;
        while(elapsed<pulseDuration&&item!=null) {
            if(GameFlow.IsGameplayActive)elapsed+=Time.deltaTime;
            float pulse=.82f+.18f*(.5f+.5f*Mathf.Sin(elapsed*Mathf.PI*8f));
            item.transform.localScale=full*pulse;
            yield return null;
        }
        if(item==null)yield break;
        item.transform.localScale=full;
        if(PlayerOverlaps(trigger)) {
            pickup.Finish(); spawning.Remove(item); yield break;
        }
        // Scale in after the warning; the collider remains off until the common activation point.
        Collider activeTrigger=trigger;
        if(activeTrigger!=null)activeTrigger.enabled=false;
        bool canceledAtActivation=false;
        yield return GroundSpawn.ScaleIn(item.transform,activeTrigger,GroundSpawn.ScaleInDuration,()=>{
            if(PlayerOverlaps(activeTrigger)) {
                if(activeTrigger!=null)activeTrigger.enabled=false;
                pickup.Finish();
                canceledAtActivation=true;
            } else pickup.BeginLifetime();
        });
        spawning.Remove(item);
        if(canceledAtActivation||pickup==null||pickup.Consumed)yield break;
    }
    bool PlayerOverlaps(Collider trigger) {
        if(trigger==null)return false;
        Collider[] hits;
        SphereCollider sphere=trigger as SphereCollider;
        if(sphere!=null) {
            Vector3 scale=sphere.transform.lossyScale;
            float radius=sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            hits=Physics.OverlapSphere(sphere.transform.TransformPoint(sphere.center),radius,~0,QueryTriggerInteraction.Collide);
        } else {
            BoxCollider box=trigger as BoxCollider;
            CapsuleCollider capsule=trigger as CapsuleCollider;
            if(box!=null) {
                Vector3 scale=box.transform.lossyScale;
                Vector3 half=Vector3.Scale(box.size,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)))*.5f;
                hits=Physics.OverlapBox(box.transform.TransformPoint(box.center),half,box.transform.rotation,~0,QueryTriggerInteraction.Collide);
            } else if(capsule!=null) {
                Vector3 scale=capsule.transform.lossyScale;
                Vector3 axis; float axial, radial;
                if(capsule.direction==0){axis=capsule.transform.right;axial=Mathf.Abs(scale.x);radial=Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z));}
                else if(capsule.direction==2){axis=capsule.transform.forward;axial=Mathf.Abs(scale.z);radial=Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y));}
                else {axis=capsule.transform.up;axial=Mathf.Abs(scale.y);radial=Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));}
                Vector3 center=capsule.transform.TransformPoint(capsule.center);
                float radius=capsule.radius*radial;
                float halfSegment=Mathf.Max(0f,capsule.height*axial*.5f-radius);
                hits=Physics.OverlapCapsule(center-axis*halfSegment,center+axis*halfSegment,radius,~0,QueryTriggerInteraction.Collide);
            } else {
                Bounds bounds=default(Bounds); bool found=false;
                foreach(Renderer renderer in trigger.GetComponentsInChildren<Renderer>(true)) {
                    if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
                }
                if(!found)bounds=new Bounds(trigger.transform.position,Vector3.one*.5f);
                hits=Physics.OverlapBox(bounds.center,bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Collide);
            }
        }
        foreach(Collider c in hits) {
            if(c==null||c==trigger)continue;
            if(c.GetComponentInParent<Health>()!=null||c.GetComponentInParent<BallUserControl>()!=null||c.CompareTag("Player"))return true;
        }
        return false;
    }
}
