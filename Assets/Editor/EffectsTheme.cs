using UnityEditor;
using UnityEngine;

/// <summary>
/// Recolors the three explosion particle prefabs and assigns the theme sounds.
/// Run with -executeMethod EffectsTheme.Apply
/// </summary>
public static class EffectsTheme {

	const string Audio = "Assets/ThirdParty/KenneyAudio/";

	[MenuItem("Tools/Robo Lac Loi/Apply Effects")]
	public static void Apply() {
		Theme("ExplodeCoin Particle", "forceField_000", new Color32(46, 230, 230, 255), new Color32(220, 255, 255, 255));
		Theme("ExplodeEnemy Particle", "explosionCrunch_000", new Color32(255, 140, 50, 255), new Color32(90, 90, 90, 255));
		Theme("ExplodePlayer Particle", "lowFrequency_explosion_000", new Color32(255, 90, 46, 255), new Color32(255, 200, 80, 255));
		AssetDatabase.SaveAssets();
		Debug.Log("EffectsTheme: done");
	}

	static void Theme(string prefabName, string clipName, Color a, Color b) {
		string path = "Assets/Prefabs/" + prefabName + ".prefab";
		GameObject root = PrefabUtility.LoadPrefabContents(path);
		try {
			foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true)) {
				ParticleSystem.MainModule main = system.main;
				main.startColor = new ParticleSystem.MinMaxGradient(a, b);
			}
			foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
				source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + clipName + ".ogg");
			PrefabUtility.SaveAsPrefabAsset(root, path);
			Debug.Log("EffectsTheme: " + prefabName);
		} finally {
			PrefabUtility.UnloadPrefabContents(root);
		}
	}
}
