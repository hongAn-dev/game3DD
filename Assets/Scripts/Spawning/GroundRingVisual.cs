using UnityEngine;

/// <summary>Low-cost pulsing ground ring; one line renderer, no realtime light.</summary>
[RequireComponent(typeof(LineRenderer))]
public sealed class GroundRingVisual : MonoBehaviour {
    public Material material;
    public float radius=.62f;
    public float width=.035f;
    LineRenderer line;
    Vector3 baseScale;
    void Awake() {
        line=GetComponent<LineRenderer>(); line.useWorldSpace=false; line.loop=true; line.positionCount=33;
        line.startWidth=line.endWidth=width; line.alignment=LineAlignment.TransformZ;
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows=false; if(material!=null)line.sharedMaterial=material;
        for(int i=0;i<33;i++){float a=i*Mathf.PI*2f/32f;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0f,Mathf.Sin(a)*radius));}
        baseScale=transform.localScale;
    }
    void Update() {
        if(!GameFlow.IsGameplayActive)return;
        float pulse=.97f+.03f*(.5f+.5f*Mathf.Sin(Time.time*2.2f));
        transform.localScale=baseScale*pulse;
    }
}