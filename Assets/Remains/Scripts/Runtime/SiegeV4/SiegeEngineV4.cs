using System;
using System.Collections;
using UnityEngine;
namespace Remains {
 public enum SiegeEngineKind { Ballista, Ram, Catapult }
 public class SiegeEngineV4:MonoBehaviour {
  public SiegeEngineKind kind;public GameObject ammunition;public Transform muzzle;public float speed,runup;public float cooldown;public int fired;
  public bool aiming;public float gripSide=1;public string Title=>kind==SiegeEngineKind.Ballista?"БАЛЛИСТА":kind==SiegeEngineKind.Ram?"ТАРАН":"КАТАПУЛЬТА";
  public Vector3 Grip=>transform.TransformPoint(kind==SiegeEngineKind.Ram?new Vector3(gripSide*2.25f,0,-.8f):new Vector3(0,0,kind==SiegeEngineKind.Ballista?-3f:-3.4f));
  public bool CanGrab(Vector3 p){var q=transform.InverseTransformPoint(p);if(kind==SiegeEngineKind.Ram){gripSide=q.x<0?-1:1;return Mathf.Abs(q.x)>1.2f&&Mathf.Abs(q.x)<4&&Mathf.Abs(q.z)<3.7f;}return q.z< -1.7f&&q.z> -5.5f&&Mathf.Abs(q.x)<2.7f;}
  Transform armPivot;Quaternion armRest;
  void Start(){if(kind!=SiegeEngineKind.Catapult)return;var parts=GetComponentsInChildren<Transform>();armPivot=new GameObject("Throwing_Arm_Pivot").transform;armPivot.SetParent(transform,false);armPivot.localPosition=new Vector3(0,2.2f,.2f);foreach(var t in parts)if(t.name=="Spoon_Arm"||t.name=="Spoon_Bowl")t.SetParent(armPivot,true);armRest=armPivot.localRotation;}
  IEnumerator ThrowAnimation(){if(!armPivot)yield break;for(float t=0;t<.45f;t+=Time.deltaTime){armPivot.localRotation=armRest*Quaternion.Euler(-70*Mathf.Sin(t/.45f*Mathf.PI*.5f),0,0);yield return null;}for(float t=0;t<1.5f;t+=Time.deltaTime){armPivot.localRotation=armRest*Quaternion.Euler(Mathf.Lerp(-70,0,t/1.5f),0,0);yield return null;}armPivot.localRotation=armRest;}
  void Update(){cooldown=Mathf.Max(0,cooldown-Time.deltaTime);}
  public bool GroundAt(Vector3 position,Transform player,out float y){
   var hits=Physics.RaycastAll(position+Vector3.up*4,Vector3.down,9,~0,QueryTriggerInteraction.Ignore);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
   foreach(var h in hits){if(h.transform.IsChildOf(transform)||(player&&h.transform.IsChildOf(player))||h.normal.y<.60f)continue;y=h.point.y;return y>8;}
   y=position.y;return false;
  }
  public void Drive(float forward,float steer,Transform player,float dt){
   float max=kind==SiegeEngineKind.Catapult?1.05f:kind==SiegeEngineKind.Ram?4.3f:2.5f;
   float target=aiming?0:forward*(forward<0?1.8f:max);speed=Mathf.MoveTowards(speed,target,(kind==SiegeEngineKind.Ram?1.0f:1.8f)*dt);
   if(forward<=0||aiming)runup=0;
   if(!aiming&&Mathf.Abs(steer)>.01f)transform.Rotate(0,steer*25*dt,0);
   var delta=transform.forward*speed*dt;float length=kind==SiegeEngineKind.Ram?3.5f:2.5f;
   var hits=Physics.BoxCastAll(transform.position+Vector3.up*1.6f,new Vector3(1.1f,1.05f,length),delta.normalized,transform.rotation,delta.magnitude+.08f,~0,QueryTriggerInteraction.Ignore);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));bool blocked=false;
   foreach(var h in hits){if(h.transform.IsChildOf(transform)||(player&&h.transform.IsChildOf(player))||h.normal.y>.55f)continue;var gate=h.collider.GetComponentInParent<SiegeTarget>();if(kind==SiegeEngineKind.Ram&&speed>0&&gate&&gate.kind==SiegeTarget.TargetKind.Gate)gate.Ram(speed,runup,h.point);blocked=true;break;}
   if(blocked){speed=0;runup=0;return;}
   var next=transform.position+delta;if(!GroundAt(next,player,out float y)||Mathf.Abs(y-transform.position.y)>.7f){speed=0;runup=0;return;}
   // Both axles need support; engines cannot cross water or the raised bridge gap.
   if(!GroundAt(next+transform.forward*1.7f,player,out float front)||!GroundAt(next-transform.forward*1.7f,player,out float back)||Mathf.Abs(front-back)>2){speed=0;runup=0;return;}
   next.y=y+.025f;transform.position=next;if(speed>0)runup+=delta.magnitude;
  }
  public static bool Ballistic(Vector3 origin,Vector3 target,float launchSpeed,bool high,out Vector3 velocity){
   var flat=target-origin;float h=flat.y;flat.y=0;float x=flat.magnitude,g=-Physics.gravity.y,v2=launchSpeed*launchSpeed;float disc=v2*v2-g*(g*x*x+2*h*v2);
   if(x<.1f||disc<0){velocity=Vector3.zero;return false;}float angle=Mathf.Atan((v2+(high?1:-1)*Mathf.Sqrt(disc))/(g*x));velocity=flat.normalized*(Mathf.Cos(angle)*launchSpeed)+Vector3.up*(Mathf.Sin(angle)*launchSpeed);return true;
  }
  public bool AimVelocity(Camera camera,Transform player,out Vector3 velocity){
   Vector3 target=camera.transform.position+camera.transform.forward*120;
   var hits=Physics.RaycastAll(camera.transform.position,camera.transform.forward,230,~0,QueryTriggerInteraction.Collide);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));foreach(var h in hits){if(h.transform.IsChildOf(transform)||h.transform.IsChildOf(player)||h.collider.GetComponent<SiegeProjectile>())continue;target=h.point;break;}
   var flat=target-muzzle.position;flat.y=0;if(Vector3.Angle(transform.forward,flat)>65){velocity=Vector3.zero;return false;}
   return Ballistic(muzzle.position,target,kind==SiegeEngineKind.Ballista?75:38,kind==SiegeEngineKind.Catapult,out velocity);
  }
  public bool Fire(Camera camera,Transform player){if(kind==SiegeEngineKind.Ram||cooldown>0||!aiming||!AimVelocity(camera,player,out var v))return false;
   var g=Instantiate(ammunition,muzzle.position,Quaternion.LookRotation(v));g.SetActive(true);foreach(var c in g.GetComponentsInChildren<Collider>())Destroy(c);var p=g.AddComponent<SiegeProjectile>();p.owner=transform;p.player=player;p.shot=kind==SiegeEngineKind.Ballista?SiegeShot.Bolt:SiegeShot.Stone;p.velocity=v;cooldown=kind==SiegeEngineKind.Ballista?2.5f:6;fired++;if(kind==SiegeEngineKind.Catapult)StartCoroutine(ThrowAnimation());return true;
  }
 }
}
