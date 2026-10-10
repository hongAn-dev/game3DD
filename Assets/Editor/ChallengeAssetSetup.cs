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
	const string CrystalMeshPath = Root + "Models/AntiEnergyCrystal.asset";
	static readonly Color Poison = new Color32(198, 154, 44, 255);
	static readonly Color PoisonDark = new Color32(82, 64, 27, 255);
	static readonly Color AntiEnergy = new Color32(255, 90, 54, 255);
	static readonly Color AntiEnergyDark = new Color32(102, 35, 32, 255);
	static readonly Color Magnetic = new Color32(159, 92, 224, 255);
	static readonly Color MagneticDark = new Color32(65, 40, 103, 255);

	[MenuItem("Tools/Robo Lac Loi/Apply Challenge Assets")]
	public static void Apply() {
		EnsureFolder("Assets/ThirdParty/RoboLacLoi/Materials", "Harmful");
		EnsureFolder("Assets/Prefabs", "Harmful");
		ImportIcons();
		Material poison = MakeMaterial(MaterialFolder + "/Poison.mat", Poison, 0.05f);
		Material poisonDark = MakeMaterial(MaterialFolder + "/PoisonDark.mat", PoisonDark, 0.05f);
		Material anti = MakeMaterial(MaterialFolder + "/AntiEnergy.mat", AntiEnergy, 0.15f);
		Material antiDark = MakeMaterial(MaterialFolder + "/AntiEnergyDark.mat", AntiEnergyDark, 0.1f);
		Material magnetic = MakeMaterial(MaterialFolder + "/Magnetic.mat", Magnetic, 0.15f);
		Material magneticDark = MakeMaterial(MaterialFolder + "/MagneticDark.mat", MagneticDark, 0.05f);
		BuildPlanet();
		BuildPoison(poison, poisonDark);
		BuildAntiEnergy(anti, antiDark);
		BuildMagnetic(magnetic, magneticDark);
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

	static Material MakeMaterial(string path, Color color, float metallic) {
		Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
		if (material == null) {
			Shader shader = Shader.Find("Standard");
			material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
			AssetDatabase.CreateAsset(material, path);
		}
		material.shader = Shader.Find("Standard");
		material.color = color;
		material.SetFloat("_Metallic", metallic);
		material.SetFloat("_Glossiness", 0.28f);
		EditorUtility.SetDirty(material);
		return material;
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

	static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale,
		Material material, Quaternion rotation = default(Quaternion)) {
		if (parent.GetComponent<HarmfulPickup>() != null) parent = parent.Find("Model");
		GameObject child = GameObject.CreatePrimitive(type);
		child.name = name;
		child.transform.SetParent(parent, false);
		child.transform.localPosition = position;
		child.transform.localScale = scale;
		if (rotation != default(Quaternion))
			child.transform.localRotation = rotation;
		Renderer renderer = child.GetComponent<Renderer>();
		renderer.sharedMaterial = material;
		Collider collider = child.GetComponent<Collider>();
		if (collider != null)
			Object.DestroyImmediate(collider);
		return child;
	}

	static void SavePrefab(GameObject root) {
		Transform model = root.transform.Find("Model");
		Bounds bounds;
		if (model != null && GroundSpawn.TryBounds(model, out bounds) && bounds.size.y > .001f) model.localScale *= .9f / bounds.size.y;
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

	static void BuildPoison(Material body, Material dark) {
		GameObject root = RootObject("Poison");
		root.GetComponent<HarmfulPickup>().kind = HarmfulKind.Poison;
		Primitive(root.transform, PrimitiveType.Cylinder, "Bottle body", new Vector3(0f, 0.39f, 0f), new Vector3(0.62f, 0.37f, 0.62f), body);
		Primitive(root.transform, PrimitiveType.Cylinder, "Bottle neck", new Vector3(0f, 0.76f, 0f), new Vector3(0.28f, 0.18f, 0.28f), dark);
		Primitive(root.transform, PrimitiveType.Cube, "Stopper", new Vector3(0f, 0.91f, 0f), new Vector3(0.33f, 0.08f, 0.33f), body);
		Primitive(root.transform, PrimitiveType.Sphere, "Skull", new Vector3(0f, 0.43f, -0.32f), new Vector3(0.2f, 0.18f, 0.07f), dark);
		Primitive(root.transform, PrimitiveType.Sphere, "Skull eye L", new Vector3(-0.055f, 0.45f, -0.36f), new Vector3(0.045f, 0.045f, 0.025f), body);
		Primitive(root.transform, PrimitiveType.Sphere, "Skull eye R", new Vector3(0.055f, 0.45f, -0.36f), new Vector3(0.045f, 0.045f, 0.025f), body);
		Primitive(root.transform, PrimitiveType.Cube, "Skull jaw", new Vector3(0f, 0.36f, -0.34f), new Vector3(0.12f, 0.04f, 0.035f), dark);
		SavePrefab(root);
	}

	static void BuildAntiEnergy(Material body, Material dark) {
		GameObject root = RootObject("AntiEnergy", 0.42f);
		root.GetComponent<HarmfulPickup>().kind = HarmfulKind.AntiEnergy;
		GameObject crystal = new GameObject("Minus crystal", typeof(MeshFilter), typeof(MeshRenderer));
		crystal.transform.SetParent(root.transform.Find("Model"), false);
		crystal.transform.localPosition = new Vector3(0f, 0.48f, 0f);
		crystal.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
		crystal.GetComponent<MeshFilter>().sharedMesh = Octahedron();
		crystal.GetComponent<MeshRenderer>().sharedMaterial = body;
		Primitive(root.transform, PrimitiveType.Cube, "Minus sign", new Vector3(0f, 0.49f, -0.36f), new Vector3(0.42f, 0.09f, 0.06f), dark);
		SavePrefab(root);
	}

	static Mesh Octahedron() {
		Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(CrystalMeshPath);
		if (saved != null)
			return saved;
		Mesh mesh = new Mesh { name = "AntiEnergyCrystal" };
		mesh.vertices = new[] {
			new Vector3(0, 0.5f, 0), new Vector3(0, -0.5f, 0), new Vector3(-0.5f, 0, 0),
			new Vector3(0, 0, 0.5f), new Vector3(0.5f, 0, 0), new Vector3(0, 0, -0.5f)
		};
		mesh.triangles = new[] { 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2, 1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5 };
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		AssetDatabase.CreateAsset(mesh, CrystalMeshPath);
		return mesh;
	}

	static void BuildMagnetic(Material body, Material poles) {
		GameObject root = RootObject("MagneticInterference");
		root.GetComponent<HarmfulPickup>().kind = HarmfulKind.Magnetic;
		Primitive(root.transform, PrimitiveType.Cylinder, "Magnet bridge", new Vector3(0f, 0.82f, 0f), new Vector3(0.12f, 0.43f, 0.12f), body,
			Quaternion.Euler(0f, 0f, 90f));
		Primitive(root.transform, PrimitiveType.Cylinder, "Magnet left", new Vector3(-0.32f, 0.54f, 0f), new Vector3(0.12f, 0.3f, 0.12f), body);
		Primitive(root.transform, PrimitiveType.Cylinder, "Magnet right", new Vector3(0.32f, 0.54f, 0f), new Vector3(0.12f, 0.3f, 0.12f), body);
		Primitive(root.transform, PrimitiveType.Cube, "Pole left", new Vector3(-0.32f, 0.35f, 0f), new Vector3(0.2f, 0.08f, 0.2f), poles);
		Primitive(root.transform, PrimitiveType.Cube, "Pole right", new Vector3(0.32f, 0.35f, 0f), new Vector3(0.2f, 0.08f, 0.2f), poles);
		SavePrefab(root);
	}
}
