using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overdrive (spec §9): builds Assets/Prefabs/Overdrive.prefab (static trigger root, spinning bolt child), adds the
/// OverdriveDirector to Level2+ (removed from Level1) and an "Overdrive Indicator" (bolt icon + seconds) to every
/// level's MainCanvas. Re-runnable. Run UiTheme.Apply afterwards.
/// </summary>
public static class OverdriveSetup {

	const string PrefabPath = "Assets/Prefabs/Overdrive.prefab";
	const string ModelPath = "Assets/ThirdParty/RoboLacLoi/Models/Overdrive.fbx";
	const string IconPath = "Assets/ThirdParty/RoboLacLoi/Sprites/overdrive_icon.png";
	public const float BoltHeight = 0.7f;

	[MenuItem("Tools/Robo Lac Loi/Apply Overdrive Setup")]
	public static void Apply() {
		TextureImporter icon = (TextureImporter)AssetImporter.GetAtPath(IconPath);
		if (icon.textureType != TextureImporterType.Sprite) {
			icon.textureType = TextureImporterType.Sprite;
			icon.alphaIsTransparency = true;
			icon.SaveAndReimport();
		}
		GameObject prefab = BuildPrefab();
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
		foreach (LevelConfig level in LevelCatalog.All) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			GameManager manager = Object.FindObjectOfType<GameManager>();
			OverdriveDirector director = manager.GetComponent<OverdriveDirector>();
			if (level.order >= 2) {
				if (director == null)
					director = manager.gameObject.AddComponent<OverdriveDirector>();
				director.pickupPrefab = prefab;
			} else if (director != null) {
				Object.DestroyImmediate(director);
			}
			Indicator(sprite);
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
		}
		Debug.Log("OverdriveSetup: done");
	}

	static GameObject BuildPrefab() {
		GameObject root = new GameObject("Overdrive");
		try {
			SphereCollider trigger = root.AddComponent<SphereCollider>();
			trigger.isTrigger = true;
			trigger.radius = 0.6f;
			root.AddComponent<OverdrivePickup>();
			GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
			model.name = "Model";
			Bounds b = model.GetComponentInChildren<Renderer>().bounds;
			model.transform.localScale *= BoltHeight / b.size.y;
			b = model.GetComponentInChildren<Renderer>().bounds;
			model.transform.localPosition -= b.center;
			model.AddComponent<EnergyBob>();
			return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
		} finally {
			Object.DestroyImmediate(root);
		}
	}

	// Top centre of the HUD, under the score bar: bolt icon and the seconds left; hidden until Overdrive runs.
	static void Indicator(Sprite sprite) {
		GameObject canvas = GameObject.Find("MainCanvas");
		Transform old = canvas.transform.Find("Overdrive Indicator");
		if (old != null)
			Object.DestroyImmediate(old.gameObject);
		GameObject go = new GameObject("Overdrive Indicator", typeof(RectTransform));
		go.transform.SetParent(canvas.transform, false);
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = new Vector2(0f, -150f);
		rect.sizeDelta = new Vector2(200f, 80f);
		OverdriveIndicator indicator = go.AddComponent<OverdriveIndicator>();

		GameObject content = new GameObject("Content", typeof(RectTransform));
		content.transform.SetParent(go.transform, false);
		RectTransform contentRect = (RectTransform)content.transform;
		contentRect.anchorMin = Vector2.zero;
		contentRect.anchorMax = Vector2.one;
		contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

		GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
		iconGo.transform.SetParent(content.transform, false);
		RectTransform iconRect = (RectTransform)iconGo.transform;
		iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
		iconRect.sizeDelta = new Vector2(80f, 80f);
		Image image = iconGo.GetComponent<Image>();
		image.sprite = sprite;
		image.raycastTarget = false;

		GameObject textGo = new GameObject("Seconds", typeof(RectTransform), typeof(Text));
		textGo.transform.SetParent(content.transform, false);
		RectTransform textRect = (RectTransform)textGo.transform;
		textRect.anchorMin = new Vector2(0f, 0f);
		textRect.anchorMax = new Vector2(1f, 1f);
		textRect.offsetMin = new Vector2(90f, 0f);
		textRect.offsetMax = Vector2.zero;
		Text text = textGo.GetComponent<Text>();
		text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ChakraPetch/ChakraPetch-Bold.ttf");
		text.fontSize = 56;
		text.alignment = TextAnchor.MiddleLeft;
		text.color = new Color32(255, 214, 40, 255);
		text.text = "5";
		text.raycastTarget = false;

		indicator.content = content;
		indicator.seconds = text;
		content.SetActive(false);
	}
}
