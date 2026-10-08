using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class MapTests {

	static void Open(LevelConfig level) {
		EditorSceneManager.OpenScene("Assets/Scenes/" + level.levelId + ".unity", OpenSceneMode.Single);
	}

	static HazardVolume Sea() {
		return Object.FindObjectsOfType<HazardVolume>().First(h => h.name == "DeathZone");
	}

	static bool InHazard(Vector3 point) {
		return Object.FindObjectsOfType<HazardVolume>().Any(h => h.GetComponent<Collider>().bounds.Contains(point));
	}

	static Vector3 Start() {
		return GameObject.FindWithTag("Player").transform.position;
	}

	[Test]
	public void HazardsHaveThickTriggerAndSeparateVisual() {
		foreach (LevelConfig level in LevelCatalog.All) {
			Open(level);
			HazardVolume[] hazards = Object.FindObjectsOfType<HazardVolume>();
			Assert.GreaterOrEqual(hazards.Length, 2, level.levelId + ": sea + kill plane");
			foreach (HazardVolume hazard in hazards) {
				BoxCollider box = hazard.GetComponent<BoxCollider>();
				Assert.IsNotNull(box, hazard.name);
				Assert.IsTrue(box.isTrigger, hazard.name);
				Assert.GreaterOrEqual(box.bounds.size.y, 2f, level.levelId + " " + hazard.name + " is too thin");
				for (Transform t = hazard.transform; t != null; t = t.parent)
					Assert.IsTrue(t.lossyScale.x > 0f && t.lossyScale.y > 0f && t.lossyScale.z > 0f, t.name + " has a zero scale");
				Assert.IsNull(hazard.GetComponent<Renderer>(), hazard.name + ": visual must be separate");
			}
			GameObject visual = GameObject.Find("HazardVisual");
			Assert.IsNotNull(visual, level.levelId);
			Assert.IsNull(visual.GetComponent<Collider>(), level.levelId);
			Assert.AreEqual(level.levelId == "Level3" ? "Lava" : "Acid", visual.GetComponent<Renderer>().sharedMaterial.name, level.levelId);
			Assert.IsNull(GameObject.Find("WaterDeathZone"), level.levelId);
		}
	}

	[Test]
	public void HazardCauseMatchesLevel() {
		foreach (LevelConfig level in LevelCatalog.All) {
			StringAssert.DoesNotContain("nước", level.hazardDeathMessage.ToLowerInvariant(), level.levelId);
			StringAssert.Contains(level.levelId == "Level3" ? "dung nham" : "axit", level.hazardDeathMessage, level.levelId);
			Open(level);
			foreach (HazardVolume hazard in Object.FindObjectsOfType<HazardVolume>())
				Assert.AreEqual(level.hazardDeathMessage, hazard.cause, level.levelId + " " + hazard.name);
		}
	}

	[Test]
	public void KillPlaneUnderEveryLevel() {
		foreach (LevelConfig level in LevelCatalog.All) {
			Open(level);
			GameObject plane = GameObject.Find("Kill Plane");
			Assert.IsNotNull(plane, level.levelId);
			Bounds kill = plane.GetComponent<Collider>().bounds;
			foreach (Collider c in Object.FindObjectsOfType<Collider>().Where(c => !c.isTrigger && c.attachedRigidbody == null)) {
				Assert.Less(kill.max.y, c.bounds.min.y, level.levelId + ": kill plane must be under " + c.name);
				Assert.IsTrue(kill.min.x <= c.bounds.min.x && kill.max.x >= c.bounds.max.x && kill.min.z <= c.bounds.min.z && kill.max.z >= c.bounds.max.z,
					level.levelId + ": kill plane must cover " + c.name);
			}
		}
	}

	// From the start, walk outwards in 16 directions over walkable ground. Where the ground ends (and it is not a
	// steep wall) the robot falls: the ground beyond must be under the sea surface, inside the death volume.
	[Test]
	public void ShoreAlwaysEndsInHazard() {
		foreach (LevelConfig level in LevelCatalog.All) {
			Open(level);
			Bounds sea = Sea().GetComponent<Collider>().bounds;
			Vector3 start = Start();
			int shores = 0;
			for (int d = 0; d < 16; d++) {
				Vector3 dir = Quaternion.Euler(0f, d * 22.5f, 0f) * Vector3.forward;
				for (float r = 1f; r < 400f; r += 0.5f) {
					Vector3 p = start + dir * r;
					RaycastHit hit;
					bool ground = Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out hit, 400f, ~0, QueryTriggerInteraction.Ignore);
					if (ground && hit.point.y > sea.max.y) {
						if (hit.normal.y <= 0.7f)
							break;   // wall or cliff: the robot cannot roll on
						continue;
					}
					shores++;
					Assert.IsTrue(InHazard(new Vector3(p.x, sea.max.y - 0.3f, p.z)), level.levelId + " direction " + d + " at " + p + " has no hazard");
					break;
				}
			}
			Assert.Greater(shores, 0, level.levelId);
		}
	}

	[Test]
	public void NoHazardNearTheStart() {
		foreach (LevelConfig level in LevelCatalog.All) {
			Open(level);
			Bounds sea = Sea().GetComponent<Collider>().bounds;
			Vector3 start = Start();
			for (int d = 0; d < 16; d++) {
				Vector3 p = start + Quaternion.Euler(0f, d * 22.5f, 0f) * Vector3.forward * 6f;
				RaycastHit hit;
				Assert.IsTrue(Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out hit, 400f, ~0, QueryTriggerInteraction.Ignore), level.levelId);
				Assert.Greater(hit.point.y, sea.max.y + 0.1f, level.levelId + ": hazard within 6 m of the start");
			}
		}
	}

	[Test]
	public void ExpandedLevelsHaveMoreConnectedGround() {
		// Baseline walkable area (T0, spec §14.1): Level1 1021 m², Level2 2237 m²; targets ~1.5× and ~1.4×.
		foreach (var target in new[] { new { level = "Level1", area = 1021f * 1.45f }, new { level = "Level2", area = 2237f * 1.35f } }) {
			Open(LevelCatalog.Get(target.level));
			int area = WalkableGrid.Reachable(Start()).Count;
			Debug.Log("MapTests: " + target.level + " connected walkable " + area + " m2");
			Assert.GreaterOrEqual(area, target.area, target.level);
		}
	}

	[Test]
	public void LandingPointsAreReachableFromStart() {
		foreach (LevelConfig level in LevelCatalog.All) {
			Open(level);
			var reachable = WalkableGrid.Reachable(Start());
			foreach (Transform point in Object.FindObjectOfType<EnergySpawnDirector>().landingPoints) {
				var cell = new Vector2Int(Mathf.RoundToInt(point.position.x), Mathf.RoundToInt(point.position.z));
				Assert.IsTrue(reachable.ContainsKey(cell), level.levelId + " " + point.name + " at " + point.position + " cannot be reached");
			}
		}
	}
}
