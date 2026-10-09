using System.Collections.Generic;
using UnityEngine;
namespace Remains {
// One lifting chain of the drawbridge. Outside the gatehouse the chain is a string of physical link pairs joined to the
// bridge leaf and to a feed anchor in the wall hole; inside, the same links ride a fixed path (over the ceiling pulley,
// down to the floor pulley and onto the capstan drum) as kinematic bodies. Raising the bridge pulls links through the
// hole one by one, lowering pays them out, so the outside length always matches the bridge pose.
public class DrawbridgeChain : MonoBehaviour {
    public BlockoutDrawbridge bridge;
    public Rigidbody leafBody;
    public Transform attachment;           // on the leaf, where the last link is pinned
    public Transform capstanHead;          // turns as chain winds onto the drum
    public Vector3[] innerPath;            // world points from the hole outward face inward to the drum tangent
    public Vector3 drumCenter;             // world, at the height where the chain meets the drum
    public float drumRadius=.66f, coilPitch=.16f;
    public GameObject linkPairPrefab;
    public int pairs=64;
    public const float Pitch=.64f;
    public bool broken;
    readonly List<Rigidbody> bodies=new List<Rigidbody>();
    readonly List<Collider> colliders=new List<Collider>();
    float[] cumulative;
    float straightLength;
    int outside=-1;
    Rigidbody feed;
    float spooled;

    public float InsideLength=>pairs*Pitch-OutsideTarget();
    public float DrumAngle=>Mathf.Max(0,spooled-straightLength)/drumRadius;

    void Awake(){
        cumulative=new float[innerPath.Length];
        for(int i=1;i<innerPath.Length;i++)cumulative[i]=cumulative[i-1]+Vector3.Distance(innerPath[i-1],innerPath[i]);
        straightLength=cumulative[cumulative.Length-1];
        var f=new GameObject("Chain_Feed_Anchor");f.transform.SetParent(transform,false);feed=f.AddComponent<Rigidbody>();feed.isKinematic=true;feed.transform.position=innerPath[0];
        var walker=FindAnyObjectByType<CastleWalkthrough>();var player=walker?walker.GetComponent<Collider>():null;
        var leafColliders=leafBody.GetComponentsInChildren<Collider>();
        for(int i=0;i<pairs;i++){
            var g=Instantiate(linkPairPrefab,transform);g.name="Chain_Link_Pair_"+i;g.SetActive(true);
            var rb=g.GetComponent<Rigidbody>();if(!rb)rb=g.AddComponent<Rigidbody>();
            rb.mass=6;rb.linearDamping=.4f;rb.angularDamping=1.5f;rb.solverIterations=24;rb.solverVelocityIterations=8;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.isKinematic=true;
            var c=g.GetComponent<CapsuleCollider>();if(!c){c=g.AddComponent<CapsuleCollider>();c.direction=2;c.radius=.12f;c.height=Pitch+.24f;}
            if(player)Physics.IgnoreCollision(c,player);
            foreach(var lc in leafColliders)Physics.IgnoreCollision(c,lc);
            bodies.Add(rb);colliders.Add(c);
        }
        for(int i=0;i<colliders.Count;i++)for(int j=i+1;j<Mathf.Min(colliders.Count,i+3);j++)Physics.IgnoreCollision(colliders[i],colliders[j]);
        Relayout(true);
    }

    // Target length of chain outside the hole: straight distance to the leaf eye plus a little slack.
    float OutsideTarget(){return broken?Mathf.Min(pairs*Pitch,12f):Vector3.Distance(innerPath[0],attachment.position)*1.004f+.02f;}

    // Point at arc length s inward from the hole; beyond the straight path the chain coils up the drum.
    public Vector3 PathPoint(float s,out Vector3 inward){
        if(s<=0){inward=(innerPath[1]-innerPath[0]).normalized;return innerPath[0]+inward*s;}
        if(s<straightLength){int i=1;while(i<cumulative.Length-1&&cumulative[i]<s)i++;var pa=innerPath[i-1];var pb=innerPath[i];inward=(pb-pa).normalized;return Vector3.Lerp(pa,pb,(s-cumulative[i-1])/Mathf.Max(1e-4f,cumulative[i]-cumulative[i-1]));}
        var end=innerPath[innerPath.Length-1];var start=end-drumCenter;start.y=0;float a0=Mathf.Atan2(start.x,start.z);
        float extra=s-straightLength;float turn=extra/drumRadius;float a=a0+turn*Spin;
        var p=new Vector3(drumCenter.x+Mathf.Sin(a)*drumRadius,end.y+coilPitch*turn/(2*Mathf.PI),drumCenter.z+Mathf.Cos(a)*drumRadius);
        inward=new Vector3(Mathf.Cos(a)*Spin,coilPitch/(2*Mathf.PI*drumRadius),-Mathf.Sin(a)*Spin).normalized;return p;
    }
    // Winding sense that continues the last straight segment tangentially around the drum.
    float Spin{get{var d=innerPath[innerPath.Length-1]-innerPath[innerPath.Length-2];var r=innerPath[innerPath.Length-1]-drumCenter;return Mathf.Sign(r.z*d.x-r.x*d.z);}}

