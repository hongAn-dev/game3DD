using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

public class AudioTests {

	static readonly (SfxEvent e, float min, float max)[] Lengths = {
		(SfxEvent.UiClick, 0.05f, 0.15f), (SfxEvent.EnergyPickup, 0.15f, 0.30f), (SfxEvent.Heal, 0.25f, 0.45f),
		(SfxEvent.ShieldOn, 0.1f, 0.4f), (SfxEvent.ShieldBlock, 0.1f, 0.4f), (SfxEvent.ShieldOff, 0.1f, 0.4f),
		(SfxEvent.RobotHit, 0.15f, 0.30f), (SfxEvent.CreepAttack, 0.15f, 0.35f), (SfxEvent.BossWarn, 0.2f, 0.6f),
		(SfxEvent.BossStrike, 0.2f, 0.6f), (SfxEvent.Lose, 0.6f, 0.9f), (SfxEvent.LevelWin, 0.7f, 1.5f),
		(SfxEvent.CampaignWin, 0.7f, 1.5f), (SfxEvent.OverdriveOn, 0.2f, 0.4f), (SfxEvent.OverdriveOff, 0.2f, 0.4f),
	};

	// Spec §8.2: one clip per event, lengths from the table, no clipping (peak <= -1 dBFS).
	[Test]
	public void CatalogCoversEveryEvent() {
		AudioCatalog catalog = Resources.Load<AudioCatalog>("AudioCatalog");
		Assert.IsNotNull(catalog);
		foreach (SfxEvent e in Enum.GetValues(typeof(SfxEvent)))
			Assert.IsNotNull(catalog.Clip(e), e.ToString());
		foreach (var (e, min, max) in Lengths) {
			AudioClip clip = catalog.Clip(e);
			Assert.That(clip.length, Is.InRange(min, max), e + " length");
			float[] data = new float[clip.samples * clip.channels];
			Assert.IsTrue(clip.GetData(data, 0), e + " readable");
			float peak = 0f;
			foreach (float v in data)
				peak = Mathf.Max(peak, Mathf.Abs(v));
			Assert.LessOrEqual(peak, Mathf.Pow(10f, -1f / 20f), e + " peak");
		}
		Assert.Greater(catalog.Clip(SfxEvent.CampaignWin).length, catalog.Clip(SfxEvent.LevelWin).length, "campaign win is longer");
	}

	[Test]
	public void SettingsPersistAndDefaultOn() {
		PlayerPrefs.DeleteKey(SoundSettings.SfxKey);
		SoundSettings.Reload();
		Assert.IsTrue(SoundSettings.SfxEnabled, "on at first run");
		SoundSettings.SfxEnabled = false;
		Assert.AreEqual(0, PlayerPrefs.GetInt(SoundSettings.SfxKey, 1));
		SoundSettings.Reload();
		Assert.IsFalse(SoundSettings.SfxEnabled, "remembered");
		SoundSettings.SfxEnabled = true;
		SoundSettings.MusicVolume = 0.4f;
		SoundSettings.Reload();
		Assert.AreEqual(0.4f, SoundSettings.MusicVolume, 0.001f);
		SoundSettings.MusicVolume = 1f;
	}

	// Spec §8.1: the harsh loop is gone (asset, scenes, prefabs, Timeline, builders) and no AudioClip reference dangles.
	const string ForbiddenGuid = "850ad93d20b2c9b3a895d9de86c09f9c";
	const string ForbiddenName = "computerNoise_000";

