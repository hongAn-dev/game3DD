using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class FlowTests {

	[SetUp]
	public void ResetCampaign() {
		CampaignProgress.Reset();
	}

	[Test]
	public void LevelConfigsMatchSpecTargetsAndRouteToEnding() {
		Assert.AreEqual(4, LevelCatalog.All.Count);
		CollectionAssert.AreEqual(new[] { "Level1", "Level2", "Level3", "Level4" }, LevelCatalog.All.Select(c => c.levelId).ToArray());
		CollectionAssert.AreEqual(new[] { 6, 10, 14, 18 }, LevelCatalog.All.Select(c => c.energyTarget).ToArray());
		CollectionAssert.AreEqual(new[] { "Level2", "Level3", "Level4", "Ending" }, LevelCatalog.All.Select(c => c.nextScene).ToArray());
		Assert.IsTrue(LevelCatalog.Get("Level4").IsFinal);
		Assert.IsFalse(LevelCatalog.Get("Level3").IsFinal);
		Assert.IsNull(LevelCatalog.Get("MainMenu"));
		Assert.AreSame(LevelCatalog.Get("Level1"), LevelCatalog.First);
	}

	[Test]
	public void EnergyBudgetIsReachable() {
		foreach (LevelConfig c in LevelCatalog.All) {
			Assert.LessOrEqual(c.energyAtStart, c.energyCap, c.levelId);
			Assert.Greater(c.energyIntervalMin, 0f, c.levelId);
			Assert.LessOrEqual(c.energyIntervalMin, c.energyIntervalMax, c.levelId);
			Assert.AreEqual(3, c.enemyCap.Length, c.levelId);
			Assert.AreEqual(3, c.bossCap.Length, c.levelId);
			Assert.IsNotEmpty(c.displayName, c.levelId);
			Assert.IsNotEmpty(c.hazardDeathMessage, c.levelId);
		}
	}

	[Test]
	public void CampaignCompletesEachLevelOnce() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		Assert.IsTrue(CampaignProgress.CompleteLevel("Level1"));
		Assert.IsFalse(CampaignProgress.CompleteLevel("Level1"));
		Assert.AreEqual(1, CampaignProgress.CompletedCount);
		Assert.AreEqual(25, CampaignProgress.FuelPercent);
	}

	[Test]
	public void RetryKeepsCompletedLevelsAndNewGameResets() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Easy);
		CampaignProgress.CompleteLevel("Level1");
		// A retry of Level2 does not touch the campaign.
		Assert.IsTrue(CampaignProgress.IsCompleted("Level1"));
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Hard);
		Assert.IsFalse(CampaignProgress.IsCompleted("Level1"));
		Assert.AreEqual(GameSettings.gameDifficulties.Hard, CampaignProgress.Difficulty);
		Assert.AreEqual(GameSettings.gameDifficulties.Hard, GameSettings.difficulty);
	}

	[Test]
	public void EndingNeedsAllFourLevelsInAnActiveRun() {
		foreach (string id in new[] { "Level1", "Level2", "Level3", "Level4" })
			CampaignProgress.CompleteLevel(id);
		Assert.IsFalse(CampaignProgress.CanPlayEnding, "no active run (Level4 opened directly)");

		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Easy);
		foreach (string id in new[] { "Level1", "Level2", "Level3" })
			CampaignProgress.CompleteLevel(id);
		Assert.IsFalse(CampaignProgress.CanPlayEnding);
		CampaignProgress.CompleteLevel("Level4");
		Assert.IsTrue(CampaignProgress.CanPlayEnding);
		Assert.AreEqual(100, CampaignProgress.FuelPercent);
	}
	[Test]
	public void PauseFreezesTimeAndResumeRestoresIt() {
		GameFlow.ResetForScene();
		Assert.IsTrue(GameFlow.Pause());
		Assert.AreEqual(FlowState.Paused, GameFlow.State);
		Assert.AreEqual(0f, Time.timeScale);
		Assert.IsFalse(GameFlow.IsGameplayActive);
		Assert.IsTrue(GameFlow.Resume());
		Assert.AreEqual(1f, Time.timeScale);
		Assert.IsTrue(GameFlow.IsGameplayActive);
	}

	[Test]
	public void GameFlowIgnoresCompleteAfterDeath() {
		GameFlow.ResetForScene();
		Assert.IsTrue(GameFlow.Die("MẤT KẾT NỐI"));
		Assert.IsFalse(GameFlow.CompleteLevel());
		Assert.IsFalse(GameFlow.Die("again"));
		Assert.AreEqual(FlowState.Dead, GameFlow.State);
	}

	[Test]
	public void ReportedHazardCauseWinsOverFallback() {
		GameFlow.ResetForScene();
		GameFlow.ReportDeathCause("Robo chạm dung nham");
		GameFlow.ReportDeathCause("second report ignored");
		GameFlow.Die("MẤT KẾT NỐI");
		Assert.AreEqual("Robo chạm dung nham", GameFlow.DeathCause);
	}

	[Test]
	public void SceneLoadResetsFlowAndTime() {
		GameFlow.ResetForScene();
		Assert.IsTrue(GameFlow.Pause());
		Assert.AreEqual(0f, Time.timeScale);
		GameFlow.ResetForScene();
		Assert.AreEqual(FlowState.Playing, GameFlow.State);
		Assert.AreEqual(1f, Time.timeScale);
		Assert.AreEqual("", GameFlow.DeathCause);
	}
	[Test]
	public void EndingSceneAndAllRouteTargetsAreInTheBuild() {
		string[] inBuild = EditorBuildSettings.scenes.Where(s => s.enabled)
			.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
		CollectionAssert.Contains(inBuild, SceneRouter.MainMenuScene);
		CollectionAssert.Contains(inBuild, SceneRouter.EndingScene);
		foreach (LevelConfig c in LevelCatalog.All) {
			CollectionAssert.Contains(inBuild, c.levelId);
			CollectionAssert.Contains(inBuild, c.nextScene);
		}
	}

	[Test]
	public void NoBuildIndexRouting() {
		foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
			StringAssert.DoesNotContain("buildIndex + 1", File.ReadAllText(file), file);
	}
}
