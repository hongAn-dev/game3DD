using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Rebuilds the project-owned hazard models/materials and the isolated Ending planet material.</summary>
public static class ChallengeAssetSetup {
	const string Root = "Assets/ThirdParty/RoboLacLoi/";
	const string MaterialFolder = Root + "Materials/Harmful";
	const string PrefabFolder = "Assets/Prefabs/Harmful";
	const string TexturePath = Root + "Textures/planet_surface.png";
	const string PlanetPath = Root + "Materials/Planet.mat";
	const string AtmospherePath = Root + "Materials/PlanetAtmosphere.mat";
	// Harmful pickups wear the energy core's shape in colours opposed to its cyan (playtest): red takes HP,
	// magenta takes energy, orange slows the robot. Glow (body) and shard colours per kind.
	static readonly Color Poison = new Color32(235, 45, 45, 255);
	static readonly Color PoisonShard = new Color32(255, 150, 140, 255);
	static readonly Color AntiEnergy = new Color32(225, 40, 205, 255);
	static readonly Color AntiEnergyShard = new Color32(255, 150, 240, 255);
	static readonly Color Magnetic = new Color32(255, 105, 15, 255);
	static readonly Color MagneticShard = new Color32(255, 205, 130, 255);
	const string CoreModel = Root + "Models/EnergyCore.fbx";

	[MenuItem("Tools/Robo Lac Loi/Apply Challenge Assets")]
	public static void Apply() {
		EnsureFolder("Assets/ThirdParty/RoboLacLoi/Materials", "Harmful");
		EnsureFolder("Assets/Prefabs", "Harmful");
		ImportIcons();
		BuildPlanet();
		BuildCore("Poison", HarmfulKind.Poison, Poison, PoisonShard);
		BuildCore("AntiEnergy", HarmfulKind.AntiEnergy, AntiEnergy, AntiEnergyShard);
		BuildCore("MagneticInterference", HarmfulKind.Magnetic, Magnetic, MagneticShard);
		AssetDatabase.SaveAssets();
		Debug.Log("ChallengeAssetSetup: planet materials and three harmful-item prefabs rebuilt");
	}