	[Test]
	public void ForbiddenClipIsGoneEverywhere() {
		Assert.IsEmpty(AssetDatabase.GUIDToAssetPath(ForbiddenGuid), "asset deleted");
		Assert.IsFalse(File.Exists("Assets/ThirdParty/KenneyAudio/" + ForbiddenName + ".ogg"));
		var data = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories)
			.Where(f => f.EndsWith(".unity") || f.EndsWith(".prefab") || f.EndsWith(".playable") || f.EndsWith(".asset")
				|| (f.EndsWith(".cs") && !f.Replace('\\', '/').StartsWith("Assets/Tests/")));
		var clipRef = new Regex("fileID: 8300000, guid: ([0-9a-f]{32})");
		foreach (string file in data) {
			string text = File.ReadAllText(file);
			Assert.IsFalse(text.Contains(ForbiddenGuid) || text.Contains(ForbiddenName), file);
			foreach (Match m in clipRef.Matches(text))
				Assert.IsNotEmpty(AssetDatabase.GUIDToAssetPath(m.Groups[1].Value), file + " has a missing AudioClip");
		}
	}

	// One sound per event: the pickup/explosion prefabs are silent, the event plays its catalog sound.
	[Test]
	public void ExplosionPrefabsHaveNoOwnSound() {
		foreach (string name in new[] { "ExplodeCoin Particle", "ExplodeEnemy Particle", "ExplodePlayer Particle" }) {
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
			Assert.IsEmpty(prefab.GetComponentsInChildren<AudioSource>(true), name);
		}
	}

	static void CheckButtons(GameObject root, string where) {
		foreach (Button button in root.GetComponentsInChildren<Button>(true)) {
			string name = where + "/" + button.name;
			Assert.IsNull(button.GetComponent<AudioSource>(), name + " has its own AudioSource");
			ClickSound click = button.GetComponent<ClickSound>();
			Assert.IsNotNull(click, name);
			int calls = 0;
			for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++) {
				Assert.IsFalse(button.onClick.GetPersistentTarget(i) is AudioSource, name + " old click listener");
				if (button.onClick.GetPersistentTarget(i) == click && button.onClick.GetPersistentMethodName(i) == "Play")
					calls++;
			}
			Assert.AreEqual(1, calls, name + " click listener");
		}
	}

	[Test]
	public void ButtonsUseClickSound() {
		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })) {
			string path = AssetDatabase.GUIDToAssetPath(guid);
			CheckButtons(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
		}
		foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes) {
			EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
			foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
				CheckButtons(root, scene.path);
		}
	}

	// L1 acid bed, L2/L4 station bed, L3 none (old source removed); beds and music sit under the SFX in the mix.
	[Test]
	public void LevelAmbienceIsSoft() {
		var beds = new[] { ("Level1", "ambience_acid"), ("Level2", "ambience_station"), ("Level3", null), ("Level4", "ambience_station") };
		foreach (var (level, bed) in beds) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity", OpenSceneMode.Single);
			GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
			Transform ambient = manager.transform.Find("Ambient");
			if (bed == null) {
				Assert.IsNull(ambient, level + " has no ambience");
			} else {
				AudioSource source = ambient.GetComponent<AudioSource>();
				Assert.AreEqual(bed, source.clip.name, level);
				Assert.IsTrue(source.loop && source.playOnAwake, level);
				SoundGroup group = ambient.GetComponent<SoundGroup>();
				Assert.AreEqual(SoundGroup.Group.Ambience, group.group, level);
				Assert.That(20f * Mathf.Log10(group.baseVolume), Is.InRange(-20f, -14f), level + " ambience level");
			}
			SoundGroup music = manager.backgroundMusic.GetComponent<SoundGroup>();
			Assert.AreEqual(SoundGroup.Group.Music, music.group, level);
			Assert.That(20f * Mathf.Log10(music.baseVolume), Is.InRange(-12f, -8f), level + " music level");
		}
		EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
		foreach (AudioSource source in UnityEngine.Object.FindObjectsOfType<AudioSource>())
			Assert.IsNotNull(source.GetComponent<SoundGroup>(), "MainMenu " + source.name);
	}

	// Ending: soft charge, short ignition, smooth engine from the new set, with fades, in the SFX group.
	[Test]
	public void EndingUsesTheNewClips() {
		EditorSceneManager.OpenScene("Assets/Scenes/Ending.unity", OpenSceneMode.Single);
		PlayableDirector director = UnityEngine.Object.FindObjectOfType<EndingController>().director;
		var names = new System.Collections.Generic.List<string>();
		foreach (TrackAsset track in ((TimelineAsset)director.playableAsset).GetOutputTracks().OfType<AudioTrack>()) {
			AudioSource source = (AudioSource)director.GetGenericBinding(track);
			Assert.AreEqual(SoundGroup.Group.Sfx, source.GetComponent<SoundGroup>().group, track.name);
			foreach (TimelineClip clip in track.GetClips()) {
				AudioClip audio = ((AudioPlayableAsset)clip.asset).clip;
				StringAssert.StartsWith(AudioSetup.Folder, AssetDatabase.GetAssetPath(audio), track.name);
				Assert.That(clip.easeInDuration, Is.InRange(0.2, 0.5), track.name + " fade in");
				Assert.That(clip.easeOutDuration, Is.InRange(0.2, 0.5), track.name + " fade out");
				names.Add(audio.name);
			}
		}
		CollectionAssert.IsSubsetOf(new[] { "ending_charge", "ending_ignition", "ending_engine" }, names);
	}
}