    void FixedUpdate(){if(bodies.Count!=pairs)return;Relayout(false);Solve();}

    // Joint chains of this length stretch under gravity, so link spacing is also enforced positionally each step:
    // the hole anchor and the leaf eye are fixed, every pitch is at most Pitch long (a chain can go slack, not stretch).
    Vector3 feedTarget;
    void Solve(){
        int n=outside;if(n<1)return;int first=pairs-n;var p=new Vector3[n+1];
        p[0]=feedTarget;
        for(int i=1;i<n;i++){var A=bodies[first+i-1];var B=bodies[first+i];p[i]=(A.position+A.rotation*Vector3.forward*(Pitch/2)+B.position-B.rotation*Vector3.forward*(Pitch/2))*.5f;}
        var last=bodies[pairs-1];p[n]=broken?last.position+last.rotation*Vector3.forward*(Pitch/2):attachment.position;
        for(int it=0;it<20;it++)for(int i=0;i<n;i++){
            var d=p[i+1]-p[i];float len=d.magnitude;if(len<=Pitch)continue;var corr=d*(1-Pitch/len);
            bool fa=i==0,fb=i+1==n&&!broken;if(fa&&fb)continue;
            if(fa)p[i+1]-=corr;else if(fb)p[i]+=corr;else{p[i]+=corr*.5f;p[i+1]-=corr*.5f;}
        }
        for(int i=0;i<n;i++){var rb=bodies[first+i];var d=p[i+1]-p[i];if(d.sqrMagnitude<1e-6f)continue;
            rb.position=(p[i]+p[i+1])*.5f;rb.rotation=Quaternion.LookRotation(d,rb.rotation*Vector3.up);}
    }

    void Relayout(bool initial){
        float outLen=Mathf.Clamp(OutsideTarget(),Pitch,pairs*Pitch-Pitch);
        int n=Mathf.Clamp(Mathf.CeilToInt(outLen/Pitch),2,pairs-1);
        spooled=pairs*Pitch-outLen;
        // The feed anchor sits just inside the hole so the outside links add up to exactly outLen.
        float feedS=n*Pitch-outLen;
        feedTarget=PathPoint(feedS,out _);feed.MovePosition(feedTarget);
        int first=pairs-n;
        for(int k=0;k<first;k++){
            float s=feedS+(first-k-.5f)*Pitch;var p=PathPoint(s,out var inward);
            var rb=bodies[k];if(!rb.isKinematic){rb.isKinematic=true;}colliders[k].enabled=false;
            var rot=Quaternion.LookRotation(-inward,Vector3.up);
            if(initial){rb.transform.SetPositionAndRotation(p,rot);}else{rb.MovePosition(p);rb.MoveRotation(rot);}
        }
        if(n!=outside||initial){
            if(initial){var a=PathPoint(feedS,out _);var b=attachment.position;for(int k=first;k<pairs;k++){float t=(k-first+.5f)/n;bodies[k].transform.SetPositionAndRotation(Vector3.Lerp(a,b,t),Quaternion.LookRotation(b-a,Vector3.up));}}
            Rejoin(first);outside=n;
        }
        if(capstanHead)capstanHead.localRotation=Quaternion.Euler(0,DrumAngle*Mathf.Rad2Deg*Spin,0);
    }

    void Rejoin(int first){
        foreach(var rb in bodies)foreach(var j in rb.GetComponents<ConfigurableJoint>())Destroy(j);
        for(int k=first;k<pairs;k++){
            var rb=bodies[k];rb.isKinematic=false;colliders[k].enabled=true;
            if(k==first)Joint(rb,new Vector3(0,0,-Pitch/2),feed,Vector3.zero);
            if(k<pairs-1)Joint(rb,new Vector3(0,0,Pitch/2),bodies[k+1],new Vector3(0,0,-Pitch/2));
            else if(!broken)Joint(rb,new Vector3(0,0,Pitch/2),leafBody,leafBody.transform.InverseTransformPoint(attachment.position));
        }
    }
    static void Joint(Rigidbody a,Vector3 anchor,Rigidbody b,Vector3 connectedAnchor){
        var j=a.gameObject.AddComponent<ConfigurableJoint>();j.autoConfigureConnectedAnchor=false;j.connectedBody=b;j.anchor=anchor;j.connectedAnchor=connectedAnchor;
        j.xMotion=j.yMotion=j.zMotion=ConfigurableJointMotion.Locked;j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Free;j.enableCollision=false;
    }

    // Siege: a broken chain lets go of the bridge and hangs from the wall hole.
    public void Break(){if(broken)return;broken=true;outside=-1;}
    public Vector3 Midpoint=>bodies.Count>0?bodies[Mathf.Clamp(pairs-Mathf.Max(outside,2)/2,0,pairs-1)].position:transform.position;
}
}
