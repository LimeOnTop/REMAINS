using UnityEngine;
namespace Remains {
public class BattlefieldClimbRope:MonoBehaviour {
 public Vector3 bottom,top;
 public static bool TryClimb(CastleWalkthrough player,CharacterController cc,bool ascend){
  if(!ascend)return false;
  foreach(var rope in FindObjectsByType<BattlefieldClimbRope>(FindObjectsSortMode.None)){
   var p=player.transform.position;var d=Vector3.up*(rope.top.y-rope.bottom.y);
   float t=Mathf.Clamp01(Vector3.Dot(p-rope.bottom,d)/d.sqrMagnitude);
   if(Vector3.Distance(p,rope.bottom+d*t)>2.3f||p.y>rope.top.y+.1f)continue;
   if(p.y>rope.top.y-.45f){cc.enabled=false;player.transform.position=rope.top+Vector3.up*.15f;cc.enabled=true;}
   else cc.Move((d.normalized*4.3f+(rope.bottom+d*t-p)*2)*Time.deltaTime);
   return true;
  }
  return false;
 }
}
}
