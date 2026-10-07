using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class ThemeEditorTests {

	static GameObject Model() {
		return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyNatureKit/Models/stone_largeA.fbx");
	}

	[Test]
	public void ReplaceVisualKeepsTrailRenderer() {
		GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		root.AddComponent<TrailRenderer>();

		AssetReplacer.ReplaceVisual(root, Model(), false);

		Assert.IsNotNull(root.GetComponent<TrailRenderer>());
		Assert.IsNull(root.GetComponent<MeshRenderer>());
		Object.DestroyImmediate(root);
	}

	[Test]
	public void ReplaceVisualKeepsConvexFlag() {
		GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
		Object.DestroyImmediate(root.GetComponent<BoxCollider>());
		root.AddComponent<MeshCollider>().convex = true;

		GameObject model = AssetReplacer.ReplaceVisual(root, Model(), false);

		MeshCollider[] colliders = model.GetComponentsInChildren<MeshCollider>();
		Assert.IsNotEmpty(colliders);
		foreach (MeshCollider collider in colliders)
			Assert.IsTrue(collider.convex);
		Object.DestroyImmediate(root);
	}

	static Bounds RendererBounds(GameObject root) {
		Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
		foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
			bounds.Encapsulate(r.bounds);
		return bounds;
	}

	[Test]
	public void DecorationFootprintNotLarger() {
		// A long log replaced by a round barrel: the barrel must not grow past the log's narrow side.
		GameObject log = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyNatureKit/Models/log_large.fbx");
		GameObject barrel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneySurvivalKit/Models/barrel.fbx");
		GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(log);
		Bounds before = RendererBounds(root);

		ZoneDresser.SwapDecoration(root, barrel, 1f);

		Bounds after = RendererBounds(root);
		Assert.LessOrEqual(after.size.x, before.size.x + 0.01f);
		Assert.LessOrEqual(after.size.z, before.size.z + 0.01f);
		Object.DestroyImmediate(root);
	}

	[Test]
	public void TreeSwapUsesTrunkFootprint() {
		// Trees only block at the trunk; a solid prop in their place must be much smaller than the canopy.
		GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
		root.transform.localScale = new Vector3(4, 4, 4);
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyCityKitIndustrial/Models/shipping-container-a.fbx");

		ZoneDresser.SwapDecoration(root, model, ZoneDresser.TreeFootprint);

		Bounds after = RendererBounds(root);
		Assert.LessOrEqual(Mathf.Max(after.size.x, after.size.z), 4f * ZoneDresser.TreeFootprint + 0.01f);
		Object.DestroyImmediate(root);
	}

	[Test]
	public void EveryLevelHasAZone() {
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" })
			Assert.IsTrue(ZoneDresser.Zones.Any(z => z.scene == level), level);
	}

	[Test]
	public void CoinGroupsUseEnergyCores() {
		foreach (string name in new[] { "Sil Coins", "Vin Coins" }) {
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
			foreach (Treasure treasure in prefab.GetComponentsInChildren<Treasure>(true))
				Assert.AreEqual("EnergyCore", treasure.GetComponentInChildren<MeshFilter>().sharedMesh.name, name + "/" + treasure.name);
		}
	}

	[Test]
	public void EachLevelHasItsOwnLightingData() {
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" }) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			Assert.IsNotNull(Lightmapping.lightingDataAsset, level);
			StringAssert.Contains("/" + level + "/", AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset), level);
		}
	}

	[Test]
	public void LevelsHaveOneMainSun() {
		foreach (string level in new[] { "Level1", "Level2", "Level3", "Level4" }) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			float total = Object.FindObjectsOfType<Light>()
				.Where(l => l.type == LightType.Directional && l.enabled)
				.Sum(l => l.intensity);
			Assert.LessOrEqual(total, 1.5f, level);
		}
	}

	[Test]
	public void ExplosionPrefabsUseThemeSounds() {
		var expected = new System.Collections.Generic.Dictionary<string, string> {
			{ "ExplodeCoin Particle", "forceField_000" },
			{ "ExplodeEnemy Particle", "explosionCrunch_000" },
			{ "ExplodePlayer Particle", "lowFrequency_explosion_000" },
		};
		foreach (var pair in expected) {
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + pair.Key + ".prefab");
			Assert.AreEqual(pair.Value, prefab.GetComponentInChildren<AudioSource>().clip.name, pair.Key);
		}
	}

	[Test]
	public void AllUiTextsUseOpenSans() {
		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })) {
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
			foreach (UnityEngine.UI.Text text in prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true))
				StringAssert.StartsWith("OpenSans", text.font.name, prefab.name + "/" + text.name);
		}
	}

	// Each line fits the rect without wrapping and all lines fit its height (at the best-fit size if enabled).
	static void AssertFits(Text text) {
		Vector2 size = text.rectTransform.rect.size;
		TextGenerationSettings settings = text.GetGenerationSettings(size);
		// Best fit would wrap long lines instead of shrinking them, so check the worst case: the max size.
		if (settings.resizeTextForBestFit) {
			settings.fontSize = text.resizeTextMaxSize;
			settings.resizeTextForBestFit = false;
		}
		settings.horizontalOverflow = HorizontalWrapMode.Overflow;
		settings.verticalOverflow = VerticalWrapMode.Overflow;
		TextGenerator generator = new TextGenerator();
		float width = generator.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit;
		float height = generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
		Assert.LessOrEqual(width, size.x + 1f, text.name + " width: " + text.text);
		Assert.LessOrEqual(height, size.y + 1f, text.name + " height: " + text.text);
	}

	[Test]
	public void IntroCardFitsLongestZoneAndGoal() {
		GameObject prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/IntroBeatLevelCanvas.prefab");
		try {
			Text intro = prefab.GetComponentsInChildren<Text>(true).First(t => t.name == "Intro Level Text");
			intro.text = Zones.Title("Level2").ToUpperInvariant() + "\nTHU 100 LÕI NĂNG LƯỢNG";
			AssertFits(intro);
		} finally {
			PrefabUtility.UnloadPrefabContents(prefab);
		}
	}

	[Test]
	public void FinalScreenTextsFitAndDoNotOverlap() {
		EditorSceneManager.OpenScene("Assets/Scenes/Level4.unity", OpenSceneMode.Single);
		Text[] texts = Object.FindObjectsOfType<Text>(true)
			.Where(t => t.text.StartsWith("ĐÃ VỀ") || t.text.StartsWith("Cảm ơn")).ToArray();
		Assert.AreEqual(2, texts.Length);
		foreach (Text text in texts)
			AssertFits(text);
		Rect a = WorldRect(texts[0].rectTransform), b = WorldRect(texts[1].rectTransform);
		Assert.IsFalse(a.Overlaps(b), "final screen texts overlap");
	}

	static Rect WorldRect(RectTransform rect) {
		Vector3[] corners = new Vector3[4];
		rect.GetWorldCorners(corners);
		return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
	}

	[Test]
	public void SelectedButtonsStandOut() {
		EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
		Button[] buttons = Object.FindObjectsOfType<Button>(true);
		Assert.IsNotEmpty(buttons);
		foreach (Button button in buttons) {
			Color baseColor = button.GetComponent<Image>().color;
			Color normal = baseColor * button.colors.normalColor;
			Color selected = baseColor * button.colors.selectedColor;
			float diff = Mathf.Max(Mathf.Abs(normal.r - selected.r), Mathf.Abs(normal.g - selected.g), Mathf.Abs(normal.b - selected.b));
			Assert.GreaterOrEqual(diff, 0.15f, button.name);
		}
	}

	[Test]
	public void BaseStationHasWinBeacons() {
		EditorSceneManager.OpenScene("Assets/Scenes/Level4.unity", OpenSceneMode.Single);
		WinBeacons beacons = Object.FindObjectOfType<WinBeacons>();
		Assert.IsNotNull(beacons);
		Assert.IsNotEmpty(beacons.lights);
		Assert.IsTrue(beacons.lights.All(l => l != null));
		Assert.AreEqual(beacons.lights.Length, Object.FindObjectsOfType<Light>().Count(l => l.name == "Beacon Light"));
	}

	static float Luminance(Color c) {
		return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
	}

	[Test]
	public void PanelTextIsReadableOnItsBox() {
		foreach (string name in new[] { "IntroBeatLevelCanvas", "BeatLevelUICanvas", "GameOver Canvas" }) {
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
			Image box = prefab.GetComponentsInChildren<Image>(true).First(i => i.name == "BoxBackground");
			foreach (Text text in prefab.GetComponentsInChildren<Text>(true).Where(t => t.GetComponentInParent<Button>() == null))
				Assert.GreaterOrEqual(Mathf.Abs(Luminance(text.color) - Luminance(box.color)), 0.4f, name + "/" + text.name);
		}
	}
}
