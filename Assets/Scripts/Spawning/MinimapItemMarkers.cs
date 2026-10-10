using System.Collections.Generic;
using UnityEngine;

/// <summary>Projects nearby energy and harmful pickup icons into the existing top-down minimap camera.</summary>
public sealed class MinimapItemMarkers : MonoBehaviour {
    public Sprite energyIcon;
    public Sprite poisonIcon, antiEnergyIcon, magneticIcon;
    public float nearbyDistance = 12f;
    public float iconSize = 0.62f;
    const int MarkerLayer = 30;
    sealed class Marker { public GameObject go; public SpriteRenderer renderer; }
    readonly Dictionary<int, Marker> markers = new Dictionary<int, Marker>();
    Camera mapCamera;
    Transform player;
    float timer;

    void Awake() {
        mapCamera=GetComponent<Camera>();
        if(mapCamera!=null)mapCamera.cullingMask|=1<<MarkerLayer;
        if(Camera.main!=null)Camera.main.cullingMask&=~(1<<MarkerLayer);
        GameObject p=GameObject.FindWithTag("Player"); player=p!=null?p.transform:null;
    }
    void OnDestroy() { foreach(Marker m in markers.Values)if(m.go!=null)Destroy(m.go); markers.Clear(); }
    void Update() {
        if(!GameFlow.IsGameplayActive) {
            foreach(Marker marker in markers.Values)if(marker.go!=null)marker.go.SetActive(false);
            return;
        }
        timer+=Time.deltaTime;if(timer<.2f)return;timer=0f;
        if(player==null){GameObject p=GameObject.FindWithTag("Player");player=p!=null?p.transform:null;}
        if(player==null)return;
        var seen=new HashSet<int>();
        foreach(Treasure item in FindObjectsOfType<Treasure>()) {
            if(item==null)continue;
            Collider trigger=item.GetComponent<Collider>(); if(trigger==null)trigger=item.GetComponentInChildren<Collider>();
            if(trigger==null||!trigger.enabled)continue;
            Sprite icon=energyIcon;
            UpdateMarker(item.transform,item.GetInstanceID(),icon,new Color(.18f,.95f,1f,1f),seen);
        }
        foreach(HarmfulPickup item in FindObjectsOfType<HarmfulPickup>()) {
            if(item==null)continue;
            Collider trigger=item.GetComponent<Collider>(); if(trigger==null)trigger=item.GetComponentInChildren<Collider>();
            if(trigger==null||!trigger.enabled)continue;
            Sprite icon=item.kind==HarmfulKind.Poison?poisonIcon:item.kind==HarmfulKind.AntiEnergy?antiEnergyIcon:magneticIcon;
            Color color=item.kind==HarmfulKind.Poison?new Color(1f,.75f,.12f):item.kind==HarmfulKind.AntiEnergy?new Color(1f,.28f,.18f):new Color(.75f,.35f,1f);
            UpdateMarker(item.transform,item.GetInstanceID(),icon,color,seen);
        }
        var remove=new List<int>();
        foreach(var pair in markers)if(!seen.Contains(pair.Key))remove.Add(pair.Key);
        foreach(int id in remove){if(markers[id].go!=null)Destroy(markers[id].go);markers.Remove(id);}
    }
    void UpdateMarker(Transform target,int id,Sprite icon,Color tint,HashSet<int> seen) {
        seen.Add(id);
        bool nearby=Vector3.Distance(player.position,target.position)<=nearbyDistance;
        Marker marker;
        if(!markers.TryGetValue(id,out marker)) {
            GameObject go=new GameObject("Minimap Item Marker");
            go.layer=MarkerLayer;
            marker=new Marker{go=go,renderer=go.AddComponent<SpriteRenderer>()};
            marker.renderer.sortingOrder=30000;
            markers.Add(id,marker);
        }
        marker.renderer.sprite=icon;marker.renderer.color=tint;
        marker.go.SetActive(nearby&&icon!=null);
        if(!nearby||icon==null)return;
        marker.go.transform.position=target.position+Vector3.up*1.5f;
        marker.go.transform.rotation=Quaternion.Euler(90f,0f,0f);
        marker.go.transform.localScale=Vector3.one*iconSize;
    }
}
