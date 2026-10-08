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
		Assert.IsTrue(Find("Space Camera").activeSelf, "a camera still renders behind the completion panel");
	}

	[UnityTest]
	public IEnumerator NaturalEndShowsTheSameCompletion() {
		yield return Load();
		director.time = director.duration - 0.3;
		yield return new WaitForSeconds(1f);
		Assert.AreEqual(1, controller.CompletionCount);
		Assert.IsTrue(controller.completionPanel.activeSelf);
		Assert.Greater(Camera.allCamerasCount, 0, "a camera still renders behind the completion panel");
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

	// Spec §8: Robo visibly rolls (the timeline only moves it; RollVisual spins the visual child).
	[UnityTest]
	public IEnumerator RoboRollsWhileMoving() {
		yield return Load();
		At(4f);
		director.Resume();
		GameObject visual = Find("Robo Visual");
		Assert.IsNotNull(visual, "mesh lives on a child the timeline does not drive");
		Quaternion last = visual.transform.rotation;
		float spun = 0f;
		float end = Time.time + 1f;
		while (Time.time < end) {
			yield return null;
			spun += Quaternion.Angle(last, visual.transform.rotation);
			last = visual.transform.rotation;
		}
		Assert.Greater(spun, 120f, "rolled, not slid");
	}

	// The ball must stay on the ramp and inside floor while boarding: never cutting into the ship's meshes.
	[UnityTest]
	public IEnumerator BoardingDoesNotClipThroughTheShip() {
		yield return Load();
		director.Pause();
		GameObject ship = Find("Spaceship");
		foreach (MeshFilter filter in ship.GetComponentsInChildren<MeshFilter>(true)) {
			if (filter.GetComponent<Collider>() != null)
				continue;
			filter.gameObject.layer = 2;
			filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
		}
		GameObject robo = Find("Robo");
		for (float t = 14.3f; t <= 15.7f; t += 0.05f) {
			At(t);
			Physics.SyncTransforms();
			Assert.GreaterOrEqual(robo.transform.position.y, 0.45f, "above the ground at " + t);
			Assert.IsFalse(Physics.CheckSphere(robo.transform.position, 0.42f, 1 << 2, QueryTriggerInteraction.Ignore),
				"Robo cuts into the ship at " + t + " s, " + robo.transform.position);
		}
	}

	// Behind the open ramp there is a doorway into the ship, not a solid wall.
	[UnityTest]
	public IEnumerator RampOpensIntoTheShip() {
		yield return Load();
		director.Pause();
		GameObject ship = Find("Spaceship");
		foreach (MeshFilter filter in ship.GetComponentsInChildren<MeshFilter>(true))
			if (filter.GetComponent<Collider>() == null && filter.name != "Door") {
				filter.gameObject.layer = 2;
				filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
			}
		At(15f);
		Physics.SyncTransforms();
		Transform door = ship.GetComponentsInChildren<Transform>(true).First(t => t.name == "Door");
		Vector3 hinge = door.position;
		Vector3 centre = ship.GetComponentsInChildren<Renderer>().First(r => r.name == "Hull").bounds.center;
		Vector3 into = new Vector3(centre.x - hinge.x, 0f, centre.z - hinge.z).normalized;
		Vector3 from = hinge - into * 3f + Vector3.up * 0.75f;
		RaycastHit hit;
		Assert.IsTrue(Physics.Raycast(from, into, out hit, 30f, 1 << 2), "the ship is somewhere ahead");
		Assert.Greater(hit.distance, 3f + 1.5f, "first wall must be well inside the doorway, hit " + hit.collider.name);
	}
}
