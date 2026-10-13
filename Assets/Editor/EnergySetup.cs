using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Authors reachable safe and separate edge-risk ground candidates; run after EnemySetup has baked navigation.</summary>
public static class EnergySetup {
    [MenuItem("Tools/Robo Lac Loi/Apply Energy Setup")]
    public static void Apply() {
        GameObject core = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
        ConfigureCorePrefab();
        core = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
        foreach (LevelConfig level in LevelCatalog.All) {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
            GameManager manager = Object.FindObjectOfType<GameManager>();
            EnergySpawnDirector director = manager.GetComponent<EnergySpawnDirector>();
            if (director == null) director = manager.gameObject.AddComponent<EnergySpawnDirector>();
            director.corePrefab = core;
            director.minSpacing = 1.5f;
            director.spreadSpacing = 8f;   // cores 8 m apart (stepped down only on small maps)
            director.minPlayerDistance = 3f;
            director.staleDelay = 12f;
            director.intervalOverride = -1f;
            director.nearPath = new Vector2(5f,18f);
            NavMeshLoader loader = manager.GetComponent<NavMeshLoader>();
            if (loader == null || loader.data == null) throw new System.Exception("EnergySetup: " + level.levelId + " has no baked NavMesh; run Apply Enemy Setup first");
            NavMeshDataInstance nav = NavMesh.AddNavMeshData(loader.data);
            try {
                director.landingPoints = LandingPoints(level.energyPointMinimum).ToArray();
            } finally { nav.Remove(); }
            RemoveAuthoredCores();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("EnergySetup: " + level.levelId + " ground candidates " + director.landingPoints.Length);
        }
    }

    static void ConfigureCorePrefab() {
        foreach (string prefabName in new[] { "Coin", "Coin Bouncy", "Sil Coins", "Vin Coins" }) {
        string prefabPath="Assets/Prefabs/"+prefabName+".prefab";
        Material glow=AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/RoboLacLoi/Materials/CoreGlow.mat");
        GameObject contents=PrefabUtility.LoadPrefabContents(prefabPath);
        try {
            foreach (Treasure treasure in contents.GetComponentsInChildren<Treasure>(true)) {
            Transform coreRoot = treasure.transform;
            treasure.value = 1;
            Transform model=coreRoot.Find("Model");
            if(model!=null) {
                Renderer renderer=model.GetComponentInChildren<Renderer>();
                if(renderer!=null&&renderer.bounds.size.y>0.001f)model.localScale*=0.9f/renderer.bounds.size.y;
                EnergyBob bob=model.GetComponent<EnergyBob>();
                if(bob!=null)bob.bobHeight=.08f;
            }
            SphereCollider trigger=coreRoot.GetComponent<SphereCollider>();
            if(trigger!=null){trigger.radius=1f;trigger.isTrigger=true;}
            Transform ring=coreRoot.Find("Ground Ring");
            if(ring==null){GameObject go=new GameObject("Ground Ring");ring=go.transform;ring.SetParent(coreRoot,false);}
            ring.localPosition=new Vector3(0f,-.05f,0f);
            ring.localRotation=Quaternion.identity;
            GroundRingVisual visual=ring.GetComponent<GroundRingVisual>();
            if(visual==null)visual=ring.gameObject.AddComponent<GroundRingVisual>();
            visual.material=glow;visual.radius=1.5f;visual.width=.1f;
            LineRenderer line=ring.GetComponent<LineRenderer>();
            if(line==null)line=ring.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace=false;line.loop=true;line.positionCount=33;line.startWidth=line.endWidth=visual.width;
            line.alignment=LineAlignment.TransformZ;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            if(glow!=null)line.sharedMaterial=glow;
            for(int i=0;i<33;i++){float a=i*Mathf.PI*2f/32f;line.SetPosition(i,new Vector3(Mathf.Cos(a)*visual.radius,0f,Mathf.Sin(a)*visual.radius));}
            }
            PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);
        } finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }

    public static int PointTarget(int order) { return new[] { 20, 40, 64, 88 }[Mathf.Clamp(order, 1, 4) - 1]; }

