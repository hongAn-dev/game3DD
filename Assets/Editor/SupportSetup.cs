using UnityEditor;
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
		BuildPickup("Assets/Prefabs/Support Shield.prefab", Models + "SupportShield.fbx", SupportKind.Shield, ItemHeight);
		BuildPickup("Assets/Prefabs/Support Heal10.prefab", Models + "SupportHeal10.fbx", SupportKind.Heal10, ItemHeight);
		BuildPickup("Assets/Prefabs/Support Heal20.prefab", Models + "SupportHeal20.fbx", SupportKind.Heal20, ItemHeight);
		PlayerShield();
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
