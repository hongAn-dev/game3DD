using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ThemeEditorTests {

	static GameObject Model() {
		return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyNatureKit/Models/stone_largeA.fbx");
	}

	[Test]
	public void ReplaceVisualKeepsTrailRenderer() {
		GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		root.AddComponent<TrailRenderer>();

		AssetReplacer.ReplaceVisual(root, Model(), false);

		Assert.IsNotNull(root.GetComponent<TrailRenderer>());
		Assert.IsNull(root.GetComponent<MeshRenderer>());
		Object.DestroyImmediate(root);
	}

	[Test]
	public void ReplaceVisualKeepsConvexFlag() {
		GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
		Object.DestroyImmediate(root.GetComponent<BoxCollider>());
		root.AddComponent<MeshCollider>().convex = true;

		GameObject model = AssetReplacer.ReplaceVisual(root, Model(), false);

		MeshCollider[] colliders = model.GetComponentsInChildren<MeshCollider>();
		Assert.IsNotEmpty(colliders);
		foreach (MeshCollider collider in colliders)
			Assert.IsTrue(collider.convex);
		Object.DestroyImmediate(root);
	}
}
