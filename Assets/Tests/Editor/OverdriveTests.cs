using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class OverdriveTests {

	[Test]
	public void PickupIsABoltThatNeverScores() {
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Overdrive.prefab");
		Assert.IsNotNull(prefab);
		Assert.IsNotNull(prefab.GetComponent<OverdrivePickup>());
		Assert.IsNull(prefab.GetComponentInChildren<Treasure>(true), "not part of the energy budget");
		Assert.IsTrue(prefab.GetComponent<SphereCollider>().isTrigger);
		Assert.IsNotNull(prefab.transform.Find("Model").GetComponent<EnergyBob>(), "the bolt spins, the trigger does not");
	}

	[Test]
	public void BoostStartsFromLevel2AndEveryLevelHasTheIndicator() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			OverdriveDirector director = Object.FindObjectOfType<OverdriveDirector>();
			if (level.order < 2) {
				Assert.IsNull(director, level.levelId);
			} else {
				Assert.IsNotNull(director, level.levelId);
				Assert.IsNotNull(director.pickupPrefab, level.levelId);
				Assert.AreEqual(25f, director.intervalMin);
				Assert.AreEqual(35f, director.intervalMax);
			}
			OverdriveIndicator indicator = Object.FindObjectOfType<OverdriveIndicator>();
			Assert.IsNotNull(indicator, level.levelId);
			Assert.IsFalse(indicator.content.activeSelf, level.levelId + ": hidden until boosted");
			Assert.AreEqual(new Color32(255, 214, 40, 255), (Color32)indicator.seconds.color, level.levelId + ": countdown in Overdrive yellow");
			// The HUD zone label follows LevelConfig (it kept the pre-T4 names before).
			Assert.AreEqual(level.displayName.ToUpperInvariant(), GameObject.Find("Zone Text").GetComponent<UnityEngine.UI.Text>().text, level.levelId);
		}
	}
}
