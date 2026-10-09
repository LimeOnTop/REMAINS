using System;
using UnityEngine;
namespace Remains {
 public class SiegeProjectile:MonoBehaviour {
  public static string LastImpact;public SiegeShot shot;public Vector3 velocity;public Transform owner;public Transform player;float age;
  void FixedUpdate(){float dt=Time.fixedDeltaTime;var next=transform.position+velocity*dt+Physics.gravity*(.5f*dt*dt);var delta=next-transform.position;
   var hits=Physics.SphereCastAll(transform.position,shot==SiegeShot.Bolt?.12f:.45f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Collide);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
   foreach(var h in hits){if((owner&&h.transform.IsChildOf(owner))||(player&&h.transform.IsChildOf(player))||h.collider.GetComponentInParent<SiegeProjectile>())continue;
    LastImpact=h.collider.name+" at "+h.point;var target=h.collider.GetComponentInParent<SiegeTarget>();if(target)target.Hit(shot,h.point,velocity);
    var wall=h.collider.GetComponentInParent<SiegeWall>();if(wall&&shot==SiegeShot.Stone)wall.Blast(h.point,velocity);
    if(h.collider.isTrigger&&!target)continue;
    SiegeEffects.Puff(h.point,shot==SiegeShot.Bolt?.3f:1.4f,shot==SiegeShot.Bolt?3:7);Destroy(gameObject);return;
   }
   transform.position=next;velocity+=Physics.gravity*dt;if(velocity.sqrMagnitude>.1f)transform.rotation=Quaternion.LookRotation(velocity);age+=dt;if(age>15||transform.position.y< -15)Destroy(gameObject);
  }
 }
}
