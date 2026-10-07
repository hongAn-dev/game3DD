using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Applies the Robo Lac Loi zone look to each level: terrain colors, sky, fog, sun, decorations,
/// ambient loop and win/lose sounds. Run with -executeMethod ZoneDresser.DressAll
/// </summary>
public static class ZoneDresser {

	public class Zone {
		public string scene;
		public Color ground, accent, rock;
		public Color skyTint, ground_sky, fog, sun;
		public float fogDensity, sunIntensity;
		public string ambient;     // Assets path of a looping clip, or null
		public bool beacons;       // add win beacons on decorations
		public string[] extraTerrain = new string[0];  // other ground prefabs recolored like the terrain
		public Dictionary<string, string> props;  // old prefab name -> "Pack/model"
	}

	const string ThirdParty = "Assets/ThirdParty/";
	const string ZoneMaterials = "Assets/ThirdParty/RoboLacLoi/Materials/Zones/";

	static Color C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }

	public static readonly Zone[] Zones = {
		new Zone {
			scene = "Level1",
			ground = C(168, 132, 84), accent = C(122, 74, 44), rock = C(110, 104, 96),
			skyTint = C(214, 178, 120), ground_sky = C(120, 98, 70), fog = C(204, 176, 128), sun = C(255, 226, 180),
			fogDensity = 0.012f, sunIntensity = 0.9f,
			ambient = "Assets/ThirdParty/KenneyAudio/computerNoise_000.ogg",
			props = new Dictionary<string, string> {
				{ "Tree_1", "KenneyCityKitIndustrial/shipping-container-a" },
				{ "Tree_2", "KenneyCityKitIndustrial/detail-tank" },
				{ "Rock_1", "KenneySurvivalKit/barrel" },
				{ "Rock_5", "KenneySurvivalKit/box-large" },
				{ "Rock_6", "KenneySpaceStationKit/skip-rocks" },
				{ "Stone_1", "KenneySurvivalKit/metal-panel-screws" },
				{ "Log_2", "KenneySpaceStationKit/pipe" },
			},
		},
		new Zone {
			scene = "Level2",
			ground = C(112, 116, 120), accent = C(112, 66, 52), rock = C(84, 88, 94),
			skyTint = C(150, 165, 180), ground_sky = C(70, 74, 80), fog = C(150, 158, 166), sun = C(220, 230, 240),
			fogDensity = 0.014f, sunIntensity = 0.95f,
			ambient = "Assets/ThirdParty/KenneyAudio/spaceEngineLow_000.ogg",
			props = new Dictionary<string, string> {
				{ "Rock_1", "KenneyCityKitIndustrial/detail-tank-large" },
				{ "Rock_2", "KenneyCityKitIndustrial/shipping-container-b" },
				{ "Rock_3", "KenneyCityKitIndustrial/chimney-small" },
				{ "Rock_4", "KenneyCityKitIndustrial/shipping-container-c" },
				{ "Rock_5", "KenneyCityKitIndustrial/detail-tank" },
				{ "Stone_1", "KenneySurvivalKit/barrel" },
			},
		},
		new Zone {
			scene = "Level3",
			extraTerrain = new[] { "Mounting_1", "Mounting_2", "Mounting_3" },
			ground = C(86, 112, 58), accent = C(112, 100, 84), rock = C(120, 122, 116),
			skyTint = C(160, 190, 170), ground_sky = C(80, 96, 70), fog = C(170, 190, 168), sun = C(250, 244, 220),
			fogDensity = 0.010f, sunIntensity = 1.05f,
			ambient = null,
			props = new Dictionary<string, string> {
				{ "Stone_1", "KenneySurvivalKit/metal-panel-screws-half" },
				{ "Rock_4", "KenneySurvivalKit/structure-metal-wall" },
				{ "Rock_6", "KenneySpaceStationKit/skip-rocks" },
			},
		},
		new Zone {
			scene = "Level4",
			ground = C(78, 94, 106), accent = C(60, 70, 82), rock = C(108, 118, 126),
			skyTint = C(110, 140, 170), ground_sky = C(50, 60, 72), fog = C(120, 140, 160), sun = C(200, 220, 255),
			fogDensity = 0.012f, sunIntensity = 0.9f,
			ambient = "Assets/ThirdParty/KenneyAudio/spaceEngineLow_000.ogg",
			beacons = true,
			props = new Dictionary<string, string> {
				{ "Tree_1", "KenneySpaceStationKit/structure" },
				{ "Tree_2", "KenneyCityKitIndustrial/solar-panel-portrait" },
				{ "Tree_3", "KenneyCityKitIndustrial/chimney-basic" },
				{ "Bush_1", "KenneySpaceStationKit/container" },
				{ "Bush_2", "KenneySpaceStationKit/container-wide" },
				{ "Bush_3", "KenneySpaceStationKit/computer-system" },
				{ "Rock_2", "KenneySpaceStationKit/container-tall" },
				{ "Rock_5", "KenneySpaceStationKit/pipe-ring" },
				{ "Log_1", "KenneySpaceStationKit/pipe" },
			},
		},
	};

	[MenuItem("Tools/Robo Lac Loi/Dress All Zones")]
	public static void DressAll() {
		foreach (Zone zone in Zones)
			Dress(zone.scene);
		Debug.Log("ZoneDresser: done");
	}

	public static void Dress(string sceneName) {
		Zone zone = Zones.First(z => z.scene == sceneName);
		if (!AssetDatabase.IsValidFolder(ZoneMaterials.TrimEnd('/')))
			AssetDatabase.CreateFolder("Assets/ThirdParty/RoboLacLoi/Materials", "Zones");

		var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
		ColorTerrain(zone);
		SetSkyFogSun(zone);
		List<GameObject> swapped = SwapDecorations(zone, scene);
		AddAmbient(zone);
		if (zone.beacons)
			AddBeacons(swapped);
		EditorSceneManager.MarkSceneDirty(scene);
		EditorSceneManager.SaveScene(scene);
		BakeEnvironment(zone);
		Debug.Log("ZoneDresser: " + sceneName + " dressed, " + swapped.Count + " decorations swapped");
	}

	// The levels shared one LightingData asset baked from the old sky, so the new sky never reached the ambient light.
	// Bake each level's own ambient probe and reflection probe; no lightmaps (baked/realtime GI off) keeps it fast.
	static void BakeEnvironment(Zone zone) {
		LightingSettings settings = null;
		try {
			settings = Lightmapping.lightingSettings;
		} catch (System.Exception) {
		}
		if (settings == null) {
			settings = new LightingSettings();
			AssetDatabase.CreateAsset(settings, "Assets/Scenes/" + zone.scene + "Settings.lighting");
			Lightmapping.lightingSettings = settings;
		}
		settings.bakedGI = false;
		settings.realtimeGI = false;
		Lightmapping.lightingDataAsset = null;
		if (!Lightmapping.Bake())
			throw new System.Exception("ZoneDresser: lighting bake failed for " + zone.scene);
		EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
	}

	static Material ZoneMaterial(Zone zone, string role, Color color) {
		return AssetReplacer.EnsureMaterial(ZoneMaterials + zone.scene + "_" + role + ".mat", color, Color.black);
	}

	// The terrain prefab of each level ("Level1 Terrain") uses the shared color materials in Assets/Models/Materials.
	// Ground-like colors become the zone ground, browns the accent, everything else the rock color.
	static void ColorTerrain(Zone zone) {
		foreach (string prefab in new[] { zone.scene + " Terrain" }.Concat(zone.extraTerrain))
			ColorTerrain(zone, "Assets/Prefabs/" + prefab + ".prefab");
	}

	static void ColorTerrain(Zone zone, string path) {
		Material ground = ZoneMaterial(zone, "Ground", zone.ground);
		Material accent = ZoneMaterial(zone, "Accent", zone.accent);
		Material rock = ZoneMaterial(zone, "Rock", zone.rock);

		GameObject root = PrefabUtility.LoadPrefabContents(path);
		try {
			foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) {
				Material[] materials = renderer.sharedMaterials;
				for (int i = 0; i < materials.Length; i++) {
					string name = materials[i] != null ? materials[i].name : "";
					if (name.StartsWith(zone.scene + "_"))
						continue;
					if (name.StartsWith("Green") || name.StartsWith("Yellow") || name.StartsWith("Orange") || name.StartsWith("Pink"))
						materials[i] = ground;
					else if (name.StartsWith("Brown"))
						materials[i] = accent;
					else
						materials[i] = rock;
				}
				renderer.sharedMaterials = materials;
			}
			PrefabUtility.SaveAsPrefabAsset(root, path);
		} finally {
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	static void SetSkyFogSun(Zone zone) {
		string skyPath = ZoneMaterials + zone.scene + "_Sky.mat";
		Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
		if (sky == null) {
			sky = new Material(Shader.Find("Skybox/Procedural"));
			AssetDatabase.CreateAsset(sky, skyPath);
		}
		sky.SetColor("_SkyTint", zone.skyTint);
		sky.SetColor("_GroundColor", zone.ground_sky);
		sky.SetFloat("_AtmosphereThickness", 1.4f);
		sky.SetFloat("_Exposure", 0.9f);
		EditorUtility.SetDirty(sky);

		RenderSettings.skybox = sky;
		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
		// Full skybox ambient washes out the low-poly ground colors.
		RenderSettings.ambientIntensity = 0.75f;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.ExponentialSquared;
		RenderSettings.fogColor = zone.fog;
		RenderSettings.fogDensity = zone.fogDensity;

		// Some levels have a second directional light; keep it as a dim fill so the ground is not lit twice.
		Light[] suns = Object.FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional)
			.OrderByDescending(l => l.shadows != LightShadows.None).ToArray();
		for (int i = 0; i < suns.Length; i++) {
			suns[i].color = zone.sun;
			suns[i].intensity = i == 0 ? zone.sunIntensity : 0.25f;
		}
	}

	static GameObject LoadProp(string packAndModel) {
		string[] parts = packAndModel.Split('/');
		string path = ThirdParty + parts[0] + "/Models/" + parts[1] + ".fbx";
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
		if (model == null)
			throw new System.Exception("ZoneDresser: missing model " + path);
		return model;
	}

	/// <summary>
	/// Replaces the visual of one decoration with model, keeping position, rotation and footprint.
	/// </summary>
	public static void SwapDecoration(GameObject decoration, GameObject model) {
		if (PrefabUtility.IsPartOfPrefabInstance(decoration))
			PrefabUtility.UnpackPrefabInstance(decoration, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
		AssetReplacer.ReplaceVisual(decoration, model, false);
		decoration.name = model.name;
	}

	static List<GameObject> SwapDecorations(Zone zone, UnityEngine.SceneManagement.Scene scene) {
		var swapped = new List<GameObject>();
		var targets = new List<KeyValuePair<GameObject, string>>();
		foreach (GameObject root in scene.GetRootGameObjects()) {
			foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
				if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
					continue;
				GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
				string model;
				if (source != null && zone.props.TryGetValue(source.name, out model))
					targets.Add(new KeyValuePair<GameObject, string>(t.gameObject, model));
			}
		}
		foreach (var target in targets) {
			SwapDecoration(target.Key, LoadProp(target.Value));
			swapped.Add(target.Key);
		}
		return swapped;
	}

	static void AddAmbient(Zone zone) {
		GameManager manager = Object.FindObjectOfType<GameManager>();
		if (manager == null || zone.ambient == null)
			return;
		AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(zone.ambient);
		Transform existing = manager.transform.Find("Ambient");
		GameObject ambient = existing != null ? existing.gameObject : new GameObject("Ambient");
		ambient.transform.SetParent(manager.transform, false);
		// Unity's fake null breaks '??' on components, so check explicitly.
		AudioSource source = ambient.GetComponent<AudioSource>();
		if (source == null)
			source = ambient.AddComponent<AudioSource>();
		source.clip = clip;
		source.loop = true;
		source.playOnAwake = true;
		source.volume = 0.15f;
		source.spatialBlend = 0f;
	}

	static void AddBeacons(List<GameObject> decorations) {
		GameManager manager = Object.FindObjectOfType<GameManager>();
		WinBeacons beacons = manager.GetComponent<WinBeacons>();
		if (beacons == null)
			beacons = manager.gameObject.AddComponent<WinBeacons>();
		var lights = new List<Light>();
		foreach (GameObject decoration in decorations.Take(8)) {
			GameObject go = new GameObject("Beacon Light");
			go.transform.SetParent(decoration.transform, false);
			go.transform.localPosition = Vector3.up * 2f;
			Light light = go.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = new Color32(46, 230, 230, 255);
			light.range = 10f;
			light.intensity = 2.5f;
			lights.Add(light);
		}
		beacons.lights = lights.ToArray();
	}
}
