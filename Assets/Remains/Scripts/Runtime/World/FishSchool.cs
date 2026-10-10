using UnityEngine;
namespace Remains {
// A small school swimming a scripted closed loop: each fish keeps its own lateral/vertical offset and a slow weave,
// and faces along the path. The tail wag itself is done in the UnderwaterSway shader.
public class FishSchool : MonoBehaviour {
    public Vector3[] path;            // closed loop, world space
    public float speed=2.2f;
    public Transform[] fish;
    public Vector3[] offsets;         // x: sideways, y: up, z: along the path (metres)
    public float[] phases;
    float[] cumulative;float length;float start;

    void Awake(){
        cumulative=new float[path.Length+1];
        for(int i=0;i<path.Length;i++)cumulative[i+1]=cumulative[i]+Vector3.Distance(path[i],path[(i+1)%path.Length]);
        length=cumulative[path.Length];start=Random.Range(0,length);
    }
    Vector3 At(float s){
        s=Mathf.Repeat(s,length);int i=0;while(i<path.Length-1&&cumulative[i+1]<s)i++;
        return Vector3.Lerp(path[i],path[(i+1)%path.Length],(s-cumulative[i])/Mathf.Max(1e-4f,cumulative[i+1]-cumulative[i]));
    }
    void Update(){
        float t=Time.time;float s0=start+t*speed;
        for(int k=0;k<fish.Length;k++){
            var f=fish[k];if(!f)continue;var o=offsets[k];float ph=phases[k];
            float s=s0+o.z;var p=At(s);var ahead=At(s+1.2f);var dir=(ahead-p);dir.y*=.3f;if(dir.sqrMagnitude<1e-6f)continue;dir.Normalize();
            var side=Vector3.Cross(Vector3.up,dir);
            float weave=Mathf.Sin(t*1.3f+ph)*.35f,bob=Mathf.Sin(t*.9f+ph*1.7f)*.12f;
            f.position=p+side*(o.x+weave)+Vector3.up*(o.y+bob);
            var look=dir+side*Mathf.Cos(t*1.3f+ph)*.25f;
            f.rotation=Quaternion.Slerp(f.rotation,Quaternion.LookRotation(look,Vector3.up),Time.deltaTime*6);
        }
    }
}
}
