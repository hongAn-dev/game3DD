using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Wires authored harmful pickup prefabs into each level director.</summary>
public static class HarmfulSetup {
    [MenuItem("Tools/Robo Lac Loi/Apply Harmful Setup")]
    public static void Apply() {
        GameObject poison=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Harmful/Poison.prefab");
        GameObject anti=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Harmful/AntiEnergy.prefab");
        GameObject magnetic=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Harmful/MagneticInterference.prefab");
        if(poison==null||anti==null||magnetic==null)throw new System.Exception("HarmfulSetup: run Challenge Asset Setup before wiring trap directors.");
        poison=EnsurePickupComponents("Assets/Prefabs/Harmful/Poison.prefab");
        anti=EnsurePickupComponents("Assets/Prefabs/Harmful/AntiEnergy.prefab");
        magnetic=EnsurePickupComponents("Assets/Prefabs/Harmful/MagneticInterference.prefab");
        foreach(LevelConfig level in LevelCatalog.All) {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+level.levelId+".unity",OpenSceneMode.Single);
            GameManager manager=Object.FindObjectOfType<GameManager>();
            HarmfulSpawnDirector director=manager.GetComponent<HarmfulSpawnDirector>();
            if(director==null)director=manager.gameObject.AddComponent<HarmfulSpawnDirector>();
            director.poisonPrefab=poison;
            director.antiEnergyPrefab=anti;
            director.magneticPrefab=magnetic;
            director.pulseDuration=0.5f;
            director.intervalOverride=-1f;
            director.lifetimeOverride=-1f;
            GameObject map=GameObject.Find("Minimap Camera");
            if(map!=null) {
                MinimapItemMarkers markers=map.GetComponent<MinimapItemMarkers>();
                if(markers==null)markers=map.AddComponent<MinimapItemMarkers>();
                markers.energyIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ThirdParty/RoboLacLoi/Sprites/core_icon.png");
                markers.poisonIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ThirdParty/RoboLacLoi/Sprites/trap_poison.png");
                markers.antiEnergyIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ThirdParty/RoboLacLoi/Sprites/trap_anti_energy.png");
                markers.magneticIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ThirdParty/RoboLacLoi/Sprites/trap_magnetic.png");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("HarmfulSetup: done");
    }

    static GameObject EnsurePickupComponents(string path) {
        GameObject root=PrefabUtility.LoadPrefabContents(path);
        try {
            HarmfulPickup pickup=root.GetComponent<HarmfulPickup>();
            if(pickup==null)pickup=root.AddComponent<HarmfulPickup>();
            Collider trigger=root.GetComponent<Collider>();
            if(trigger==null)trigger=root.AddComponent<SphereCollider>();
            trigger.isTrigger=true;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
