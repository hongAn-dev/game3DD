using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Audio (spec §8): imports the synthesized clips (Tools/audio/make_sfx.py → Assets/ThirdParty/RoboLacLoi/Audio,
/// decompressed on load), fills Resources/AudioCatalog, gives the Player prefab its RobotSounds, puts level/menu
/// music in the Music group (≈ −10.5 dB under SFX) and sets each level's ambience bed (ZoneDresser). Re-runnable.
/// </summary>
public static class AudioSetup {

	public const string Folder = "Assets/ThirdParty/RoboLacLoi/Audio/";
	const string CatalogPath = "Assets/Resources/AudioCatalog.asset";
	const float MusicVolume = 0.3f;

	static readonly (SfxEvent e, string file, float volume)[] Map = {
		(SfxEvent.UiClick, "ui_click", 0.6f), (SfxEvent.EnergyPickup, "energy_pickup", 0.8f), (SfxEvent.Heal, "heal", 0.8f),
		(SfxEvent.ShieldOn, "shield_on", 0.7f), (SfxEvent.ShieldBlock, "shield_block", 0.8f), (SfxEvent.ShieldOff, "shield_off", 0.6f),
		(SfxEvent.OverdriveOn, "overdrive_on", 0.7f), (SfxEvent.OverdriveOff, "overdrive_off", 0.6f),
		(SfxEvent.RobotHit, "robot_hit", 0.9f), (SfxEvent.CreepAttack, "creep_attack", 0.6f),
		(SfxEvent.BossWarn, "boss_warn", 0.8f), (SfxEvent.BossStrike, "boss_strike", 0.8f), (SfxEvent.Lose, "lose", 0.8f),
		(SfxEvent.LevelWin, "level_win", 0.8f), (SfxEvent.CampaignWin, "campaign_win", 0.8f),
	};

	[MenuItem("Tools/Robo Lac Loi/Apply Audio Setup")]
	public static void Apply() {
		foreach (string file in Directory.GetFiles(Folder, "*.wav")) {
			AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(file);
			AudioImporterSampleSettings settings = importer.defaultSampleSettings;
			bool bed = file.Contains("ambience") || file.Contains("ending_engine");
			settings.loadType = bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
			settings.compressionFormat = AudioCompressionFormat.Vorbis;
			settings.quality = 0.7f;
			importer.defaultSampleSettings = settings;
			importer.forceToMono = true;
			importer.preloadAudioData = true;
			importer.SaveAndReimport();
		}
		AudioCatalog catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
		if (catalog == null) {
			catalog = ScriptableObject.CreateInstance<AudioCatalog>();
			AssetDatabase.CreateAsset(catalog, CatalogPath);
		}
		int count = System.Enum.GetValues(typeof(SfxEvent)).Length;
		catalog.clips = new AudioClip[count];
		catalog.volumes = new float[count];
		foreach (var (e, file, volume) in Map) {
			catalog.clips[(int)e] = Clip(file);
			catalog.volumes[(int)e] = volume;
		}
		EditorUtility.SetDirty(catalog);

		GameObject contents = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
		try {
			if (contents.GetComponent<RobotSounds>() == null)
				contents.AddComponent<RobotSounds>();
			PrefabUtility.SaveAsPrefabAsset(contents, "Assets/Prefabs/Player.prefab");
		} finally {
			PrefabUtility.UnloadPrefabContents(contents);
		}
		AssetDatabase.SaveAssets();

		foreach (ZoneDresser.Zone zone in ZoneDresser.Zones) {
			var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + zone.scene + ".unity", OpenSceneMode.Single);
			ZoneDresser.AddAmbient(zone);
			Music(Object.FindObjectOfType<GameManager>().backgroundMusic);
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
		}
		var menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
		foreach (AudioSource source in Object.FindObjectsOfType<AudioSource>())
			if (source.GetComponent<UnityEngine.UI.Button>() == null)   // old click sources are UiTheme's to remove
				Music(source);
		EditorSceneManager.MarkSceneDirty(menu);
		EditorSceneManager.SaveScene(menu);
		Debug.Log("AudioSetup: done");
	}

	static void Music(AudioSource source) {
		SoundGroup group = source.GetComponent<SoundGroup>();
		if (group == null)
			group = source.gameObject.AddComponent<SoundGroup>();
		group.group = SoundGroup.Group.Music;
		group.baseVolume = MusicVolume;
		source.volume = MusicVolume;
	}

	public static AudioClip Clip(string name) {
		return AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name + ".wav");
	}
}
