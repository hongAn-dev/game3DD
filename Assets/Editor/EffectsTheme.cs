using UnityEditor;
using UnityEngine;

/// <summary>
/// Recolors the three explosion particle prefabs and strips their own sounds (the event plays its catalog sound).
/// Run with -executeMethod EffectsTheme.Apply
/// </summary>
public static class EffectsTheme {

	[MenuItem("Tools/Robo Lac Loi/Apply Effects")]
	public static void Apply() {
		Theme("ExplodeCoin Particle", new Color32(46, 230, 230, 255), new Color32(220, 255, 255, 255));
		Theme("ExplodeEnemy Particle", new Color32(255, 140, 50, 255), new Color32(90, 90, 90, 255));
		Theme("ExplodePlayer Particle", new Color32(255, 90, 46, 255), new Color32(255, 200, 80, 255));
		AssetDatabase.SaveAssets();
		Debug.Log("EffectsTheme: done");
	}

	static void Theme(string prefabName, Color a, Color b) {
		string path = "Assets/Prefabs/" + prefabName + ".prefab";
		GameObject root = PrefabUtility.LoadPrefabContents(path);
		try {
			foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true)) {
				ParticleSystem.MainModule main = system.main;
				main.startColor = new ParticleSystem.MinMaxGradient(a, b);
			}
			foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
				Object.DestroyImmediate(source);
			PrefabUtility.SaveAsPrefabAsset(root, path);
			Debug.Log("EffectsTheme: " + prefabName);
		} finally {
			PrefabUtility.UnloadPrefabContents(root);
		}
	}
}
