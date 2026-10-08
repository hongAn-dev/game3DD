using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class EndingTests {

	[Test]
	public void ShipModelHasStoryParts() {
		var names = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/RoboLacLoi/Models/Spaceship.fbx").OfType<Transform>().Select(t => t.name).ToList();
		CollectionAssert.IsSubsetOf(new[] { "Hull", "Door", "Port", "Pod_1", "Pod_2", "Pod_3", "Pod_4", "Flame_L", "Flame_R" }, names);
	}

	[Test]
	public void EndingTimelineLasts22To28SecondsAndBindsEveryTrack() {
		EditorSceneManager.OpenScene("Assets/Scenes/Ending.unity", OpenSceneMode.Single);
		EndingController controller = Object.FindObjectOfType<EndingController>();
		PlayableDirector director = controller.director;
		Assert.IsNotNull(director);
		Assert.IsFalse(director.playOnAwake, "the controller starts it");
		TimelineAsset timeline = (TimelineAsset)director.playableAsset;
		Assert.That(timeline.duration, Is.InRange(22.0, 28.0));
		foreach (TrackAsset track in timeline.GetOutputTracks()) {
			Assert.IsNotNull(director.GetGenericBinding(track), track.name + " is not bound");
			Assert.IsNotEmpty(track.GetClips(), track.name);
		}
		foreach (string beat in new[] { "Robo", "Spaceship", "Camera", "Energy beam", "Ship light 4", "Flame L", "Space camera", "Caption escaped" })
			Assert.IsTrue(timeline.GetOutputTracks().Any(t => t.name == beat), beat);
		Assert.IsNotNull(controller.skipButton);
		Assert.IsFalse(controller.skipButton.activeSelf, "skip appears after a second");
	}

	[Test]
	public void EndingHasNoGameplay() {
		EditorSceneManager.OpenScene("Assets/Scenes/Ending.unity", OpenSceneMode.Single);
		Assert.IsNull(Object.FindObjectOfType<GameManager>());
		Assert.IsNull(Object.FindObjectOfType<Ball>());
		Assert.IsNull(Object.FindObjectOfType<Health>());
		Assert.IsNull(Object.FindObjectOfType<EnemyDirector>());
		Assert.IsNull(Object.FindObjectOfType<EnergySpawnDirector>());
		Assert.IsEmpty(Object.FindObjectsOfType<Treasure>(true));
		Assert.IsEmpty(Object.FindObjectsOfType<Collider>(true).Where(c => c.attachedRigidbody != null));
	}
}
