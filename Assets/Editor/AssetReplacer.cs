using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Replaces the original nature and monster models with the Kenney Nature Kit and
/// Quaternius Ultimate Monsters models. Prefab and scene references are kept: only the
/// visual part of each prefab is swapped, scaled to the size of the old model.
/// Run from the menu (Tools > Replace Assets) or in batch mode with
/// -executeMethod AssetReplacer.ReplaceAll
/// </summary>
public static class AssetReplacer {

	const string KenneyModels = "Assets/ThirdParty/KenneyNatureKit/Models/";
	const string MonsterModel = "Assets/ThirdParty/QuaterniusUltimateMonsters/BlueDemon.fbx";
	const string MonsterPrefab = "Assets/Prefabs/Enemy - Monster.prefab";
	const string ModelChildName = "Model";

	// Prefab name -> Kenney model name.
	static readonly Dictionary<string, string> NatureMap = new Dictionary<string, string> {
		{ "Tree_1", "tree_default" },
		{ "Tree_2", "tree_oak" },
		{ "Tree_3", "tree_pineDefaultA" },
		{ "Leaves_2", "plant_bushLargeTriangle" },
		{ "Bush_1", "plant_bush" },
		{ "Bush_2", "plant_bushLarge" },
		{ "Bush_3", "plant_bushDetailed" },
		{ "Grass_1", "grass" },
		{ "Grass_2", "grass_large" },
		{ "Log_1", "log" },
		{ "Log_2", "log_large" },
		{ "Log_3", "log_stack" },
		{ "Plant_1", "flower_redA" },
		{ "Plant_2", "flower_yellowA" },
		{ "Plant_3", "flower_purpleA" },
		{ "Plant_4", "plant_flatTall" },
		{ "Plant_5", "mushroom_red" },
		{ "Plant_6", "mushroom_tanGroup" },
		{ "Plant_7", "plant_flatShort" },
		{ "Rock_1", "rock_largeA" },
		{ "Rock_2", "rock_largeB" },
		{ "Rock_3", "rock_largeC" },
		{ "Rock_4", "rock_largeD" },
		{ "Rock_5", "rock_largeE" },
		{ "Rock_6", "rock_largeF" },
		{ "Stone_1", "stone_largeA" },
	};

	// Old model file -> Kenney model name, for scene objects that use the old meshes directly.
	static readonly Dictionary<string, string> SceneModelMap = new Dictionary<string, string> {
		{ "Assets/Models/Tree_2.fbx", "tree_oak" },
		{ "Assets/Models/Tree_3.fbx", "tree_pineDefaultA" },
	};

	[MenuItem("Tools/Replace Assets (Kenney + Quaternius)")]
	public static void ReplaceAll() {
		AssetDatabase.Refresh();
		ReplaceNaturePrefabs();
		ReplaceMonsterPrefab();
		ReplaceInScenes();
		AssetDatabase.SaveAssets();
		Debug.Log("AssetReplacer: done");
	}

