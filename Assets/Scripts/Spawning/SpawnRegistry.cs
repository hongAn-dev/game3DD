using System.Collections.Generic;
using UnityEngine;

/// <summary>Level-local atomic reservation book. Each category has its own cap; every footprint blocks every other category.</summary>
public sealed class SpawnRegistry : MonoBehaviour {
    public enum SpawnCategory { Energy, Support, Harmful, EnemyTelegraph }
    sealed class Entry { public int id; public SpawnCategory category; public Object owner; public GameObject instance; public Vector3 point; public float radius; }
    static SpawnRegistry current;
    readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    int nextId = 1;
    const float MinimumSeparation = 1.5f;
    public static SpawnRegistry Current { get { return current; } }

    void Awake() { if (current == null || current == this) current = this; }
    void OnDestroy() { if (current == this) current = null; entries.Clear(); }

    public bool TryReserve(SpawnCategory category, Object owner, Vector3 position, float radius, int capacity, out int reservationId) {
        reservationId = 0;
        RemoveDestroyed();
        if (Count(category) >= Mathf.Max(0, capacity)) return false;
        float r = Mathf.Max(0.05f, radius);
        foreach (Entry entry in entries.Values) {
            float min = Mathf.Max(MinimumSeparation, r + entry.radius);
            Vector2 a = new Vector2(position.x, position.z), b = new Vector2(entry.point.x, entry.point.z);
            if ((a - b).sqrMagnitude < min * min) return false;
        }
        int id = nextId++;
        entries.Add(id, new Entry { id = id, category = category, owner = owner, point = position, radius = r });
        reservationId = id;
        return true;
    }

    /// <summary>Atomically trades one live entry for a pending entry of the same budgeted category.</summary>
    public bool TryReplaceAlive(GameObject oldInstance, SpawnCategory category, Object owner, Vector3 position,
        float radius, int capacity, out int reservationId) {
        reservationId = 0;
        if (oldInstance == null) return false;
        RemoveDestroyed();
        Entry oldEntry = null;
        foreach (Entry entry in entries.Values)
            if (entry.instance == oldInstance) { oldEntry = entry; break; }
        if (oldEntry == null) return false;
        int countAfterRelease = Count(category) - (oldEntry.category == category ? 1 : 0);
        if (countAfterRelease >= Mathf.Max(0, capacity)) return false;
        float r = Mathf.Max(0.05f, radius);
        foreach (Entry entry in entries.Values) {
            if (entry.id == oldEntry.id) continue;
            float min = Mathf.Max(MinimumSeparation, r + entry.radius);
            Vector2 a = new Vector2(position.x, position.z), b = new Vector2(entry.point.x, entry.point.z);
            if ((a - b).sqrMagnitude < min * min) return false;
        }
        entries.Remove(oldEntry.id);
        int id = nextId++;
        entries.Add(id, new Entry { id = id, category = category, owner = owner, point = position, radius = r });
        reservationId = id;
        return true;
    }

    public bool CommitAlive(int reservationId, GameObject instance) {
        Entry entry;
        if (instance == null || !entries.TryGetValue(reservationId, out entry) || entry.instance != null) return false;
        entry.instance = instance;
        entry.owner = null;
        entry.point = instance.transform.position;
        return true;
    }

    public void Release(int reservationId) { if (reservationId != 0) entries.Remove(reservationId); }
    public void UpdatePosition(GameObject instance, Vector3 position) {
        if(instance==null)return;
        foreach(Entry entry in entries.Values)if(entry.instance==instance){entry.point=position;return;}
    }
    public void ReleaseAlive(GameObject instance) {
        if (instance == null) return;
        int found = 0;
        foreach (Entry entry in entries.Values) if (entry.instance == instance) { found = entry.id; break; }
        if (found != 0) entries.Remove(found);
    }
    public void ReleaseOwner(Object owner) {
        if (owner == null) return;
        var remove = new List<int>();
        foreach (Entry entry in entries.Values) if (entry.owner == owner) remove.Add(entry.id);
        foreach (int id in remove) entries.Remove(id);
    }
    public int Count(SpawnCategory category) {
        int count = 0;
        foreach (Entry entry in entries.Values) if (entry.category == category) count++;
        return count;
    }
    public bool IsOccupied(Vector3 position, float radius, int exceptId = 0) {
        RemoveDestroyed();
        foreach (Entry entry in entries.Values) {
            if (entry.id == exceptId) continue;
            float min = Mathf.Max(MinimumSeparation, Mathf.Max(0.05f, radius) + entry.radius);
            Vector2 a = new Vector2(position.x, position.z), b = new Vector2(entry.point.x, entry.point.z);
            if ((a - b).sqrMagnitude < min * min) return true;
        }
        return false;
    }
    public List<Vector3> OccupiedPoints() {
        RemoveDestroyed();
        var result = new List<Vector3>();
        foreach (Entry e in entries.Values) result.Add(e.point);
        return result;
    }
    void RemoveDestroyed() {
        var remove = new List<int>();
        foreach (Entry entry in entries.Values) if (entry.instance == null && entry.owner == null) remove.Add(entry.id);
        foreach (int id in remove) entries.Remove(id);
    }
}
