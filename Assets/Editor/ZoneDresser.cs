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
		Debug.Log("ZoneDresser: " + sceneName + " dressed, " + swapped.Count + " decorations swapped");
	}

	static Material ZoneMaterial(Zone zone, string role, Color color) {
		return AssetReplacer.EnsureMaterial(ZoneMaterials + zone.scene + "_" + role + ".mat", color, Color.black);
	}

	// The terrain prefab of each level ("Level1 Terrain") uses the shared color materials in Assets/Models/Materials.
	// Ground-like colors become the zone ground, browns the accent, everything else the rock color.
	static void ColorTerrain(Zone zone) {
		string path = "Assets/Prefabs/" + zone.scene + " Terrain.prefab";
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
		sky.SetFloat("_Exposure", 1.1f);
		EditorUtility.SetDirty(sky);

		RenderSettings.skybox = sky;
		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.ExponentialSquared;
		RenderSettings.fogColor = zone.fog;
		RenderSettings.fogDensity = zone.fogDensity;

		foreach (Light light in Object.FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional)) {
			light.color = zone.sun;
			light.intensity = zone.sunIntensity;
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
