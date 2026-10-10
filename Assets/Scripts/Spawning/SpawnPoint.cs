using System;
using UnityEngine;

[Flags]
public enum SpawnRisk { None = 0, Edge = 1, Enemy = 2, Trap = 4 }

/// <summary>Editor-authored candidate metadata. Runtime still revalidates ground, path, hazards and current reservations.</summary>
public class SpawnPoint : MonoBehaviour {
    public int regionId;
    public int clusterId;
    public SpawnRisk riskFlags;
    [System.NonSerialized] public SpawnRisk dynamicRiskFlags;
    public float edgeClearance;
    public Vector3 groundNormal = Vector3.up;
    public Vector3 escapeDirection;
    public SpawnRisk EffectiveRisk { get { return riskFlags | dynamicRiskFlags; } }
    public bool IsRisk { get { return EffectiveRisk != SpawnRisk.None; } }
}