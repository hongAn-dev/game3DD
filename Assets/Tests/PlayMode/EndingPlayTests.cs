using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EndingPlayTests {

	EndingController controller;
	PlayableDirector director;

	IEnumerator Load() {
		SceneManager.LoadScene(SceneRouter.EndingScene);
		yield return null;
		yield return null;
		controller = Object.FindObjectOfType<EndingController>();
		director = controller.director;
	}

	[TearDown]
	public void Cleanup() {
		AudioListener.pause = false;
	}

	void At(float time) {
		director.time = time;
		director.Evaluate();
	}

	static GameObject Find(string name) {
		foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
			if (t.name == name && t.gameObject.scene.IsValid())
				return t.gameObject;
		return null;
	}

	// Storyboard: roll to the port → charge (beam, four lights) → board (ramp, Robo gone, ramp closed) → take-off → space.
	[UnityTest]
	public IEnumerator StoryBeatsHappenInOrder() {
		yield return Load();
		director.Pause();
		GameObject robo = Find("Robo"), beam = Find("Energy Beam"), ship = Find("Spaceship");
		GameObject door = ship.GetComponentsInChildren<Transform>(true).First(t => t.name == "Door").gameObject;   // not the space copy's
		At(1f);
		Vector3 start = robo.transform.position;
		Quaternion doorClosed = door.transform.localRotation;
		At(7.9f);
		Assert.Greater(Vector3.Distance(start, robo.transform.position), 5f, "Robo rolled to the charging point");
		Assert.IsFalse(beam.activeSelf, "beam starts with the charge at 8 s");
		At(10f);
		Assert.IsTrue(beam.activeSelf, "energy beam during the charge");
		At(12.9f);
		for (int i = 1; i <= 4; i++)
			Assert.IsTrue(Find("Ship Light " + i).activeSelf, "ship light " + i);
		At(14.6f);
		Assert.Greater(Quaternion.Angle(doorClosed, door.transform.localRotation), 60f, "ramp open");
		Assert.IsFalse(beam.activeSelf, "beam over once charged");
		Assert.IsTrue(robo.activeSelf);
		At(16.95f);
		Assert.IsFalse(robo.activeSelf, "Robo is inside the ship");
		Assert.Less(Quaternion.Angle(doorClosed, door.transform.localRotation), 5f, "ramp closed");
		float ground = ship.transform.position.y;
		At(22.5f);
		Assert.Greater(ship.transform.position.y - ground, 15f, "ship took off");
		At(25f);
		Assert.IsTrue(Find("Space Camera").activeSelf, "space shot");
		Assert.IsTrue(Find("Caption Escaped").activeSelf);
	}

	[UnityTest]
	public IEnumerator SkipAppearsAfterOneSecondAndCompletesOnce() {
		yield return Load();
		yield return new WaitForSeconds(0.5f);
		Assert.IsFalse(controller.skipButton.activeSelf);
		Assert.IsFalse(controller.completionPanel.activeSelf);
		yield return new WaitForSeconds(0.8f);
		Assert.IsTrue(controller.skipButton.activeSelf);
		controller.Skip();
		controller.Skip();
		controller.Complete();
		yield return null;
		Assert.AreEqual(1, controller.CompletionCount);
		Assert.IsTrue(controller.completionPanel.activeSelf);
		Assert.IsFalse(controller.skipButton.activeSelf);
		Assert.AreEqual(director.duration, director.time, 0.05, "skip jumps to the last frame");
	}

	[UnityTest]
	public IEnumerator NaturalEndShowsTheSameCompletion() {
		yield return Load();
		director.time = director.duration - 0.3;
		yield return new WaitForSeconds(1f);
		Assert.AreEqual(1, controller.CompletionCount);
		Assert.IsTrue(controller.completionPanel.activeSelf);
	}

	[UnityTest]
	public IEnumerator FocusLossPausesAndResumes() {
		yield return Load();
		yield return new WaitForSeconds(0.3f);
		controller.SendMessage("OnApplicationFocus", false);
		double frozen = director.time;
		yield return new WaitForSecondsRealtime(0.5f);
		Assert.AreEqual(frozen, director.time, 0.001, "timeline paused");
		Assert.IsTrue(AudioListener.pause);
		controller.SendMessage("OnApplicationFocus", true);
		yield return new WaitForSeconds(0.4f);
		Assert.Greater(director.time, frozen + 0.2, "timeline resumed");
		Assert.IsFalse(AudioListener.pause);
		controller.Skip();
		Assert.IsTrue(controller.completionPanel.activeSelf, "skip still works after resume");
	}
}
