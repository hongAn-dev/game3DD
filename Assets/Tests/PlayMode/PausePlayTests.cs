using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PausePlayTests {

	[UnitySetUp]
	public IEnumerator LoadLevel1() {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Easy);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene("Level1");
		yield return null;
		yield return null;
	}

	[UnityTest]
	public IEnumerator PauseControllerPausesAndResumes() {
		PauseController pause = Object.FindObjectOfType<PauseController>();
		Assert.IsNotNull(pause);
		pause.Toggle();
		yield return null;
		Assert.AreEqual(FlowState.Paused, GameFlow.State);
		Assert.AreEqual(0f, Time.timeScale);
		Assert.IsTrue(pause.overlay.activeSelf);

		pause.Resume();
		yield return null;
		Assert.AreEqual(FlowState.Playing, GameFlow.State);
		Assert.AreEqual(1f, Time.timeScale);
		Assert.IsFalse(pause.overlay.activeSelf);
	}

	[UnityTest]
	public IEnumerator FocusLossPausesWhilePlaying() {
		PauseController pause = Object.FindObjectOfType<PauseController>();
		typeof(PauseController).GetMethod("OnApplicationFocus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
			.Invoke(pause, new object[] { false });
		yield return null;
		Assert.AreEqual(FlowState.Paused, GameFlow.State);
		Assert.IsTrue(pause.overlay.activeSelf);
		pause.Resume();
	}
}
