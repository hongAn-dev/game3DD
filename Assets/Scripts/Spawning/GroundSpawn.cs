using System.Collections;
using UnityEngine;

/// <summary>Common bounds-correct placement and scale-in activation for every pickup family.</summary>
public static class GroundSpawn {
    public const float MinGap = 0.075f;
    public const float ScaleInDuration = 0.18f;

    public static bool PlaceAtGround(Transform item, Vector3 candidate, Collider trigger, out RaycastHit ground) {
        ground = default(RaycastHit);
        if (item == null) return false;
        RaycastHit[] hits = Physics.RaycastAll(candidate + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < hits.Length; i++) {
            if (hits[i].collider == null || hits[i].collider.attachedRigidbody != null) continue;
            if (!found || hits[i].point.y > ground.point.y) { ground = hits[i]; found = true; }
        }
        if (!found || ground.normal.y < 0.7f || Mathf.Abs(ground.point.y-candidate.y)>0.35f
            || WalkableGround.InHazard(ground.point - Vector3.up * 0.05f)) return false;
        Bounds bounds;
        if (!TryBounds(item, out bounds)) return false;
        item.position += Vector3.up * (ground.point.y + MinGap - bounds.min.y);
        foreach (GroundRingVisual ring in item.GetComponentsInChildren<GroundRingVisual>(true)) {
            Vector3 p = ring.transform.position;
            ring.transform.position = new Vector3(p.x, ground.point.y + .02f, p.z);
        }
        if (trigger != null) trigger.enabled = false;
        return true;
    }

    public static IEnumerator ScaleIn(Transform item, Collider trigger, float duration, System.Action activated = null) {
        if (item == null) yield break;
        Vector3 full = item.localScale;
        Bounds fullBounds;
        bool keepGrounded = TryBounds(item, out fullBounds);
        float groundY = keepGrounded ? fullBounds.min.y : 0f;
        float time = 0f;
        item.localScale = Vector3.zero;
        while (time < duration && item != null) {
            if (GameFlow.IsGameplayActive) time += Time.deltaTime;
            item.localScale = full * Mathf.Clamp01(time / Mathf.Max(0.001f, duration));
            // Scaling around a centered pivot lifts the visible base off the floor. Keep the
            // renderer's lower bound at its placed height throughout the scale-in.
            if (keepGrounded) KeepBoundsMinAt(item, groundY);
            yield return null;
        }
        if (item == null) yield break;
        item.localScale = full;
        if (keepGrounded) KeepBoundsMinAt(item, groundY);
        if (!GameFlow.IsGameplayActive) yield break;
        if (trigger != null) trigger.enabled = true;
        if (activated != null) activated();
    }

    static void KeepBoundsMinAt(Transform item, float groundY) {
        Bounds bounds;
        if (TryBounds(item, out bounds))
            item.position += Vector3.up * (groundY - bounds.min.y);
    }

    public static float FootprintRadius(GameObject prefab) {
        if (prefab == null) return 0.5f;
        Collider c = prefab.GetComponent<Collider>();
        if (c is SphereCollider) {
            SphereCollider sphere=(SphereCollider)c;
            return sphere.radius*Mathf.Max(prefab.transform.lossyScale.x,prefab.transform.lossyScale.z);
        }
        if (c is CapsuleCollider) {
            CapsuleCollider capsule=(CapsuleCollider)c;
            return capsule.radius*Mathf.Max(prefab.transform.lossyScale.x,prefab.transform.lossyScale.z);
        }
        if (c is BoxCollider) {
            BoxCollider box=(BoxCollider)c; Vector3 size=Vector3.Scale(box.size,prefab.transform.lossyScale);
            return Mathf.Max(size.x,size.z)*.5f;
        }
        Bounds b;
        if (!TryBounds(prefab.transform, out b)) return 0.5f;
        return Mathf.Max(b.extents.x, b.extents.z);
    }

    public static bool TryBounds(Transform root, out Bounds bounds) {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds();
        bool any = false;
        foreach (Renderer r in renderers) {
            if (r.GetComponentInParent<GroundRingVisual>() != null) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }
}
