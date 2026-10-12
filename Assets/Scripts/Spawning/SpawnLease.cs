using UnityEngine;

/// <summary>Releases a committed registry slot if a pickup is destroyed without its normal Gone callback.</summary>
public sealed class SpawnLease : MonoBehaviour {
    SpawnRegistry registry;
    int reservationId;
    public void Bind(SpawnRegistry owner, int id) { registry = owner; reservationId = id; }
    void OnDestroy() { if (registry != null) registry.Release(reservationId); }
}