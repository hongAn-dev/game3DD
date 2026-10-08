using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class HazardPlayTests {

	int deaths;

	void CountDeaths(FlowState state) {
		if (state == FlowState.Dead)
			deaths++;
	}

	IEnumerator Load(string level) {
		CampaignProgress.BeginRun(GameSettings.gameDifficulties.Normal);
		GameSettings.showIntroLevelMessage = false;
		SceneManager.LoadScene(level);
		yield return null;
		yield return null;
		deaths = 0;
		GameFlow.Changed += CountDeaths;
	}

	[TearDown]
	public void Unhook() {
		GameFlow.Changed -= CountDeaths;
	}

	static void Place(Vector3 position) {
		GameObject player = GameObject.FindWithTag("Player");
		Rigidbody body = player.GetComponent<Rigidbody>();
		body.velocity = Vector3.zero;
		body.position = position;
		player.transform.position = position;
	}

	// A point over open sea: far beyond every collider of the map, just above the sea surface.
	static Vector3 OverTheSea() {
		Bounds sea = Object.FindObjectsOfType<HazardVolume>().First(h => h.name == "DeathZone").GetComponent<Collider>().bounds;
		Bounds land = new Bounds(GameObject.FindWithTag("Player").transform.position, Vector3.zero);
		foreach (Collider c in Object.FindObjectsOfType<Collider>().Where(c => !c.isTrigger && c.attachedRigidbody == null))
			land.Encapsulate(c.bounds);
		return new Vector3(land.max.x + 5f, sea.max.y + 1f, land.center.z);
	}

	IEnumerator ExpectHazardDeath(string level) {
		yield return new WaitForSeconds(2f);
		Assert.AreEqual(FlowState.Dead, GameFlow.State, level);
		Assert.AreEqual(1, deaths, level + ": dies exactly once");
		Assert.AreEqual(LevelCatalog.Get(level).hazardDeathMessage, GameFlow.DeathCause, level);
	}

	[UnityTest]
	public IEnumerator AcidKillsOnceWithCause() {
		yield return Load("Level1");
		Place(OverTheSea());
		yield return ExpectHazardDeath("Level1");
	}

	[UnityTest]
	public IEnumerator LavaKillsWithLavaCause() {
		yield return Load("Level3");
		Place(OverTheSea());
		yield return ExpectHazardDeath("Level3");
		StringAssert.Contains("dung nham", GameFlow.DeathCause);
	}

	[UnityTest]
	public IEnumerator KillPlaneCatchesFallThrough() {
		yield return Load("Level2");
		Bounds kill = GameObject.Find("Kill Plane").GetComponent<Collider>().bounds;
		Place(new Vector3(kill.center.x, kill.max.y + 3f, kill.center.z));
		yield return ExpectHazardDeath("Level2");
	}

	// Spec §12 Hazard row: rolling off the shore in many directions always ends in death with the hazard cause.
	[UnityTest]
	public IEnumerator RollingOffTheShoreKills() {
		foreach (string level in new[] { "Level1", "Level2" }) {
			int tested = 0;
			for (int d = 0; d < 8; d++) {
				yield return Load(level);
				Vector3 edge;
				if (!LastDryPoint(d * 45f + 10f, out edge))
					continue;
				tested++;
				GameObject player = GameObject.FindWithTag("Player");
				Object.Destroy(player.GetComponent<BallUserControl>());   // no input, no braking
				Vector3 dir = Quaternion.Euler(0f, d * 45f + 10f, 0f) * Vector3.forward;
				Place(edge - dir * 1.5f + Vector3.up * 0.6f);
				player.GetComponent<Rigidbody>().velocity = dir * 6f;
				float end = Time.time + 4f;
				while (Time.time < end && GameFlow.State != FlowState.Dead)
					yield return null;
				Assert.AreEqual(FlowState.Dead, GameFlow.State, level + " direction " + d + " edge " + edge + " robot " + (player != null ? player.transform.position.ToString() : "gone"));
				Assert.AreEqual(LevelCatalog.Get(level).hazardDeathMessage, GameFlow.DeathCause, level + " direction " + d);
				GameFlow.Changed -= CountDeaths;
			}
			// Level2's ridge and machinery block half the straight lines from the start.
			Assert.GreaterOrEqual(tested, level == "Level1" ? 6 : 4, level);
		}
	}

	// The last dry ground point walking out from the start at yaw; false when a rising obstacle comes first.
	static bool LastDryPoint(float yaw, out Vector3 edge) {
		float sea = Object.FindObjectsOfType<HazardVolume>().First(h => h.name == "DeathZone").GetComponent<Collider>().bounds.max.y;
		Vector3 start = GameObject.FindWithTag("Player").transform.position;
		Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
		edge = start;
		float last = float.MaxValue;
		for (float r = 1f; r < 200f; r += 0.25f) {
			Vector3 p = start + dir * r;
			RaycastHit hit;
			// Level1's lowland around the start is only ~0.1 m above the acid, so "dry" means above the surface.
			if (!Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out hit, 600f, ~0, QueryTriggerInteraction.Ignore) || hit.point.y <= sea + 0.02f)
				return true;
			if (hit.collider.attachedRigidbody != null || (last != float.MaxValue && hit.point.y > last + 0.3f))
				return false;
			last = hit.point.y;
			edge = hit.point;
		}
		return false;
	}
}
