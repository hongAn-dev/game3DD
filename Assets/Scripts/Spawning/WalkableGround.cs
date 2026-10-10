using UnityEngine;

/// <summary>Runtime ground/hazard queries shared by editor authoring and pickup placement.</summary>
public static class WalkableGround {
    public static bool InHazard(Vector3 point) {
        return InHazard(point, Object.FindObjectsOfType<HazardVolume>());
    }
    public static bool InHazard(Vector3 point, HazardVolume[] hazards) {
        foreach(HazardVolume hazard in hazards) {
            Collider c=hazard.GetComponent<Collider>();
            if(c!=null&&c.bounds.Contains(point))return true;
        }
        return false;
    }
    public static bool Ground(Vector3 point,out RaycastHit hit) {
        return Ground(point,out hit,null);
    }
    public static bool Ground(Vector3 point,out RaycastHit hit,HazardVolume[] hazards) {
        hit=new RaycastHit(); bool found=false;
        foreach(RaycastHit h in Physics.RaycastAll(point+Vector3.up*50f,Vector3.down,100f,~0,QueryTriggerInteraction.Ignore)) {
            if(h.collider.attachedRigidbody!=null||h.normal.y<=.7f)continue;
            if(!found||h.point.y>hit.point.y){hit=h;found=true;}
        }
        return found&&!(hazards==null?InHazard(hit.point-Vector3.up*.05f):InHazard(hit.point-Vector3.up*.05f,hazards));
    }
}
