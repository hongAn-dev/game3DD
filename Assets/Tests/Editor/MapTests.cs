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
			Assert.AreEqual(visual.transform.position.y, Sea().GetComponent<Collider>().bounds.max.y, 0.01f, level.levelId + ": death volume top = visible surface");
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

	// Whole shoreline: every reachable cell whose neighbour drops to (or under) the acid is a place the robot can roll
	// off. Beyond it the ground must be missing or inside the death volume, and the visible sea must cover the spot.
	[Test]
	public void ShoreAlwaysEndsInHazard() {
		var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
		foreach (LevelConfig level in LevelCatalog.All) {
			Open(level);
			Bounds sea = Sea().GetComponent<Collider>().bounds;
			Bounds visual = GameObject.Find("HazardVisual").GetComponent<Renderer>().bounds;
			var reachable = WalkableGrid.Reachable(Start());
			int shore = 0;
			foreach (Vector2Int cell in reachable.Keys) {
				foreach (Vector2Int step in steps) {
					Vector2Int next = cell + step;
					if (reachable.ContainsKey(next))
						continue;
					RaycastHit hit;
					bool ground = TopSurface(new Vector3(next.x, 0f, next.y), out hit);
					if (ground && hit.point.y > sea.max.y + 0.1f)
						continue;   // wall, ledge or prop: not a fall into the sea
					shore++;
					Assert.IsTrue(!ground || hit.point.y >= sea.min.y, level.levelId + ": ground under the death volume at " + next);
					Assert.IsTrue(InHazard(new Vector3(next.x, sea.max.y - 0.3f, next.y)), level.levelId + ": no death volume at " + next);
					Assert.IsTrue(visual.min.x <= next.x && next.x <= visual.max.x && visual.min.z <= next.y && next.y <= visual.max.z,
						level.levelId + ": no visible acid/lava at " + next);
				}
			}
			Debug.Log("MapTests: " + level.levelId + " shore cells " + shore);
			Assert.Greater(shore, 50, level.levelId);
		}
	}

	static bool TopSurface(Vector3 p, out RaycastHit top) {
		top = new RaycastHit();
		bool found = false;
		foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, 300f, p.z), Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore))
			if (h.collider.attachedRigidbody == null && (!found || h.point.y > top.point.y)) {
				top = h;
				found = true;
			}
		return found;
	}

	// Spec §5: main paths at least ~3 robot diameters; the three Level2 bridges cross the channel at x -29.
	[Test]
	public void Level2BridgesAreWideEnough() {
		Open(LevelCatalog.Get("Level2"));
		var reachable = WalkableGrid.Reachable(Start());
		foreach (float centre in new[] { -15f, 0f, 15f }) {
			int run = 0, best = 0;
			for (float z = centre - 8f; z <= centre + 8f; z += 0.5f) {
				RaycastHit hit;
				run = WalkableGrid.Ground(-29f, z, out hit) ? run + 1 : 0;
				best = Mathf.Max(best, run);
			}
			Assert.GreaterOrEqual(best * 0.5f, 4f, "bridge at z " + centre + " dry width");
			Assert.IsTrue(reachable.ContainsKey(new Vector2Int(-29, Mathf.RoundToInt(centre))), "bridge at z " + centre + " is reachable from the start");
		}
		Assert.Greater(reachable.Keys.Count(k => k.x < -33), 400, "annex reachable");
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
