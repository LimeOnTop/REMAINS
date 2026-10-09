using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Remains {
 public enum SiegeShot { Bolt, Stone }
 public class SiegeTarget : MonoBehaviour {
  public enum TargetKind { Chain, Gate, Wall }
  public TargetKind kind; public int hitsRequired=3, hits; public SiegeBridge bridge; public int side;
  public BlockoutGate gate; public Transform crossbar; public bool Broken=>hits>=hitsRequired;
  public float Health=>Mathf.Clamp01(1-hits/(float)hitsRequired);
  public bool Hit(SiegeShot shot,Vector3 point,Vector3 direction){
   if(Broken||kind==TargetKind.Gate)return false;
   if(kind==TargetKind.Chain&&shot==SiegeShot.Bolt){hits++;SiegeEffects.Puff(point,.5f,5);if(Broken)bridge.BreakChain(side);return true;}
   return false;
  }
  public bool Ram(float speed,float runup,Vector3 point){
   if(kind!=TargetKind.Gate||Broken||speed<2.8f||runup<5)return false;
   hits++;StopAllCoroutines();StartCoroutine(GateReaction());SiegeEffects.Puff(point,1,8);return true;
  }
  IEnumerator GateReaction(){
   if(Broken){
    if(crossbar){foreach(var r in crossbar.GetComponentsInChildren<Renderer>())SiegeEffects.Fragment(r.gameObject,new Vector3(Random.Range(-2,2),3,2));crossbar=null;}
    foreach(var c in GetComponents<Collider>())c.enabled=false;
    gate.open=true;gate.enabled=true;yield break;
   }
   for(float t=0;t<.65f;t+=Time.deltaTime){float angle=Mathf.Sin(t/.65f*Mathf.PI*3)*Mathf.Exp(-t*5)*(2+hits*.35f);gate.leftHinge.localRotation=Quaternion.Euler(0,-angle,0);gate.rightHinge.localRotation=Quaternion.Euler(0,angle,0);yield return null;}
   gate.leftHinge.localRotation=gate.rightHinge.localRotation=Quaternion.identity;
  }
 }
 public static class SiegeEffects {
  static Material dust;
  public static void Puff(Vector3 p,float size,int count){
   if(!dust){dust=new Material(Shader.Find("Universal Render Pipeline/Lit"));dust.color=new Color(.62f,.55f,.41f);}
   for(int i=0;i<count;i++){var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);Object.Destroy(g.GetComponent<Collider>());g.name="Brief_Dust";g.transform.position=p+Random.insideUnitSphere*size*.45f;g.transform.localScale=Vector3.one*Random.Range(.25f,.6f)*size;g.GetComponent<Renderer>().sharedMaterial=dust;var f=g.AddComponent<SiegeFadingDebris>();f.life=Random.Range(.5f,1.05f);f.drift=new Vector3(Random.Range(-.7f,.7f),.65f,Random.Range(-.7f,.7f));}
  }
  public static void Fragment(GameObject g,Vector3 velocity){
   g.transform.SetParent(null,true);g.isStatic=false;var col=g.GetComponent<Collider>();if(!col){var m=g.GetComponent<MeshFilter>();if(m){var b=g.AddComponent<BoxCollider>();b.center=m.sharedMesh.bounds.center;b.size=m.sharedMesh.bounds.size;}}
   var rb=g.GetComponent<Rigidbody>();if(!rb)rb=g.AddComponent<Rigidbody>();rb.mass=8;rb.linearVelocity=velocity;rb.angularVelocity=Random.insideUnitSphere*3;g.AddComponent<SiegeFadingDebris>().life=5;
  }
 }
 public class SiegeFadingDebris:MonoBehaviour {
  public float life=1;public Vector3 drift;float elapsed;Vector3 scale;
  void Start(){scale=transform.localScale;}
  void Update(){elapsed+=Time.deltaTime;transform.position+=drift*Time.deltaTime;if(elapsed>life*.65f)transform.localScale=scale*Mathf.Clamp01((life-elapsed)/(life*.35f));if(elapsed>=life)Destroy(gameObject);}
 }
}
