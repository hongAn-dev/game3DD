using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class StoryPlayTests {

	DialogPanel dialog;

	IEnumerator Open(string scene) {
		SceneManager.LoadScene(scene);
		yield return null;
		yield return null;
		dialog = Object.FindObjectOfType<DialogPanel>(true);
	}

	// New Game from the menu: tutorial on, intro flag on, fresh run.
	IEnumerator NewGame() {
		SceneRouter.StartCampaign(GameSettings.gameDifficulties.Normal, true);
		yield return null;
		yield return null;
		dialog = Object.FindObjectOfType<DialogPanel>(true);
	}

	[TearDown]
	public void Reset() {
		GameFlow.ResetForScene();
		CampaignProgress.Reset();
	}

	[UnityTest]
	public IEnumerator NewGameShowsTutorialThenL1Card() {
		yield return NewGame();
		Assert.IsTrue(dialog.IsOpen);
		Assert.AreEqual(FlowState.Intro, GameFlow.State);
		Assert.AreEqual("Robo lạc lối", dialog.title.text);
		Assert.IsFalse(dialog.backButton.gameObject.activeSelf, "no Quay lại on page 1");
		Assert.IsTrue(dialog.skipButton.gameObject.activeSelf);
		for (int i = 0; i < 5; i++)
			dialog.Next();
		Assert.AreEqual("Đảo hoang", dialog.title.text);
		Assert.IsFalse(dialog.nextButton.gameObject.activeSelf);
		Assert.IsTrue(dialog.startButton.gameObject.activeSelf);
		Assert.AreEqual("Bắt đầu", dialog.startButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
		Assert.IsFalse(dialog.skipButton.gameObject.activeSelf, "nothing left to skip");
		dialog.Finish();
		Assert.IsFalse(dialog.IsOpen);
		Assert.AreEqual(FlowState.Playing, GameFlow.State);
		Assert.AreEqual(1f, Time.timeScale);
	}

	[UnityTest]
	public IEnumerator SkipGoesToTheL1Card() {
		yield return NewGame();
		dialog.Next();
		dialog.Skip();
		Assert.IsTrue(dialog.IsOpen, "skipping does not start play");
		Assert.AreEqual(FlowState.Intro, GameFlow.State);
		Assert.AreEqual("Đảo hoang", dialog.title.text);
		StringAssert.StartsWith("Robo tỉnh dậy", dialog.body.text);
	}

	[UnityTest]
	public IEnumerator BackButtonGoesToThePreviousPage() {
		yield return NewGame();
		dialog.Next();
		dialog.Next();
		dialog.Back();
		Assert.AreEqual("Di chuyển và quan sát", dialog.title.text);
	}

	[UnityTest]
	public IEnumerator DialogWaitsWithoutTimeout() {
		yield return NewGame();
		yield return new WaitForSecondsRealtime(5f);
		Assert.IsTrue(dialog.IsOpen);
		Assert.AreEqual(FlowState.Intro, GameFlow.State);
		Assert.AreEqual(0f, Time.timeScale);
		Assert.AreEqual(0, Object.FindObjectsOfType<EnemyBrain>().Length + EnemyDirector.Current.PendingSpawns.Count, "no enemies while reading");
	}

	[UnityTest]
	public IEnumerator BackDuringDialogDoesNothing() {
		yield return NewGame();
		Object.FindObjectOfType<PauseController>().Toggle();   // what Escape / Android Back calls
		yield return null;
		Assert.IsTrue(dialog.IsOpen);
		Assert.AreEqual(FlowState.Intro, GameFlow.State);
		Assert.AreEqual("Robo lạc lối", dialog.title.text);
	}

	[UnityTest]
	public IEnumerator LevelCardOnFirstEntry() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = true;
		yield return Open("Level2");
		Assert.IsTrue(dialog.IsOpen);
		Assert.AreEqual("Trạm khai thác bỏ hoang", dialog.title.text);
		Assert.IsFalse(dialog.skipButton.gameObject.activeSelf);
		Assert.IsFalse(dialog.backButton.gameObject.activeSelf);
		Assert.IsTrue(dialog.startButton.gameObject.activeSelf);
		StringAssert.Contains("Thu thập đủ 10 viên năng lượng để qua màn.", dialog.body.text);
	}

	[UnityTest]
	public IEnumerator RetryShowsNoIntro() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = true;
		yield return Open("Level2");
		dialog.Finish();
		GameFlow.Die("test");
		yield return null;
		SceneRouter.Retry();
		yield return null;
		yield return null;
		dialog = Object.FindObjectOfType<DialogPanel>(true);
		Assert.IsFalse(dialog.IsOpen);
		Assert.AreEqual(FlowState.Playing, GameFlow.State);
		Assert.AreEqual(0, GameManager.gm.score);
		// Even with the flag set again (e.g. a stale value), a seen intro stays closed in this run.
		GameSettings.showIntroLevelMessage = true;
		yield return Open("Level2");
		Assert.IsFalse(dialog.IsOpen);
	}

	IEnumerator Playing(string scene) {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = false;
		yield return Open(scene);
	}

	static string[] ButtonLabels(GameObject root) {
		return root.GetComponentsInChildren<UnityEngine.UI.Button>().Select(b => b.GetComponentInChildren<UnityEngine.UI.Text>().text).ToArray();
	}

	// Spec §7.1: exactly "Bạn đã thua", two buttons, no score, gameplay HUD hidden.
	[UnityTest]
	public IEnumerator LosePanelSaysBanDaThua() {
		yield return Playing("Level1");
		GameFlow.Die("Robo rơi xuống biển axit");
		yield return null;
		yield return null;
		GameObject panel = GameManager.gm.gameOverCanvas;
		Assert.IsTrue(panel.activeSelf);
		var texts = panel.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t => t.GetComponentInParent<UnityEngine.UI.Button>() == null).Select(t => t.text).ToArray();
		CollectionAssert.AreEqual(new[] { "Bạn đã thua" }, texts, "only the message");
		CollectionAssert.AreEquivalent(new[] { "Thử lại", "Menu chính" }, ButtonLabels(panel));
		Assert.IsFalse(GameManager.gm.mainCanvas.activeInHierarchy, "score HUD and joystick hidden");
		foreach (GameObject hidden in GameManager.gm.hideOnResult)
			Assert.IsFalse(hidden.activeSelf, hidden.name + " hidden");
		Assert.IsTrue(GameManager.gm.hideOnResult.Any(g => g.GetComponent<Camera>() != null), "the minimap is one of them");
	}

	[UnityTest]
	public IEnumerator RetryButtonLoadsOnce() {
		yield return Playing("Level1");
		GameFlow.Die("test");
		yield return null;
		int loads = 0;
		UnityEngine.Events.UnityAction<Scene, LoadSceneMode> count = (scene, mode) => loads++;
		SceneManager.sceneLoaded += count;
		UnityEngine.UI.Button retry = GameManager.gm.gameOverCanvas.GetComponentsInChildren<UnityEngine.UI.Button>()
			.First(b => b.GetComponentInChildren<UnityEngine.UI.Text>().text == "Thử lại");
		retry.onClick.Invoke();
		retry.onClick.Invoke();
		yield return null;
		yield return null;
		yield return new WaitForSecondsRealtime(0.3f);
		SceneManager.sceneLoaded -= count;
		Assert.AreEqual(1, loads);
		Assert.AreEqual(0, GameManager.gm.score);
		Assert.IsTrue(CampaignProgress.IsActiveRun, "retry keeps the run");
	}

	// Spec §7.5: level complete shows its story title, text and "Sang màn tiếp theo".
	[UnityTest]
	public IEnumerator LevelCompleteUsesTheStory() {
		yield return Playing("Level1");
		GameManager.gm.Collect(GameManager.gm.BeatLevelScore);
		yield return null;
		yield return null;
		GameObject panel = GameManager.gm.beatLevelCanvas;
		Assert.IsTrue(panel.activeSelf);
		StoryPage done = StoryText.LevelComplete(LevelCatalog.Get("Level1"));
		var texts = panel.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t => t.GetComponentInParent<UnityEngine.UI.Button>() == null).Select(t => t.text).ToArray();
		CollectionAssert.AreEquivalent(new[] { "Đã vượt qua Đảo hoang", done.body }, texts);
		CollectionAssert.AreEqual(new[] { "Sang màn tiếp theo" }, ButtonLabels(panel));
		foreach (GameObject hidden in GameManager.gm.hideOnResult)
			Assert.IsFalse(hidden.activeSelf, hidden.name + " hidden");
	}

	// Spec §7.6: the icon and label flip at once, and muting plays no click for that tap.
	[UnityTest]
	public IEnumerator SoundToggleFlipsAtOnceWithoutClick() {
		bool saved = SoundSettings.SfxEnabled;
		SoundSettings.SfxEnabled = true;
		yield return Playing("Level1");
		yield return new WaitForSecondsRealtime(Sfx.Cooldown + 0.05f);
		int clicks = 0;
		System.Action<SfxEvent> count = e => { if (e == SfxEvent.UiClick) clicks++; };
		Sfx.Played += count;
		SoundToggle toggle = Object.FindObjectOfType<SoundToggle>();
		Assert.AreEqual("Tắt hiệu ứng âm thanh", toggle.ActionLabel);
		toggle.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
		Assert.IsFalse(SoundSettings.SfxEnabled);
		Assert.AreEqual(toggle.soundOff, toggle.icon.sprite);
		Assert.AreEqual("Bật hiệu ứng âm thanh", toggle.ActionLabel);
		Assert.AreEqual(0, clicks, "no click after muting");
		Assert.AreEqual(0, Sfx.ActiveVoices);
		toggle.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
		Assert.IsTrue(SoundSettings.SfxEnabled);
		Assert.AreEqual(toggle.soundOn, toggle.icon.sprite);
		Assert.AreEqual(1, clicks, "unmuting answers with one click (the click sound is wired, after the toggle)");
		Assert.AreEqual(1, PlayerPrefs.GetInt(SoundSettings.SfxKey, 0), "saved");
		Sfx.Played -= count;
		SoundSettings.SfxEnabled = saved;
	}

	[UnityTest]
	public IEnumerator PauseButtonOnlyWhilePlaying() {
		yield return Playing("Level1");
		PauseController pause = Object.FindObjectOfType<PauseController>();
		SoundToggle toggle = Object.FindObjectOfType<SoundToggle>();
		Assert.IsTrue(pause.pauseButton.activeInHierarchy);
		pause.pauseButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
		yield return null;
		Assert.AreEqual(FlowState.Paused, GameFlow.State);
		Assert.IsFalse(pause.pauseButton.activeInHierarchy, "no second pause panel");
		Assert.IsTrue(toggle.gameObject.activeInHierarchy, "sound toggle still usable while paused");
		pause.Resume();
		yield return null;
		Assert.IsTrue(pause.pauseButton.activeInHierarchy);
		GameFlow.Die("test");
		yield return null;
		Assert.IsFalse(pause.pauseButton.activeInHierarchy, "locked on the result panel");
		Assert.IsTrue(toggle.gameObject.activeInHierarchy);
	}

	[UnityTest]
	public IEnumerator BackWhilePausedResumes() {
		yield return Playing("Level1");
		PauseController pause = Object.FindObjectOfType<PauseController>();
		pause.Toggle();   // Android Back / Escape while playing
		Assert.AreEqual(FlowState.Paused, GameFlow.State);
		pause.Toggle();   // Back again = Tiếp tục
		Assert.AreEqual(FlowState.Playing, GameFlow.State);
		pause.Toggle();
		UnityEngine.UI.Button exit = pause.overlay.GetComponentsInChildren<UnityEngine.UI.Button>()
			.First(b => b.GetComponentInChildren<UnityEngine.UI.Text>().text == "Thoát về menu");
		exit.onClick.Invoke();
		yield return null;
		yield return null;
		Assert.AreEqual(SceneRouter.MainMenuScene, SceneManager.GetActiveScene().name);
		Assert.IsFalse(CampaignProgress.IsActiveRun, "leaving ends the run");
	}

	[UnityTest]
	public IEnumerator SettingsSlidersSaveVolumesAndSensitivity() {
		float music = SoundSettings.MusicVolume, sfx = SoundSettings.SfxVolume, ambience = SoundSettings.AmbienceVolume, look = ThirdPersonOrbitCamera.Sensitivity;
		yield return Open("MainMenu");
		MainMenuPanels panels = Object.FindObjectOfType<MainMenuPanels>(true);
		panels.OpenSettings();
		Assert.IsTrue(panels.settings.activeSelf);
		Assert.AreEqual(music, panels.music.value, 0.001f, "shows the saved value");
		panels.music.value = 0.4f;
		panels.sfx.value = 0.7f;
		panels.ambience.value = 0.2f;
		panels.sensitivity.value = 1.5f;
		Assert.AreEqual(0.4f, PlayerPrefs.GetFloat("volume_music"), 0.001f);
		Assert.AreEqual(0.7f, SoundSettings.SfxVolume, 0.001f);
		Assert.AreEqual(0.2f, SoundSettings.AmbienceVolume, 0.001f);
		Assert.AreEqual(1.5f, ThirdPersonOrbitCamera.Sensitivity, 0.001f);
		SoundSettings.Reload();
		Assert.AreEqual(0.4f, SoundSettings.MusicVolume, 0.001f, "read back from the device");
		panels.CloseSettings();
		Assert.IsFalse(panels.settings.activeSelf);
		SoundSettings.MusicVolume = music;
		SoundSettings.SfxVolume = sfx;
		SoundSettings.AmbienceVolume = ambience;
		ThirdPersonOrbitCamera.Sensitivity = look;
	}

	[UnityTest]
	public IEnumerator GuideReturnsToMenuWithoutStartingARun() {
		CampaignProgress.Reset();
		yield return Open("MainMenu");
		MainMenuPanels panels = Object.FindObjectOfType<MainMenuPanels>(true);
		panels.OpenGuide();
		Assert.IsTrue(dialog.IsOpen);
		var titles = new System.Collections.Generic.List<string> { dialog.title.text };
		while (dialog.nextButton.gameObject.activeSelf) {
			dialog.Next();
			titles.Add(dialog.title.text);
		}
		CollectionAssert.IsSubsetOf(new[] { "Robo lạc lối", "Vật phẩm hỗ trợ", "Vật phẩm tăng tốc", "Âm thanh và tạm dừng" }, titles);
		Assert.AreEqual("Đóng", dialog.startButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
		dialog.Finish();
		panels.OpenGuide();
		Assert.IsTrue(dialog.skipButton.gameObject.activeSelf, "Đóng on the first page too");
		Assert.AreEqual("Đóng", dialog.skipButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
		dialog.Skip();
		Assert.IsFalse(dialog.IsOpen, "closed from page 1");
		yield return null;
		Assert.IsFalse(dialog.IsOpen);
		Assert.AreEqual(SceneRouter.MainMenuScene, SceneManager.GetActiveScene().name);
		Assert.IsFalse(CampaignProgress.IsActiveRun);
		Assert.IsFalse(CampaignProgress.TutorialPending);
	}

	// A double tap on "Tiếp theo" must not also press "Bắt đầu" (same place) and skip the Đảo hoang card.
	[UnityTest]
	public IEnumerator DoubleTapDoesNotSkipTheLastPage() {
		yield return NewGame();
		for (int i = 0; i < 4; i++)
			dialog.Next();
		yield return new WaitForSecondsRealtime(DialogPanel.TapGuard + 0.05f);
		dialog.nextButton.onClick.Invoke();
		dialog.startButton.onClick.Invoke();   // second tap of the double tap
		Assert.IsTrue(dialog.IsOpen);
		Assert.AreEqual("Đảo hoang", dialog.title.text);
		yield return new WaitForSecondsRealtime(DialogPanel.TapGuard + 0.05f);
		dialog.startButton.onClick.Invoke();
		Assert.IsFalse(dialog.IsOpen);
		Assert.AreEqual(FlowState.Playing, GameFlow.State);
	}
}
