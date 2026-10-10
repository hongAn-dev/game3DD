using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Reproducible integration entry point for the challenge revision, in dependency order.</summary>
public static class ChallengeBuildPipeline {
    [MenuItem("Tools/Robo Lac Loi/Apply Challenge Revision")]
    public static void Apply() {
        LevelConfigBuilder.WriteDefaults();
        ChallengeAssetSetup.Apply();
        ControlSetup.Apply();
        EnemySetup.Apply();
        EnergySetup.Apply();
        SupportSetup.Apply();
        PlayerHudSetup.Apply();
        EndingSceneBuilder.Build();
        FlowUiSetup.Apply();
        UiTheme.Apply();
        OverdriveSetup.Apply();
        HarmfulSetup.Apply();
        AudioSetup.Apply();
        UiTheme.Apply();
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("CHALLENGE_INTEGRATION_COMPLETE");
    }

    public static void ApplyAndCapture() {
        Apply();
        ChallengeVisualProbe.Capture();
    }

    public static void RefreshSpawnsAndUi() {
        EnergySetup.Apply();
        RefreshUiAndCapture();
    }

    public static void RefreshUiAndCapture() {
        FlowUiSetup.Apply();
        UiTheme.Apply();
        AssetDatabase.SaveAssets();
        Validate();
        ChallengeVisualProbe.Capture();
    }

    public static void Validate() {
        string folder = "docs/validation/robo-challenge";
        Directory.CreateDirectory(folder);
        using (var report = new StreamWriter(folder + "/serialized-validation.txt")) {
            foreach (LevelConfig config in LevelCatalog.All) {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + config.levelId + ".unity", OpenSceneMode.Single);
                GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
                EnergySpawnDirector energy = manager.GetComponent<EnergySpawnDirector>();
                if (UnityEngine.Object.FindObjectsOfType<EnergySpawnDirector>(true).Length != 1 || UnityEngine.Object.FindObjectsOfType<EnemyDirector>(true).Length != 1
                    || UnityEngine.Object.FindObjectsOfType<SpawnRegistry>(true).Length != 1)
                    throw new Exception(config.levelId + ": duplicate/missing level director or registry");
                if (UnityEngine.Object.FindObjectsOfType<SoundToggle>(true).Length != 1 || UnityEngine.Object.FindObjectsOfType<MusicToggle>(true).Length != 1)
                    throw new Exception(config.levelId + ": duplicate/missing audio toggles");
                if (energy == null || energy.landingPoints.Length < config.energyPointMinimum)
                    throw new Exception(config.levelId + ": insufficient ground spawn candidates");
                GameObject player = GameObject.FindWithTag("Player");
                if (Mathf.Abs(player.GetComponent<Ball>().MaxSpeed - 8.8f) > 0.001f)
                    throw new Exception(config.levelId + ": old player speed override");
                if (player.GetComponent<SlowStatus>() == null)
                    player.AddComponent<SlowStatus>();
                if (manager.GetComponent<SpawnRegistry>() == null)
                    manager.gameObject.AddComponent<SpawnRegistry>();
                if (manager.GetComponent<HarmfulSpawnDirector>() == null)
                    throw new Exception(config.levelId + ": missing harmful director");
                foreach (GameObject sceneRoot in scene.GetRootGameObjects()) {
                    foreach (Transform child in sceneRoot.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                            throw new Exception(config.levelId + ": missing script on " + child.name);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.WriteLine(config.levelId + " candidates=" + energy.landingPoints.Length + " cap=" + config.energyCap + " speed=" + player.GetComponent<Ball>().MaxSpeed);
            }
            foreach (string name in new[] { "MainMenu", "Ending" }) {
                EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);
                if (UnityEngine.Object.FindObjectsOfType<SoundToggle>(true).Length != 1 || UnityEngine.Object.FindObjectsOfType<MusicToggle>(true).Length != 1)
                    throw new Exception(name + ": expected one SFX and one BGM button");
            }
        }
    }
}
