using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Remains.Editor {
public static partial class CastleArtUpgrade {
 public static void ExportLandscape(){
  string path=Out+"landscape/";Directory.CreateDirectory(path+"models");
  var root=GameObject.Find("Battlefield_V2/Meadow_Upgrade_V3").transform;
  var grass=root.GetComponentsInChildren<MeshFilter>().First(f=>f.name.StartsWith("Grass_")&&f.sharedMesh.vertexCount>4000);
  var fs=new[]{grass,root.Find("Earth_Paths").GetComponent<MeshFilter>()};var parts=new Part[2];
  for(int k=0;k<2;k++){
   var m=fs[k].sharedMesh;var vs=m.vertices;var ns=m.normals;var cs=m.colors;var ts=m.triangles;var anchor=m.bounds.center;
   string id=k==0?"grass-patch":"earth-paths";
   parts[k]=new Part{id=id,name=k==0?"Образец пучков травы — участок 32 м":"Земляные тропинки карты",kind="hill",anchor=anchor,triangles=ts.Length/3,node_id=k==0?"69042a8a-c14e-4eda-a445-af286ecdb816":"6d5a13ab-8d67-4994-8838-d0d3374e5d47"};
   using(var bw=new BinaryWriter(File.Create(path+"models/"+id+".bin"))){bw.Write(1);foreach(float c in new[]{1f,1f,1f,1f})bw.Write(c);bw.Write(vs.Length);bw.Write(ts.Length);bw.Write(true);for(int i=0;i<vs.Length;i++){var v=vs[i]-anchor;foreach(float c in new[]{v.x,v.y,v.z,ns[i].x,ns[i].y,ns[i].z,cs[i].r,cs[i].g,cs[i].b,cs[i].a})bw.Write(c);}foreach(int t in ts)bw.Write(t);}
  }
  File.WriteAllText(path+"manifest.json",JsonUtility.ToJson(new Manifest{parts=parts},true));
 }
}
}
