using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMesh bake shared by EnemySetup (levels) and tests (arenas): static solid colliders are the sources, and every
/// HazardVolume is a NotWalkable box reaching half a metre above its surface, so agents never path into acid/lava.
/// One agent size fits both the creep and the boss paths.
/// </summary>
public static class NavMeshBake {

	public static NavMeshBuildSettings Settings() {
		NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
		settings.agentRadius = 0.5f;
		settings.agentHeight = 1.2f;
		settings.agentSlope = 40f;
		settings.agentClimb = 0.4f;
		settings.minRegionArea = 2f;
		return settings;
	}

	public static List<NavMeshBuildSource> Sources(Bounds area) {
		var sources = new List<NavMeshBuildSource>();
		NavMeshBuilder.CollectSources(area, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
		sources.RemoveAll(s => {
			Collider c = s.component as Collider;
			return c != null && (c.isTrigger || c.attachedRigidbody != null);
		});
		foreach (HazardVolume hazard in Object.FindObjectsOfType<HazardVolume>()) {
			Bounds b = hazard.GetComponent<Collider>().bounds;
			sources.Add(new NavMeshBuildSource {
				shape = NavMeshBuildSourceShape.ModifierBox,
				transform = Matrix4x4.Translate(b.center + Vector3.up * 0.25f),
				size = b.size + Vector3.up * 0.5f,
				area = 1,   // Not Walkable
			});
		}
		return sources;
	}

	public static NavMeshData Build(Bounds area) {
		return NavMeshBuilder.BuildNavMeshData(Settings(), Sources(area), area, Vector3.zero, Quaternion.identity);
	}
}
