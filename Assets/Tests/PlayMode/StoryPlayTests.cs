using System.Collections;
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
}
