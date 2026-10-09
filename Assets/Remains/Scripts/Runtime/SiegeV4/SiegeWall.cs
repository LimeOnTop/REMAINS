using System.Collections.Generic;
using UnityEngine;
namespace Remains {
 public class SiegeWall:MonoBehaviour {
  public List<Renderer> bricks=new List<Renderer>();public int destroyedCount;Mesh combined;GameObject visual;
  void Awake(){RefreshVisuals();}
  void OnDestroy(){if(combined)Destroy(combined);}
  public void RefreshVisuals(){
   if(!visual){visual=new GameObject("Combined_Intact_Masonry");visual.transform.SetParent(transform,false);visual.AddComponent<MeshFilter>();visual.AddComponent<MeshRenderer>();}
   var groups=new Dictionary<Material,List<CombineInstance>>();
   foreach(var r in bricks){if(!r||!r.gameObject.activeSelf)continue;var f=r.GetComponent<MeshFilter>();if(!f)continue;for(int k=0;k<f.sharedMesh.subMeshCount;k++){var m=r.sharedMaterials[Mathf.Min(k,r.sharedMaterials.Length-1)];if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();groups[m].Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=k,transform=transform.worldToLocalMatrix*f.transform.localToWorldMatrix});}r.enabled=false;}
   var pieces=new List<CombineInstance>();var mats=new List<Material>();foreach(var pair in groups){var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);pieces.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});mats.Add(pair.Key);}
   if(combined)Destroy(combined);combined=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};combined.CombineMeshes(pieces.ToArray(),false,true);foreach(var p in pieces)Destroy(p.mesh);visual.GetComponent<MeshFilter>().sharedMesh=combined;visual.GetComponent<MeshRenderer>().sharedMaterials=mats.ToArray();
  }
  public int Blast(Vector3 world,Vector3 velocity){
   var point=transform.InverseTransformPoint(world);var removed=new List<Renderer>();
   foreach(var r in bricks){if(!r)continue;var p=transform.InverseTransformPoint(r.bounds.center);float dx=p.x-point.x,dy=p.y-point.y;if(dx*dx+dy*dy<11.5f)removed.Add(r);}
   // Full depth removal opens a real hole; loosen the narrow unsupported column above it.
   foreach(var r in bricks){if(!r||removed.Contains(r))continue;var p=transform.InverseTransformPoint(r.bounds.center);if(Mathf.Abs(p.x-point.x)<1.1f&&p.y>point.y&&p.y<point.y+6)removed.Add(r);}
   int live=0;foreach(var r in removed){bricks.Remove(r);destroyedCount++;r.enabled=true;if(live++<65&&Application.isPlaying)SiegeEffects.Fragment(r.gameObject,velocity.normalized*Random.Range(2f,5f)+Vector3.up*Random.Range(1f,3f));else{foreach(var c in r.GetComponents<Collider>())c.enabled=false;r.gameObject.SetActive(false);}}
   if(Application.isPlaying)RefreshVisuals();
   if(Application.isPlaying)SiegeEffects.Puff(world,2,10);return removed.Count;
  }
 }
}
