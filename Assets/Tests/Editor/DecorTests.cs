using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class DecorTests {

	// Trees/rocks stay off the robot's start and the energy landing points; every level has its own creep looks
	// with working animation clips.
	[Test]
	public void LevelsHaveDecorAndThemedCreeps() {
		foreach (LevelConfig level in LevelCatalog.All) {
			EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
			Transform decor = GameObject.Find(DecorSetup.RootName).transform;
			Assert.GreaterOrEqual(decor.childCount, 20, level.levelId);
			Assert.Greater(decor.GetComponentsInChildren<CapsuleCollider>().Length, 0, level.levelId + " has trees/rocks to bump into");
			Vector3 start = GameObject.FindWithTag("Player").transform.position;
			EnergySpawnDirector energy = Object.FindObjectOfType<EnergySpawnDirector>();
			foreach (Transform piece in decor) {
				Vector2 p = new Vector2(piece.position.x, piece.position.z);
				Assert.GreaterOrEqual(Vector2.Distance(p, new Vector2(start.x, start.z)), DecorSetup.StartClear - 1f, level.levelId + " " + piece.name + " near the start");
				foreach (Transform point in energy.landingPoints.Where(t => piece.GetComponent<CapsuleCollider>() != null))
					Assert.GreaterOrEqual(Vector2.Distance(p, new Vector2(point.position.x, point.position.z)), 1.5f, level.levelId + " " + piece.name + " on a landing point");
			}
			EnemyDirector director = Object.FindObjectOfType<EnemyDirector>();
			Assert.AreEqual(2, director.creepVariants.Length, level.levelId);
			foreach (GameObject variant in director.creepVariants) {
				EnemyBrain brain = variant.GetComponent<EnemyBrain>();
				Animation anim = variant.GetComponentInChildren<Animation>();
				foreach (string clip in new[] { brain.idleClip, brain.runClip, brain.attackClip })
					Assert.IsNotNull(anim.GetClip(clip), variant.name + " " + clip);
			}
		}
	}
}
