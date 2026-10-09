using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Remains.Editor {
public static partial class CastleArtUpgrade {
 [Serializable] public class Part {public string id,name,kind,source,node_id,model; public Vector3 anchor,size; public int triangles,objects; public bool hinge_local;}
 [Serializable] public class Manifest {public Part[] parts;}
 public static void ExportSource(){
  EditorSceneManager.OpenScene(ScenePath);
  var data=JsonUtility.FromJson<Manifest>(File.ReadAllText("output/castle-modules/workspace-manifest.json"));
  var root=GameObject.Find("Map_LargeCastle_Blockout").transform.Find("Castle_150x130m");
  var list=new List<Part>();Directory.CreateDirectory(Out+"source/models");
  foreach(var p in data.parts.Where(p=>p.kind=="wall"||p.kind=="tower"||p.id=="gatehouse")){
   Transform tr=root.Find(p.kind=="wall"?"Wall_"+p.id.Split('-')[1]:p.kind=="tower"?"Round_Tower_"+p.id.Split('-')[1]:"Gatehouse_DoubleDoors");
   var fs=tr.GetComponentsInChildren<MeshFilter>().Where(f=>!f.name.StartsWith("Wall_Step_")&&f.name!="Stair_Guard"&&!f.transform.IsChildOf(root.Find("Gatehouse_DoubleDoors/Left_Hinge"))&&!f.transform.IsChildOf(root.Find("Gatehouse_DoubleDoors/Right_Hinge"))).ToArray();
   var bounds=fs[0].GetComponent<Renderer>().bounds;foreach(var f in fs)bounds.Encapsulate(f.GetComponent<Renderer>().bounds);
   p.anchor=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);p.size=bounds.size;p.triangles=0;p.objects=fs.Length;
   var groups=new Dictionary<Material,List<CombineInstance>>();
   foreach(var f in fs){var mats=f.GetComponent<Renderer>().sharedMaterials;for(int s=0;s<f.sharedMesh.subMeshCount;s++){var mat=mats[Mathf.Min(s,mats.Length-1)];if(!groups.ContainsKey(mat))groups[mat]=new List<CombineInstance>();groups[mat].Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=s,transform=Matrix4x4.Translate(-p.anchor)*f.transform.localToWorldMatrix});}}
   using(var bw=new BinaryWriter(File.Create(Out+"source/models/"+p.id+".bin"))){bw.Write(groups.Count);foreach(var pair in groups){var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);var color=pair.Key.HasProperty("_BaseColor")?pair.Key.GetColor("_BaseColor"):pair.Key.color;bw.Write(color.r);bw.Write(color.g);bw.Write(color.b);bw.Write(color.a);var vs=mesh.vertices;var ns=mesh.normals;var ts=mesh.triangles;bw.Write(vs.Length);bw.Write(ts.Length);bw.Write(false);for(int i=0;i<vs.Length;i++){bw.Write(vs[i].x);bw.Write(vs[i].y);bw.Write(vs[i].z);bw.Write(ns[i].x);bw.Write(ns[i].y);bw.Write(ns[i].z);}foreach(int t in ts)bw.Write(t);p.triangles+=ts.Length/3;UnityEngine.Object.DestroyImmediate(mesh);}}
   list.Add(p);
  }
  File.WriteAllText(Out+"source/manifest.json",JsonUtility.ToJson(new Manifest{parts=list.ToArray()},true));
 }
}
}
