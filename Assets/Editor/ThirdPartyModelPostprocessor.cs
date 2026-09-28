using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the third party model packs (Kenney Nature Kit, Quaternius Ultimate Monsters).
/// </summary>
public class ThirdPartyModelPostprocessor : AssetPostprocessor {

	const string KenneyFolder = "Assets/ThirdParty/KenneyNatureKit/";
	const string MonstersFolder = "Assets/ThirdParty/QuaterniusUltimateMonsters/";

	void OnPreprocessModel() {
		ModelImporter importer = (ModelImporter)assetImporter;

		if (assetPath.StartsWith(KenneyFolder)) {
			// Static props: readable meshes so they can be used by mesh colliders.
			importer.isReadable = true;
			importer.animationType = ModelImporterAnimationType.None;
			importer.importAnimation = false;
		} else if (assetPath.StartsWith(MonstersFolder)) {
			// The game drives monsters with the legacy Animation component (see MonsterChaser).
			importer.animationType = ModelImporterAnimationType.Legacy;
			importer.importAnimation = true;
		}
	}

	void OnPreprocessAnimation() {
		if (!assetPath.StartsWith(MonstersFolder))
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
