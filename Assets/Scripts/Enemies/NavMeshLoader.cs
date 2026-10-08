using UnityEngine;
using UnityEngine.AI;

/// <summary>Adds this scene's baked NavMesh (built by EnemySetup) while enabled.</summary>
public class NavMeshLoader : MonoBehaviour {

	public NavMeshData data;

	NavMeshDataInstance instance;

	void OnEnable() {
		if (data != null)
			instance = NavMesh.AddNavMeshData(data);
	}

	void OnDisable() {
		instance.Remove();
	}
}