    struct Candidate {
        public Vector3 point;
        public float path;
        public SpawnRisk risk;
        public float edgeClearance;
        public Vector3 escape;
        public int region, cluster;
    }
    static float PathFromStart(Vector3 start, Vector3 point) {
        NavMeshHit from, to;
        if (!NavMesh.SamplePosition(start, out from, 3f, NavMesh.AllAreas) || !NavMesh.SamplePosition(point, out to, 1.5f, NavMesh.AllAreas)) return -1f;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return -1f;
        return EnergySpawnDirector.PathLength(path);
    }
    static Vector3 StartGround() {
        Vector3 start = WalkableGrid.PlayerStart();
        RaycastHit hit;
        return WalkableGrid.Ground(start.x, start.z, out hit) ? hit.point : start;
    }
    static bool HasClearance(Vector3 point, float clearance) {
        for (int i=0;i<16;i++) {
            float angle = i * Mathf.PI * 2f / 16f;
            Vector3 p = point + new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle)) * clearance;
            RaycastHit hit;
            if (!WalkableGrid.Ground(p.x,p.z,out hit) || Mathf.Abs(hit.point.y-point.y)>1.5f) return false;
        }
        return true;
    }
    static bool NoObstacle(Vector3 ground) {
        foreach (Collider c in Physics.OverlapSphere(ground + Vector3.up * 0.65f, 0.4f, ~0, QueryTriggerInteraction.Ignore))
            if (c.attachedRigidbody == null && c.bounds.max.y > ground.y + 0.25f && c.bounds.min.y < ground.y + 1.0f) return false;
        return true;
    }
    static List<Transform> LandingPoints(int minimum) {
        WalkableGrid.BeginHazardSnapshot(Object.FindObjectsOfType<HazardVolume>());
        try { return LandingPointsWithSnapshot(minimum); }
        finally { WalkableGrid.EndHazardSnapshot(); }
    }
    static List<Transform> LandingPointsWithSnapshot(int minimum) {
        GameObject old = GameObject.Find("Energy Landing Points");
        if (old != null) Object.DestroyImmediate(old);
        Transform root = new GameObject("Energy Landing Points").transform;
        Vector3 start = StartGround();
        float footprint = PickupFootprintRadius();
        float minimumEdge = RequiredEdgeClearance() + footprint;
        var safe = new List<Candidate>();
        var risk = new List<Candidate>();
        Vector3 playerStart = WalkableGrid.PlayerStart();
        Dictionary<Vector2Int, float> reachable = WalkableGrid.Reachable(playerStart);
        foreach (Vector2Int cell in reachable.Keys) {
            if (cell.x % 2 != 0 || cell.y % 2 != 0) continue;
            Vector3 sample = new Vector3(cell.x, 0f, cell.y);
            RaycastHit hit;
            if (!WalkableGrid.Ground(sample.x,sample.z,out hit) || !NoObstacle(hit.point)) continue;
            float path = PathFromStart(start, hit.point);
            if (path < 0f) continue;
            Vector3 escape;
            float edge = WalkableGrid.EdgeClearance(hit.point, out escape, 4f);
            Vector3 point = hit.point;
            int region = Mathf.FloorToInt(point.x / 16f) * 10000 + Mathf.FloorToInt(point.z / 16f);
            if (edge >= 2.5f && HasClearance(point, 2.5f) && WalkableGrid.FootprintGround(point, footprint))
                safe.Add(new Candidate { point=point, path=path, risk=SpawnRisk.None, edgeClearance=edge, escape=escape, region=region });
            else if (edge >= Mathf.Max(1.2f, minimumEdge) && edge <= 1.8f && escape != Vector3.zero
                && HasClearance(point, footprint) && WalkableGrid.FootprintGround(point, footprint))
                risk.Add(new Candidate { point=point, path=path, risk=SpawnRisk.Edge, edgeClearance=edge, escape=escape, region=region });
        }
        int coarseNearSafeCount = safe.Count(c => c.path >= 5f && c.path <= 18f);
        int fineNearAdded = 0;
        // The two-metre authoring grid can skip narrow but fully safe start areas. Search those same
        // reachable surfaces on a half-metre grid, retaining every ground, obstacle, clearance,
        // footprint and complete-path check used by the coarse candidates.
        float minX = Mathf.Floor((playerStart.x - 20f) * 2f) * 0.5f;
        float maxX = Mathf.Ceil((playerStart.x + 20f) * 2f) * 0.5f;
        float minZ = Mathf.Floor((playerStart.z - 20f) * 2f) * 0.5f;
        float maxZ = Mathf.Ceil((playerStart.z + 20f) * 2f) * 0.5f;
        for (float x = minX; x <= maxX; x += 0.5f) {
            for (float z = minZ; z <= maxZ; z += 0.5f) {
                Vector2Int cell = new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(z));
                float reachableHeight;
                if (!reachable.TryGetValue(cell, out reachableHeight)) continue;
                RaycastHit hit;
                if (!WalkableGrid.Ground(x, z, out hit) || Mathf.Abs(hit.point.y - reachableHeight) > WalkableGrid.MaxClimb
                    || !NoObstacle(hit.point)) continue;
                float path = PathFromStart(start, hit.point);
                if (path < 5f || path > 18f) continue;
                Vector3 point = hit.point;
                if (safe.Any(c => Vector3.Distance(c.point, point) < 1.5f)) continue;
                Vector3 escape;
                float edge = WalkableGrid.EdgeClearance(point, out escape, 4f);
                if (edge < 2.5f || !HasClearance(point, 2.5f) || !WalkableGrid.FootprintGround(point, footprint)) continue;
                int region = Mathf.FloorToInt(point.x / 16f) * 10000 + Mathf.FloorToInt(point.z / 16f);
                safe.Add(new Candidate { point=point, path=path, risk=SpawnRisk.None, edgeClearance=edge, escape=escape, region=region });
                fineNearAdded++;
            }
        }
        int rawNearSafeCount = safe.Count(c => c.path >= 5f && c.path <= 18f);

        // Keep the ordinary candidate set complete first. If fewer than the required count pass validation, report it;
        // do not widen clearance or admit points outside the connected walkable region.
        int target = Mathf.Max(minimum, 0);
        // Grow the cell until the map gives about 1.5× the level's minimum: even coverage without thousands of
        // points (the runtime measures a NavMesh path to every candidate when it picks).
        float gridCell = GridCell;
        var selected = GridSpread(safe, gridCell, 1);
        while (selected.Count > target * 3 / 2 && gridCell < 20f) {
            gridCell += 0.5f;
            selected = GridSpread(safe, gridCell, 1);
        }
        // The start area (5–18 m by path) keeps the fine 3 m grid: the first core and the near fallback live there.
        foreach (Candidate near in GridSpread(safe.Where(c => c.path <= 18f).ToList(), GridCell, 500000))
            if (selected.All(c => Vector3.Distance(c.point, near.point) >= GridCell))
                selected.Add(near);
        int selectedNearSafeCount = selected.Count(c => c.risk == SpawnRisk.None && c.path >= 5f && c.path <= 18f);
        Debug.Log("EnergySetup: near safe candidates (5-18 m path) coarse=" + coarseNearSafeCount
            + ", subgrid added=" + fineNearAdded + ", raw validated=" + rawNearSafeCount
            + ", selected=" + selectedNearSafeCount);
        // Edge points are an additional layer for risk-budget batches.
        if (selected.Count < target)
            Debug.LogWarning("EnergySetup: the " + gridCell + " m grid gives only " + selected.Count + " safe points; target " + target);
        // Edge points: an extra layer (about a third of the safe count) on a coarser grid for the risk share.
        float riskCell = gridCell * 2f;
        var edges = GridSpread(risk, riskCell, 1000000);
        while (edges.Count > Mathf.Max(6, target / 3) && riskCell < 24f) {
            riskCell += 1f;
            edges = GridSpread(risk, riskCell, 1000000);
        }
        selected.AddRange(edges);
        var points = new List<Transform>();
        for (int i=0;i<selected.Count;i++) {
            Candidate c=selected[i];
            Transform t=new GameObject("Ground Spawn Point "+i).transform;
            t.SetParent(root,false); t.position=c.point;
            SpawnPoint meta=t.gameObject.AddComponent<SpawnPoint>();
            meta.regionId=c.region; meta.clusterId=c.cluster; meta.riskFlags=c.risk;
            meta.edgeClearance=c.edgeClearance; meta.escapeDirection=c.escape;
            points.Add(t);
        }
        if (safe.Count < target) Debug.LogWarning("EnergySetup: only "+safe.Count+" safe candidates passed ground/path/footprint validation; target "+target);
        return points;
    }
    static float PickupFootprintRadius() {
        GameObject core=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
        if(core==null)return 0.4f;
        return GroundSpawn.FootprintRadius(core);
    }
    static float RequiredEdgeClearance() {
        GameObject player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        float radius=.5f;
        if(player!=null) {
            Collider collider=player.GetComponent<Collider>();
            if(collider is SphereCollider) radius=((SphereCollider)collider).radius*Mathf.Max(player.transform.lossyScale.x,player.transform.lossyScale.z);
            else if(collider!=null) radius=Mathf.Max(collider.bounds.extents.x,collider.bounds.extents.z);
        }
        return radius+.25f;
    }
    const float GridCell = 3f;

    // Playtest: points used to come in 1.5–2.5 m clusters near the start. One point per cell over every reachable
    // cell covers the whole map evenly; the runtime keeps cores 8 m apart. Each point is its own cluster.
    static List<Candidate> GridSpread(List<Candidate> source, float cell, int firstCluster) {
        var result = new List<Candidate>();
        foreach (var bucket in source.GroupBy(c => new Vector2Int(Mathf.FloorToInt(c.point.x / cell), Mathf.FloorToInt(c.point.z / cell)))
                     .OrderBy(g => g.Min(c => c.path))) {
            Vector2 centre = ((Vector2)bucket.Key + Vector2.one * 0.5f) * cell;
            Candidate pick = bucket.OrderBy(c => Vector2.Distance(new Vector2(c.point.x, c.point.z), centre)).First();
            pick.cluster = firstCluster + result.Count;
            result.Add(pick);
        }
        return result;
    }
    static void RemoveAuthoredCores() {
        foreach (Treasure t in Object.FindObjectsOfType<Treasure>()) {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
            if (root != null && root != t.gameObject) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform parent=t.transform.parent;
            Object.DestroyImmediate(t.gameObject);
            while(parent!=null&&parent.childCount==0&&parent.GetComponents<Component>().Length==1) {
                Transform up=parent.parent; Object.DestroyImmediate(parent.gameObject); parent=up;
            }
        }
    }
}
