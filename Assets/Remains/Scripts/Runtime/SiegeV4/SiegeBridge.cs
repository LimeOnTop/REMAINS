using UnityEngine;
namespace Remains {
 public class SiegeBridge:MonoBehaviour {
  public BlockoutDrawbridge mechanism;public SiegeTarget[] chains;public bool[] broken=new bool[2];public bool fallen;float fallSpeed;
  public DrawbridgeChain[] physicsChains;
  public void BreakChain(int side){if(broken[side])return;broken[side]=true;if(physicsChains!=null&&physicsChains.Length>side&&physicsChains[side])physicsChains[side].Break();else mechanism.chainRoots[side].gameObject.SetActive(false);foreach(var c in chains[side].GetComponentsInChildren<Collider>())c.enabled=false;SiegeEffects.Puff(chains[side].transform.position,.6f,6);}
  void Update(){if(fallen||!broken[0]||!broken[1])return;fallSpeed+=65*Time.deltaTime;mechanism.ApplyPose(Mathf.MoveTowards(mechanism.angle,0,fallSpeed*Time.deltaTime));if(mechanism.angle<=0){fallen=true;for(int i=0;i<6;i++)SiegeEffects.Puff(mechanism.leaf.TransformPoint(new Vector3(Random.Range(-6f,6f),0,-15+i*2)),1.4f,5);}}
  void LateUpdate(){if(physicsChains!=null&&physicsChains.Length==2)return;for(int i=0;i<2;i++)if(broken[i])mechanism.chainRoots[i].gameObject.SetActive(false);}
 }
}
