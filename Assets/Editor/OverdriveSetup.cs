using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overdrive (spec §9): builds Assets/Prefabs/Overdrive.prefab (static trigger root, spinning bolt child), adds the
/// OverdriveDirector to Level2+ (removed from Level1) and an "Overdrive Indicator" (bolt icon + seconds) to every
/// level's energy panel. Re-runnable. Run UiTheme.Apply first (it creates the panel's "Zone Text").
/// </summary>
public static class OverdriveSetup {

	const string PrefabPath = "Assets/Prefabs/Overdrive.prefab";
	const string ModelPath = "Assets/ThirdParty/RoboLacLoi/Models/Overdrive.fbx";
	const string IconPath = "Assets/ThirdParty/RoboLacLoi/Sprites/overdrive_icon.png";
	public const float BoltHeight = 0.6f;   // same size as the other pickups (spec §6.4)

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
		return SupportSetup.BuildPickup(PrefabPath, ModelPath, SupportKind.Overdrive, BoltHeight);
	}

	// Under the energy panel at the top right (below the zone name): bolt icon and the seconds left; hidden until
	// Overdrive runs.
	static void Indicator(Sprite sprite) {
		Transform panel = GameObject.Find("Zone Text").transform.parent;
		foreach (OverdriveIndicator old in Object.FindObjectsOfType<OverdriveIndicator>(true))
			Object.DestroyImmediate(old.gameObject);
		GameObject go = new GameObject("Overdrive Indicator", typeof(RectTransform));
		go.transform.SetParent(panel, false);
		RectTransform rect = (RectTransform)go.transform;
		rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
		rect.pivot = new Vector2(1f, 1f);
		rect.anchoredPosition = new Vector2(-4f, -34f);
		rect.sizeDelta = new Vector2(110f, 44f);
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
		iconRect.sizeDelta = new Vector2(44f, 44f);
		Image image = iconGo.GetComponent<Image>();
		image.sprite = sprite;
		image.raycastTarget = false;

		GameObject textGo = new GameObject("Seconds", typeof(RectTransform), typeof(Text));
		textGo.transform.SetParent(content.transform, false);
		RectTransform textRect = (RectTransform)textGo.transform;
		textRect.anchorMin = new Vector2(0f, 0f);
		textRect.anchorMax = new Vector2(1f, 1f);
		textRect.offsetMin = new Vector2(50f, 0f);
		textRect.offsetMax = Vector2.zero;
		Text text = textGo.GetComponent<Text>();
		text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ChakraPetch/ChakraPetch-Bold.ttf");
		text.fontSize = 34;
		text.alignment = TextAnchor.MiddleLeft;
		text.color = new Color32(255, 214, 40, 255);
		text.text = "5";
		text.raycastTarget = false;

		indicator.content = content;
		indicator.seconds = text;
		content.SetActive(false);
	}
}
