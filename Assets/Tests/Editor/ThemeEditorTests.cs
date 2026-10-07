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

	[Test]
	public void DecorationFootprintNotLarger() {
		GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
		root.transform.localScale = new Vector3(2, 1, 2);
		Bounds before = root.GetComponent<Renderer>().bounds;
		GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyCityKitIndustrial/Models/shipping-container-a.fbx");

		ZoneDresser.SwapDecoration(root, model);

		Bounds after = new Bounds(root.transform.position, Vector3.zero);
		foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
			after.Encapsulate(r.bounds);
		Assert.LessOrEqual(Mathf.Max(after.size.x, after.size.z), Mathf.Max(before.size.x, before.size.z) + 0.01f);
		Object.DestroyImmediate(root);
	}
}