	static void ImportIcons() {
		foreach (string name in new[] { "music_on", "music_off", "trap_poison", "trap_anti_energy", "trap_magnetic" }) {
			string path = Root + "Sprites/" + name + ".png";
			TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer == null)
				continue;
			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Single;
			importer.alphaIsTransparency = true;
			importer.filterMode = FilterMode.Bilinear;
			importer.maxTextureSize = 128;
			importer.SaveAndReimport();
		}
	}

	static void EnsureFolder(string parent, string name) {
		string path = parent + "/" + name;
		if (!AssetDatabase.IsValidFolder(path))
			AssetDatabase.CreateFolder(parent, name);
	}

	static void BuildPlanet() {
		Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
		if (texture == null) {
			Debug.LogWarning("ChallengeAssetSetup: generate " + TexturePath + " with Tools/ui/make_planet_texture.py first");
			return;
		}
		TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
		importer.textureType = TextureImporterType.Default;
		importer.sRGBTexture = true;
		importer.mipmapEnabled = true;
		importer.wrapMode = TextureWrapMode.Repeat;
		importer.filterMode = FilterMode.Bilinear;
		importer.maxTextureSize = 2048;
		importer.SaveAndReimport();
		texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
		Material planet = AssetDatabase.LoadAssetAtPath<Material>(PlanetPath);
		if (planet == null) {
			planet = new Material(Shader.Find("Standard")) { name = "Planet" };
			AssetDatabase.CreateAsset(planet, PlanetPath);
		}
		planet.shader = Shader.Find("Standard");
		planet.color = Color.white;
		planet.mainTexture = texture;
		planet.SetFloat("_Metallic", 0f);
		planet.SetFloat("_Glossiness", 0.14f);
		EditorUtility.SetDirty(planet);

		Material atmosphere = AssetDatabase.LoadAssetAtPath<Material>(AtmospherePath);
		if (atmosphere == null) {
			Shader rim = Shader.Find("RoboLacLoi/Planet Rim");
			atmosphere = new Material(rim) { name = "PlanetAtmosphere" };
			AssetDatabase.CreateAsset(atmosphere, AtmospherePath);
		}
		if (atmosphere.HasProperty("_RimColor"))
			atmosphere.SetColor("_RimColor", new Color(0.22f, 0.58f, 0.72f, 0.45f));
		else if (atmosphere.HasProperty("_TintColor"))
			atmosphere.SetColor("_TintColor", new Color32(99, 191, 222, 30));
		else if (atmosphere.HasProperty("_Color"))
			atmosphere.SetColor("_Color", new Color32(99, 191, 222, 30));
		EditorUtility.SetDirty(atmosphere);
	}

	static GameObject RootObject(string name, float radius = 0.47f) {
		GameObject root = new GameObject(name);
		SphereCollider collider = root.AddComponent<SphereCollider>();
		collider.isTrigger = true;
		collider.radius = radius;
		collider.center = new Vector3(0f, 0.47f, 0f);
		HarmfulPickup pickup = root.AddComponent<HarmfulPickup>();
		pickup.lifetime = 20f;
		GameObject model = new GameObject("Model");
		model.transform.SetParent(root.transform, false);
		model.AddComponent<EnergyBob>().bobHeight = .06f;
		return root;
	}

	static void SavePrefab(GameObject root) {
		Transform model = root.transform.Find("Model");
		Bounds bounds;
		// Largest side 0.9 m: a flat symbol (the minus) must not grow to 2+ m wide when fitted by height.
		float side = model != null && GroundSpawn.TryBounds(model, out bounds) ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : 0f;
		if (side > .001f) model.localScale *= .9f / side;
		if (GroundSpawn.TryBounds(model, out bounds)) {
			SphereCollider trigger = root.GetComponent<SphereCollider>();
			trigger.center = root.transform.InverseTransformPoint(bounds.center);
			trigger.radius = Mathf.Max(.42f, bounds.extents.x, bounds.extents.y, bounds.extents.z);
		}
		GameObject ring = new GameObject("Ground Ring");
		ring.transform.SetParent(root.transform, false);
		GroundRingVisual glow = ring.AddComponent<GroundRingVisual>();
		glow.radius = .58f; glow.width = .055f;
		glow.material = root.GetComponentInChildren<Renderer>().sharedMaterial;
		string path = PrefabFolder + "/" + root.name + ".prefab";
		PrefabUtility.SaveAsPrefabAsset(root, path);
		Object.DestroyImmediate(root);
	}

	// The energy core model (EnergyCore.fbx) with the kind's own glow/shard materials; no Treasure on it.
	static void BuildCore(string name, HarmfulKind kind, Color glow, Color shard) {
		GameObject root = RootObject(name);
		root.GetComponent<HarmfulPickup>().kind = kind;
		Material glowMat = CoreMaterial(MaterialFolder + "/" + name + "Glow.mat", "CoreGlow", glow, 1.5f);
		Material shardMat = CoreMaterial(MaterialFolder + "/" + name + "Shard.mat", "CoreShard", shard, 1.2f);
		GameObject core = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CoreModel));
		PrefabUtility.UnpackPrefabInstance(core, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
		core.name = "Core";
		core.transform.SetParent(root.transform.Find("Model"), false);
		foreach (Renderer r in core.GetComponentsInChildren<Renderer>()) {
			Material[] mats = r.sharedMaterials;
			for (int i = 0; i < mats.Length; i++)
				mats[i] = mats[i] != null && mats[i].name.Contains("Shard") ? shardMat : glowMat;
			r.sharedMaterials = mats;
		}
		foreach (Collider c in core.GetComponentsInChildren<Collider>())
			Object.DestroyImmediate(c);
		SavePrefab(root);
	}

	// A copy of the energy core's material (same shader and emission setup) in the kind's colour.
	static Material CoreMaterial(string path, string template, Color color, float emission) {
		Material source = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + template + ".mat");
		Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
		if (material == null) {
			material = new Material(source);
			AssetDatabase.CreateAsset(material, path);
		} else {
			material.CopyPropertiesFromMaterial(source);
			material.shader = source.shader;
		}
		material.color = color;
		if (material.HasProperty("_EmissionColor")) {
			material.EnableKeyword("_EMISSION");
			material.SetColor("_EmissionColor", color * emission * 0.6f);
		}
		EditorUtility.SetDirty(material);
		return material;
	}

}
