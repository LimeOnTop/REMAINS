using System.Collections.Generic;
using UnityEngine;
namespace Remains {
public class BlockoutDrawbridge : MonoBehaviour {
    public Transform leaf;
    public Transform[] attachments, pulleys, chainRoots, drums, controls;
    public float angle;
    public bool raised;
    public bool manual;                       // driven by DrawbridgeWinch (capstans) instead of the F toggle
    public const float MaxAngle=78f, PulleyRadius=.85f;
    public bool NearControl(Vector3 p){foreach(var t in controls)if(Vector3.Distance(p,t.position)<4.5f)return true;return false;}
    public void Toggle(){raised=!raised;}
    void Update(){if(manual)return;float next=Mathf.MoveTowards(angle,raised?MaxAngle:0,15*Time.deltaTime);if(next!=angle)ApplyPose(next);}
    public List<Vector3> ChainPath(int side){
        var a=attachments[side].position;var c=pulleys[side].position;
        var d=new Vector2(a.z-c.z,a.y-c.y);float theta=Mathf.Atan2(d.y,d.x)+Mathf.Acos(Mathf.Clamp(PulleyRadius/d.magnitude,-1,1));
        theta=Mathf.Repeat(theta,2*Mathf.PI);if(theta>Mathf.PI)theta-=2*Mathf.PI;
        // Upper exterior tangent leads over the wheel, then descends on its rear edge.
        if(Mathf.Sin(theta)<0)theta=Mathf.Atan2(d.y,d.x)-Mathf.Acos(Mathf.Clamp(PulleyRadius/d.magnitude,-1,1));
        theta=Mathf.Repeat(theta,2*Mathf.PI);
        var path=new List<Vector3>{a};
        for(int j=0;j<=20;j++){float t=Mathf.Lerp(theta,0,j/20f);path.Add(c+new Vector3(0,PulleyRadius*Mathf.Sin(t),PulleyRadius*Mathf.Cos(t)));}
        var drum=drums[side].position;path.Add(new Vector3(c.x,drum.y,c.z+PulleyRadius));
        // Quarter-turn onto the winding drum, ending in the wound chain.
        for(int j=1;j<=12;j++){float t=Mathf.PI-Mathf.PI*.5f*j/12;path.Add(drum+new Vector3(0,.8f*Mathf.Sin(t),.8f*Mathf.Cos(t)));}
        return path;
    }
    public void ApplyPose(float degrees){
        angle=Mathf.Clamp(degrees,0,MaxAngle);leaf.localRotation=Quaternion.Euler(angle,0,0);
        // Legacy kinematic chains only; the physics chains (DrawbridgeChain) follow the leaf themselves.
        if(pulleys==null||pulleys.Length<2||!pulleys[0]||!pulleys[0].gameObject.activeInHierarchy){Physics.SyncTransforms();return;}
        for(int s=0;s<2;s++){
            var path=ChainPath(s);float total=0;for(int j=1;j<path.Count;j++)total+=Vector3.Distance(path[j-1],path[j]);
            int count=Mathf.Min(chainRoots[s].childCount,Mathf.CeilToInt(total/.25f));int segment=1;float before=0;
            for(int k=0;k<chainRoots[s].childCount;k++){
                var link=chainRoots[s].GetChild(k);link.gameObject.SetActive(k<count);if(k>=count)continue;
                float distance=total*(k+.5f)/count;
                while(segment<path.Count-1&&before+Vector3.Distance(path[segment-1],path[segment])<distance){before+=Vector3.Distance(path[segment-1],path[segment]);segment++;}
                Vector3 direction=(path[segment]-path[segment-1]).normalized;float len=Vector3.Distance(path[segment-1],path[segment]);
                link.position=Vector3.Lerp(path[segment-1],path[segment],(distance-before)/Mathf.Max(len,.0001f));link.rotation=Quaternion.LookRotation(Vector3.Cross(Vector3.right,direction),direction)*Quaternion.Euler(0,k%2==0?0:90,0);
            }
            // Wound links and spokes turn with the shared drive action.
            drums[s].localRotation=Quaternion.Euler(angle*5,0,0);
        }
        Physics.SyncTransforms();
    }
}
}