	static GameObject LoadKenney(string name) {
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyModels + name + ".fbx");
		if (model == null)
			throw new System.Exception("AssetReplacer: missing model " + name);
		return model;
	}

	static void ReplaceNaturePrefabs() {
		foreach (KeyValuePair<string, string> pair in NatureMap) {
			string path = "Assets/Prefabs/" + pair.Key + ".prefab";
			GameObject root = PrefabUtility.LoadPrefabContents(path);
			try {
				if (root.transform.Find(ModelChildName) != null) {
					Debug.Log("AssetReplacer: " + path + " already replaced, skipping");
					continue;
				}
				ReplaceVisual(root, LoadKenney(pair.Value));
				PrefabUtility.SaveAsPrefabAsset(root, path);
				Debug.Log("AssetReplacer: " + path + " -> " + pair.Value);
			} finally {
				PrefabUtility.UnloadPrefabContents(root);
			}
		}
	}

	static void ReplaceMonsterPrefab() {
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterModel);
		if (model == null)
			throw new System.Exception("AssetReplacer: missing model " + MonsterModel);

		GameObject root = PrefabUtility.LoadPrefabContents(MonsterPrefab);
		try {
			if (root.transform.Find(ModelChildName) != null) {
				Debug.Log("AssetReplacer: " + MonsterPrefab + " already replaced, skipping");
				return;
			}

			// The old Animation component holds the StoneMonster clips; it is replaced by one on the model.
			Animation oldAnimation = root.GetComponent<Animation>();
			if (oldAnimation != null)
				Object.DestroyImmediate(oldAnimation);

			GameObject instance = ReplaceVisual(root, model);

			AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(MonsterModel)
				.OfType<AnimationClip>()
				.Where(c => !c.name.StartsWith("__preview__"))
				.ToArray();
			AnimationClip run = clips.FirstOrDefault(c => c.name == "Run") ?? clips.FirstOrDefault(c => c.name == "Walk");
			AnimationClip attack = clips.FirstOrDefault(c => c.name == "Punch") ?? clips.FirstOrDefault(c => c.name == "Weapon");

			Animation animation = instance.GetComponent<Animation>();
			if (animation == null)
				animation = instance.AddComponent<Animation>();
			AnimationUtility.SetAnimationClips(animation, clips);
			animation.clip = run;
			animation.playAutomatically = true;

			MonsterChaser chaser = root.GetComponent<MonsterChaser>();
			if (chaser != null) {
				chaser.runClip = run != null ? run.name : "";
				chaser.attackClip = attack != null ? attack.name : "";
			}

			PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefab);
			Debug.Log("AssetReplacer: " + MonsterPrefab + " -> " + MonsterModel + " (" +
				string.Join(", ", clips.Select(c => c.name).ToArray()) + ")");
		} finally {
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	static void ReplaceInScenes() {
		foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes) {
			var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
			bool changed = false;

			foreach (GameObject sceneRoot in scene.GetRootGameObjects()) {
				foreach (MeshFilter filter in sceneRoot.GetComponentsInChildren<MeshFilter>(true)) {
					if (filter == null || filter.sharedMesh == null)
						continue;
					string modelPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
					string kenney;
					if (!SceneModelMap.TryGetValue(modelPath, out kenney))
						continue;
					// Objects inside prefab instances are handled by the prefab replacement.
					if (PrefabUtility.IsPartOfPrefabInstance(filter))
						continue;

					ReplaceVisual(filter.gameObject, LoadKenney(kenney));
					Debug.Log("AssetReplacer: " + scene.path + " " + filter.name + " -> " + kenney);
					changed = true;
				}
			}

			if (changed)
				EditorSceneManager.SaveScene(scene);
		}
	}

	/// <summary>
	/// Removes the renderers of root (and the children holding them) and adds the new model as a
	/// child named "Model", scaled and positioned to fit the old model bounds. Mesh colliders are
	/// recreated on the new meshes.
	/// </summary>
	static GameObject ReplaceVisual(GameObject root, GameObject model) {
		Bounds oldBounds;
		bool hasOldBounds = TryGetLocalBounds(root, out oldBounds);
		bool hadMeshCollider = root.GetComponentsInChildren<MeshCollider>(true).Length > 0;

		// Remove the old visual: components on the root and every child that holds a renderer.
		foreach (Transform child in root.transform.Cast<Transform>().ToList()) {
			if (child.GetComponentsInChildren<Renderer>(true).Length > 0)
				Object.DestroyImmediate(child.gameObject);
		}
		foreach (MeshCollider collider in root.GetComponents<MeshCollider>())
			Object.DestroyImmediate(collider);
		foreach (Renderer renderer in root.GetComponents<Renderer>())
			Object.DestroyImmediate(renderer);
		foreach (MeshFilter filter in root.GetComponents<MeshFilter>())
			Object.DestroyImmediate(filter);

		GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
		instance.name = ModelChildName;
		instance.transform.localPosition = Vector3.zero;
		instance.layer = root.layer;

		Bounds newBounds;
		if (hasOldBounds && TryGetLocalBounds(root, out newBounds)) {
			// Uniform scale so the largest dimension matches, then sit on the old bottom center.
			float oldSize = Mathf.Max(oldBounds.size.x, oldBounds.size.y, oldBounds.size.z);
			float newSize = Mathf.Max(newBounds.size.x, newBounds.size.y, newBounds.size.z);
			float scale = newSize > 0f ? oldSize / newSize : 1f;

			Vector3 oldBottom = new Vector3(oldBounds.center.x, oldBounds.min.y, oldBounds.center.z);
			Vector3 newBottom = new Vector3(newBounds.center.x, newBounds.min.y, newBounds.center.z);
			instance.transform.localScale = instance.transform.localScale * scale;
			instance.transform.localPosition = oldBottom - newBottom * scale;
		}

		if (hadMeshCollider) {
			foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
				filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
		}
		return instance;
	}

	/// <summary>
	/// Bounds of all meshes under root, in root local space.
	/// </summary>
	static bool TryGetLocalBounds(GameObject root, out Bounds bounds) {
		bounds = new Bounds();
		bool found = false;
		Matrix4x4 toRoot = root.transform.worldToLocalMatrix;

		foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) {
			Mesh mesh = null;
			Transform space = renderer.transform;
			SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
			if (skinned != null) {
				mesh = skinned.sharedMesh;
				// Skinned mesh vertices are in the bind pose, relative to the renderer transform.
			} else {
				MeshFilter filter = renderer.GetComponent<MeshFilter>();
				if (filter != null)
					mesh = filter.sharedMesh;
			}
			if (mesh == null)
				continue;

			Matrix4x4 matrix = toRoot * space.localToWorldMatrix;
			Bounds meshBounds = mesh.bounds;
			for (int i = 0; i < 8; i++) {
				Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
					new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
				Vector3 point = matrix.MultiplyPoint3x4(corner);
				if (!found) {
					bounds = new Bounds(point, Vector3.zero);
					found = true;
				} else {
					bounds.Encapsulate(point);
				}
			}
		}
		return found;
	}
}
