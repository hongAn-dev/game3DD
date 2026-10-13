using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Guide pictures from the real game assets (playtest: the guide used drawn symbols and showed the robot and the
/// monsters with the same picture). Renders the prefabs on a transparent background into
/// Resources/GuideSprites/{key}.png, imported as sprites. hp_minus/slow_icon stay the HUD status icons they show.
/// Re-run after a model or prefab changes (ChallengeBuildPipeline does).
/// </summary>
public static class GuideIconSetup {

	const string Folder = "Assets/Resources/GuideSprites/";
	const int Size = 512;

	static readonly Dictionary<string, string[]> Shots = new Dictionary<string, string[]> {
		{ "robot_icon", new[] { "Assets/Prefabs/Player.prefab" } },
		{ "core_icon", new[] { "Assets/Prefabs/Coin.prefab" } },
		{ "shield_icon", new[] { "Assets/Prefabs/Support Shield.prefab" } },
		{ "hp_plus", new[] { "Assets/Prefabs/Support Heal10.prefab", "Assets/Prefabs/Support Heal20.prefab" } },
		{ "support_items", new[] { "Assets/Prefabs/Support Heal10.prefab", "Assets/Prefabs/Support Heal20.prefab", "Assets/Prefabs/Support Shield.prefab" } },
		{ "overdrive_icon", new[] { "Assets/Prefabs/Overdrive.prefab" } },
		{ "trap_poison", new[] { "Assets/Prefabs/Harmful/Poison.prefab" } },
		{ "trap_anti_energy", new[] { "Assets/Prefabs/Harmful/AntiEnergy.prefab" } },
		{ "trap_magnetic", new[] { "Assets/Prefabs/Harmful/MagneticInterference.prefab" } },
		{ "harmful_items", new[] { "Assets/Prefabs/Harmful/Poison.prefab", "Assets/Prefabs/Harmful/AntiEnergy.prefab", "Assets/Prefabs/Harmful/MagneticInterference.prefab" } },
		{ "boss_icon", new[] { "Assets/Prefabs/Enemy - Monster.prefab" } },
		{ "monsters", new[] { "Assets/Prefabs/Enemies/Creep GreenBlob.prefab", "Assets/Prefabs/Enemy - Crater.prefab", "Assets/Prefabs/Enemy - Monster.prefab" } },
	};

	// Size (largest of width/height) of each item in a picture, relative to the others: icons read better at one size, except the boss,
	// which is drawn bigger than the creeps as in the game.
	static readonly Dictionary<string, float> Heights = new Dictionary<string, float> {
		{ "Assets/Prefabs/Enemy - Monster.prefab", 1f },
		{ "Assets/Prefabs/Enemies/Creep GreenBlob.prefab", 0.75f },
		{ "Assets/Prefabs/Enemy - Crater.prefab", 0.7f },
	};

	// Crops the transparent margin (keeps 8 px) so the dialog shows the items as large as its picture area allows.
	static Texture2D Trim(Texture2D tex) {
		Color32[] px = tex.GetPixels32();
		int minX = Size, minY = Size, maxX = -1, maxY = -1;
		for (int y = 0; y < Size; y++)
			for (int x = 0; x < Size; x++)
				if (px[y * Size + x].a > 8) {
					minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
					minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
				}
		if (maxX < 0)
			return tex;
		minX = Mathf.Max(0, minX - 8); minY = Mathf.Max(0, minY - 8);
		maxX = Mathf.Min(Size - 1, maxX + 8); maxY = Mathf.Min(Size - 1, maxY + 8);
		var cropped = new Texture2D(maxX - minX + 1, maxY - minY + 1, TextureFormat.RGBA32, false);
		cropped.SetPixels(tex.GetPixels(minX, minY, cropped.width, cropped.height));
		cropped.Apply();
		return cropped;
	}

	[MenuItem("Tools/Robo Lac Loi/Render Guide Pictures")]
	public static void Apply() {
		EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
		Light sun = new GameObject("Sun").AddComponent<Light>();
		sun.type = LightType.Directional;
		sun.intensity = 1.1f;
		sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
		RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
		Camera cam = new GameObject("Guide Camera").AddComponent<Camera>();
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		cam.fieldOfView = 26f;
		var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
		cam.targetTexture = target;

		foreach (var shot in Shots) {
			var items = new List<GameObject>();
			// Items sit in a near-square grid (2×2 for three) so the picture stays large in the dialog.
			int columns = Mathf.CeilToInt(Mathf.Sqrt(shot.Value.Length)), index = 0;
			foreach (string path in shot.Value) {
				GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (prefab == null) {
					Debug.LogWarning("GuideIconSetup: missing " + path);
					continue;
				}
				GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
				go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 200f, 0f));   // three-quarter view
				foreach (Canvas c in go.GetComponentsInChildren<Canvas>(true)) c.gameObject.SetActive(false);
				foreach (ParticleSystem p in go.GetComponentsInChildren<ParticleSystem>(true)) p.gameObject.SetActive(false);
				// The ground ring's material only exists at runtime (it renders magenta here).
				foreach (Transform t in go.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Ground Ring").ToList()) t.gameObject.SetActive(false);
				Bounds b = EnemySetup.MeshBounds(go);
				float height;
				go.transform.localScale *= (Heights.TryGetValue(path, out height) ? height : 1f) / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y));
				b = EnemySetup.MeshBounds(go);
				const float cell = 1.3f;   // items are normalised to at most 1 unit
				int col = index % columns, row = index / columns;
				int inRow = Mathf.Min(columns, shot.Value.Length - row * columns);
				float cx = (col - (inRow - 1) * 0.5f) * cell;   // a short last row is centred
				go.transform.position += new Vector3(cx - b.center.x, -row * cell - b.min.y, -b.center.z);
				index++;
				items.Add(go);
			}
			if (items.Count == 0)
				continue;
			Bounds all = EnemySetup.MeshBounds(items[0]);
			foreach (GameObject go in items.Skip(1))
				all.Encapsulate(EnemySetup.MeshBounds(go));
			float extent = Mathf.Max(all.extents.x, all.extents.y) * 1.15f;
			float distance = extent / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) + all.extents.z;
			cam.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
			cam.transform.position = all.center - cam.transform.forward * distance;
			cam.Render();
			RenderTexture.active = target;
			var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
			tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
			tex.Apply();
			RenderTexture.active = null;
			string file = Folder + shot.Key + ".png";
			File.WriteAllBytes(file, Trim(tex).EncodeToPNG());
			Object.DestroyImmediate(tex);
			foreach (GameObject go in items)
				Object.DestroyImmediate(go);
			AssetDatabase.ImportAsset(file);
			TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(file);
			importer.textureType = TextureImporterType.Sprite;
			importer.alphaIsTransparency = true;
			importer.mipmapEnabled = false;
			importer.SaveAndReimport();
		}
		cam.targetTexture = null;
		Object.DestroyImmediate(target);
		Debug.Log("GuideIconSetup: done");
	}
}
