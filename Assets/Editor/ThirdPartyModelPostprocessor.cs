using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the third party model packs (Kenney kits, Quaternius characters).
/// </summary>
public class ThirdPartyModelPostprocessor : AssetPostprocessor {

	static readonly string[] PropFolders = {
		"Assets/ThirdParty/KenneyNatureKit/",
		"Assets/ThirdParty/KenneyCityKitIndustrial/",
		"Assets/ThirdParty/KenneySurvivalKit/",
		"Assets/ThirdParty/KenneySpaceStationKit/",
	};
	static readonly string[] CharacterFolders = {
		"Assets/ThirdParty/QuaterniusUltimateMonsters/",
		"Assets/ThirdParty/QuaterniusRobotEnemy/",
	};

	static bool In(string path, string[] folders) {
		foreach (string folder in folders)
			if (path.StartsWith(folder))
				return true;
		return false;
	}

	void OnPreprocessModel() {
		ModelImporter importer = (ModelImporter)assetImporter;

		if (In(assetPath, PropFolders)) {
			// Static props: readable meshes so they can be used by mesh colliders.
			importer.isReadable = true;
			importer.animationType = ModelImporterAnimationType.None;
			importer.importAnimation = false;
		} else if (In(assetPath, CharacterFolders)) {
			// The game drives monsters with the legacy Animation component (see EnemyBrain).
			importer.animationType = ModelImporterAnimationType.Legacy;
			importer.importAnimation = true;
		}
	}

	void OnPreprocessAnimation() {
		if (!In(assetPath, CharacterFolders))
			return;

		// Rename "CharacterArmature|Run_CharacterArmature" to "Run" and set the wrap modes.
		ModelImporter importer = (ModelImporter)assetImporter;
		ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
		foreach (ModelImporterClipAnimation clip in clips) {
			string name = clip.name;
			int bar = name.LastIndexOf('|');
			if (bar >= 0)
				name = name.Substring(bar + 1);
			name = name.Replace("_CharacterArmature", "");
			clip.name = name;

			bool loop = name == "Idle" || name == "Walk" || name.StartsWith("Run") || name == "Jump_Idle";
			clip.loopTime = loop;
			clip.wrapMode = loop ? WrapMode.Loop : (name == "Death" ? WrapMode.ClampForever : WrapMode.Once);
		}
		importer.clipAnimations = clips;
	}
}
