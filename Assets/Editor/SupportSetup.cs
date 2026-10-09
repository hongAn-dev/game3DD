using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Support items (spec §6.3/§6.4): builds "Support Shield/Heal10/Heal20" prefabs (static trigger root with
/// SupportPickup, spinning crystal child at 0.6 m) from Tools/blender/make_support.py's models, and gives the Player
/// prefab its ShieldEffect with a 25%-opaque bubble material. Re-runnable.
/// </summary>
public static class SupportSetup {

	const string Models = "Assets/ThirdParty/RoboLacLoi/Models/";
	const string BubblePath = "Assets/ThirdParty/RoboLacLoi/Materials/ShieldBubble.mat";
	public const float ItemHeight = 0.6f;

	[MenuItem("Tools/Robo Lac Loi/Apply Support Setup")]
	public static void Apply() {
		// Own materials: the FBX's embedded emission imports far too bright (all three items looked white).
		Paint(BuildPickup("Assets/Prefabs/Support Shield.prefab", Models + "SupportShield.fbx", SupportKind.Shield, ItemHeight),
			"SupportShield", new Color(0.20f, 0.45f, 1f), new Color(0.85f, 0.92f, 1f));
		Paint(BuildPickup("Assets/Prefabs/Support Heal10.prefab", Models + "SupportHeal10.fbx", SupportKind.Heal10, ItemHeight),
			"SupportHeal10", new Color(0.15f, 0.70f, 0.25f), new Color(0.90f, 1f, 0.90f));
		Paint(BuildPickup("Assets/Prefabs/Support Heal20.prefab", Models + "SupportHeal20.fbx", SupportKind.Heal20, ItemHeight),
			"SupportHeal20", new Color(0.35f, 1f, 0.40f), new Color(1f, 1f, 0.75f));
		PlayerShield();
		Directors();
		TextureImporter icon = (TextureImporter)AssetImporter.GetAtPath("Assets/ThirdParty/RoboLacLoi/Sprites/shield_icon.png");
		if (icon.textureType != TextureImporterType.Sprite) {
			icon.textureType = TextureImporterType.Sprite;
			icon.alphaIsTransparency = true;
			icon.SaveAndReimport();
		}
		Debug.Log("SupportSetup: done");
	}

	/// <summary>Static trigger root (radius 0.6) with SupportPickup; the model child spins/bobs and is scaled to height.</summary>
	public static GameObject BuildPickup(string prefabPath, string modelPath, SupportKind kind, float height) {
		GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
		try {
			SphereCollider trigger = root.AddComponent<SphereCollider>();
			trigger.isTrigger = true;
			trigger.radius = 0.6f;
			SupportPickup pickup = root.AddComponent<SupportPickup>();
			pickup.kind = kind;
			pickup.lifetime = 25f;
			pickup.blinkTime = 3f;
			GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
			model.name = "Model";
			Bounds b = model.GetComponentInChildren<Renderer>().bounds;
			model.transform.localScale *= height / b.size.y;
			b = model.GetComponentInChildren<Renderer>().bounds;
			model.transform.localPosition -= b.center;
			model.AddComponent<EnergyBob>();
			return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
		} finally {
			Object.DestroyImmediate(root);
		}
	}

	// One SupportSpawnDirector per level on the GameManager object; the old Overdrive spawner is removed.
	static void Directors() {
		Material marker = AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/RoboLacLoi/Materials/CoreGlow.mat");
		foreach (LevelConfig level in LevelCatalog.All) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			GameManager manager = Object.FindObjectOfType<GameManager>();
			foreach (MonoBehaviour old in manager.GetComponents<MonoBehaviour>())
				if (old != null && old.GetType().Name == "OverdriveDirector")
					Object.DestroyImmediate(old);
			SupportSpawnDirector director = manager.GetComponent<SupportSpawnDirector>();
			if (director == null)
				director = manager.gameObject.AddComponent<SupportSpawnDirector>();
			director.shieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Support Shield.prefab");
			director.heal10Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Support Heal10.prefab");
			director.heal20Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Support Heal20.prefab");
			director.overdrivePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Overdrive.prefab");
			director.markerMaterial = marker;
			director.intervalOverride = -1f;
			director.lifetimeOverride = -1f;
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
		}
	}

	// Crystal (first material slot) and emblem (second) with a moderate glow; readable without bloom (spec §6.4).
	static void Paint(GameObject prefab, string name, Color crystal, Color emblem) {
		string folder = "Assets/ThirdParty/RoboLacLoi/Materials/";
		Material body = AssetReplacer.EnsureMaterial(folder + name + "Glow.mat", crystal, crystal * 0.45f);
		Material mark = AssetReplacer.EnsureMaterial(folder + name + "Emblem.mat", emblem, emblem * 0.3f);
		string path = AssetDatabase.GetAssetPath(prefab);
		GameObject contents = PrefabUtility.LoadPrefabContents(path);
		try {
			foreach (Renderer r in contents.GetComponentsInChildren<Renderer>()) {
				Material[] slots = r.sharedMaterials;
				for (int i = 0; i < slots.Length; i++)
					slots[i] = slots[i] != null && slots[i].name.Contains("Emblem") ? mark : body;
				r.sharedMaterials = slots;
			}
			PrefabUtility.SaveAsPrefabAsset(contents, path);
		} finally {
			PrefabUtility.UnloadPrefabContents(contents);
		}
	}

	static void PlayerShield() {
		Material bubble = AssetDatabase.LoadAssetAtPath<Material>(BubblePath);
		if (bubble == null) {
			bubble = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
			AssetDatabase.CreateAsset(bubble, BubblePath);
		}
		bubble.color = new Color(0.45f, 0.7f, 1f, 0.25f);
		EditorUtility.SetDirty(bubble);
		GameObject contents = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
		try {
			ShieldEffect shield = contents.GetComponent<ShieldEffect>();
			if (shield == null)
				shield = contents.AddComponent<ShieldEffect>();
			shield.bubbleMaterial = bubble;
			shield.bubbleScale = 1.15f;
			PrefabUtility.SaveAsPrefabAsset(contents, "Assets/Prefabs/Player.prefab");
		} finally {
			PrefabUtility.UnloadPrefabContents(contents);
		}
	}
}
