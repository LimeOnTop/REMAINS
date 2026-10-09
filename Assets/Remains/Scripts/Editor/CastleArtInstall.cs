using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
namespace Remains.Editor {
public static partial class CastleArtUpgrade {
 static System.Random random=new System.Random(261009);
 static float Rand(float a,float b)=>a+(b-a)*(float)random.NextDouble();
 static Material MaterialAsset(string name,Color c,float detail=0,float wind=0){string path=Dir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Remains/MeadowV3"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",c);m.SetFloat("_DetailStrength",detail);m.SetFloat("_WindStrength",wind);return m;}
 static Transform NewGroup(string n,Transform p=null){var t=new GameObject(n).transform;t.SetParent(p,false);return t;}
 public static void CopyMesh(Mesh source,Mesh target){target.Clear();target.indexFormat=source.indexFormat;target.vertices=source.vertices;target.normals=source.normals;target.colors=source.colors;target.uv=source.uv;target.subMeshCount=source.subMeshCount;for(int i=0;i<source.subMeshCount;i++)target.SetIndices(source.GetIndices(i),source.GetTopology(i),i);target.bounds=source.bounds;target.UploadMeshData(false);EditorUtility.SetDirty(target);}
 static void MeshAsset(Mesh m,string name){string path=Dir+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){CopyMesh(m,old);UnityEngine.Object.DestroyImmediate(m);}else AssetDatabase.CreateAsset(m,path);}
 static void MeshObject(Transform parent,string name,List<Vector3> v,List<int> t,List<Color> colors,Material mat,List<Vector2> uv=null){if(v.Count==0)return;var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetTriangles(t,0);m.SetColors(colors);if(uv!=null)m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();MeshAsset(m,name);var g=NewGroup(name,parent).gameObject;g.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Dir+name+".asset");g.AddComponent<MeshRenderer>().sharedMaterial=mat;g.isStatic=true;}
 static float Seg(Vector2 p,Vector2 a,Vector2 b){Vector2 d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude));}
 static List<Vector2[]> routes=new List<Vector2[]>();
 static void Routes(){routes.Clear();var trail=new List<Vector2>();for(int i=0;i<=100;i++){float a=Mathf.Lerp(-Mathf.PI/2-.25f,-Mathf.PI*1.5f+.25f,i/100f);float r=182+5*Mathf.Sin(i*.24f);trail.Add(new Vector2(Mathf.Sin(a)*r,Mathf.Cos(a)*r+8));}routes.Add(trail.ToArray());routes.Add(new[]{new Vector2(0,-214),new Vector2(0,-195),new Vector2(0,-151),new Vector2(0,-128)});routes.Add(new[]{new Vector2(0,-55),new Vector2(0,-35),new Vector2(-4,-15),new Vector2(3,5),new Vector2(-2,24)});routes.Add(new[]{new Vector2(-4,-15),new Vector2(-17,-20),new Vector2(-24,-25)});routes.Add(new[]{new Vector2(3,5),new Vector2(15,0),new Vector2(25,-1)});}
 static float PathDistance(Vector2 p){float d=999;foreach(var r in routes)for(int i=1;i<r.Length;i++)d=Mathf.Min(d,Seg(p,r[i-1],r[i]));return d;}
 static MeshCollider groundCollider;
 static bool Surface(Vector2 p,out RaycastHit hit){return groundCollider.Raycast(new Ray(new Vector3(p.x,130,p.y),Vector3.down),out hit,200)&&hit.point.y>8&&hit.normal.y>.78;}
 static bool Building(Vector2 p){return Mathf.Abs(p.x+34)<15&&Mathf.Abs(p.y+13)<13||Mathf.Abs(p.x-26)<15&&Mathf.Abs(p.y-12)<13||Mathf.Abs(p.x+6)<16&&Mathf.Abs(p.y-40)<15;}
 static bool Camps(Vector2 p){return new[]{new Vector2(-178,-35),new Vector2(0,-195),new Vector2(178,-35)}.Any(c=>Vector2.Distance(p,c)<16);}
 public static void RebuildMeadow(){Environment();}
 static void Environment(){
  Routes();var field=GameObject.Find("Battlefield_V2").transform;
  var previous=field.Find("Meadow_Upgrade_V3");if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
  var env=NewGroup("Meadow_Upgrade_V3",field);var ground=field.Find("Round_Battlefield_Ground");groundCollider=ground.GetComponent<MeshCollider>();
  var old=field.Find("Three_Dimensional_Grass");if(old)old.gameObject.SetActive(false);
  var source=ground.GetComponent<MeshFilter>().sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Ground_ArtV3";var vs=mesh.vertices;var cs=mesh.colors;
  if(cs.Length!=vs.Length)cs=Enumerable.Repeat(Color.white,vs.Length).ToArray();
  for(int i=0;i<vs.Length;i++){
   var p=new Vector2(vs[i].x,vs[i].z);float patch=Mathf.PerlinNoise(p.x*.045f+5,p.y*.045f+7);float d=PathDistance(p);float h=vs[i].y;
   if(h>8){Color grass=Color.Lerp(new Color(.26f,.39f,.115f),new Color(.46f,.55f,.205f),patch);Color dirt=Color.Lerp(new Color(.43f,.30f,.16f),new Color(.60f,.455f,.27f),patch);float w=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.7f,4.8f,d));cs[i]=Color.Lerp(grass,dirt,Camps(p)?1:w);}
  }
  mesh.colors=cs;MeshAsset(mesh,"Ground_ArtV3");var groundMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Dir+"Ground_ArtV3.asset");ground.GetComponent<MeshFilter>().sharedMesh=groundMesh;groundCollider.sharedMesh=groundMesh;ground.GetComponent<Renderer>().sharedMaterial=MaterialAsset("Meadow_Ground",Color.white,.45f);
  var grassMat=MaterialAsset("Grass_TwoSided",Color.white,0,.08f);
  var chunks=new Dictionary<Vector2Int,Tuple<List<Vector3>,List<int>,List<Color>,List<Vector2>>>();int clumps=0;
  // Stratified jitter produces dense patches without placing large numbers of GameObjects.
  for(float z=-286;z<298;z+=.75f)for(float x=-291;x<292;x+=.75f){
   Vector2 p=new Vector2(x+Rand(-.32f,.32f),z+Rand(-.32f,.32f));float d=PathDistance(p);float r=new Vector2(p.x,p.y-8).magnitude;
   if(d<4.6f||Building(p)||Camps(p)||r>287||!Surface(p,out var hit))continue;
   if(r<85&&Rand(0,1)<.25f)continue;
   // Keep grasses clear of castle masonry and all non-ground obstacles.
   if(Physics.Raycast(hit.point+Vector3.up*2.3f,Vector3.down,out var top,3f)&&top.collider!=groundCollider)continue;
   float patch=Mathf.PerlinNoise(p.x*.071f+3,p.y*.071f+8);if(patch<.15f||Rand(0,1)>.95f)continue;
   var key=new Vector2Int(Mathf.FloorToInt(p.x/32),Mathf.FloorToInt(p.y/32));if(!chunks.TryGetValue(key,out var chunk)){chunk=Tuple.Create(new List<Vector3>(),new List<int>(),new List<Color>(),new List<Vector2>());chunks[key]=chunk;}
   var v=chunk.Item1;var t=chunk.Item2;var c=chunk.Item3;var uv=chunk.Item4;
   Color dark=Color.Lerp(new Color(.18f,.30f,.065f),new Color(.28f,.40f,.10f),Rand(0,1));Color light=Color.Lerp(new Color(.42f,.56f,.16f),new Color(.62f,.68f,.29f),Rand(0,1));
   float height=Rand(.38f,.70f)*(d<7?1.15f:1);clumps++;
   for(int b=0;b<11;b++){
    float a=Rand(0,Mathf.PI*2);var dx=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*Rand(.055f,.105f);var bend=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*Rand(.08f,.24f);var foot=hit.point+new Vector3(Rand(-.36f,.36f),.015f,Rand(-.36f,.36f));float h=height*Rand(.6f,1.15f);int k=v.Count;
    v.AddRange(new[]{foot-dx,foot+dx,foot+Vector3.up*h*.6f+bend*.5f-dx*.55f,foot+Vector3.up*h*.6f+bend*.5f+dx*.55f,foot+Vector3.up*h+bend});
    t.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3,k+2,k+4,k+3});c.AddRange(new[]{dark,dark,Color.Lerp(dark,light,.65f),Color.Lerp(dark,light,.65f),light});uv.AddRange(new[]{Vector2.zero,Vector2.zero,new Vector2(0,.6f),new Vector2(0,.6f),Vector2.up});
   }
  }
  foreach(var pair in chunks)MeshObject(env,"Grass_"+pair.Key.x+"_"+pair.Key.y,pair.Value.Item1,pair.Value.Item2,pair.Value.Item3,grassMat,pair.Value.Item4);
  // Individually triangulated paths: tighter tessellation than the original terrain, with a broken edge.
  var pv=new List<Vector3>();var pt=new List<int>();var pc=new List<Color>();
  foreach(var route in routes){for(int s=1;s<route.Length;s++){Vector2 a=route[s-1],b=route[s];int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));Vector2 dir=(b-a).normalized,side=new Vector2(-dir.y,dir.x);
   for(int j=0;j<steps;j++)for(int lane=0;lane<6;lane++){
    Vector2 p0=Vector2.Lerp(a,b,j/(float)steps),p1=Vector2.Lerp(a,b,(j+1f)/steps);float w0=3.65f+.25f*Mathf.Sin(p0.x*.8f+p0.y*.65f),w1=3.65f+.25f*Mathf.Sin(p1.x*.8f+p1.y*.65f);float l=lane/3f-1,rn=(lane+1)/3f-1;
    var qs=new[]{p0+side*l*w0,p1+side*l*w1,p1+side*rn*w1,p0+side*rn*w0};var hits=new RaycastHit[4];bool ok=true;for(int k=0;k<4;k++)if(!Surface(qs[k],out hits[k]))ok=false;
    if(!ok||Building((p0+p1)/2))continue;int n=pv.Count;foreach(var hit in hits){float variation=Mathf.PerlinNoise(hit.point.x*.19f+31,hit.point.z*.19f+17);var color=Color.Lerp(new Color(.46f,.34f,.20f),new Color(.57f,.435f,.27f),variation);pv.Add(hit.point+Vector3.up*.045f);pc.Add(color);}pt.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
   }
  }}
  MeshObject(env,"Earth_Paths",pv,pt,pc,MaterialAsset("Earth_Path",Color.white,.7f));
  // Low-poly stones are combined into one mesh, sparsely scattered over path shoulders.
  var rockv=new List<Vector3>();var rockt=new List<int>();var rockc=new List<Color>();int pebbles=0;
  foreach(var route in routes)for(int i=1;i<route.Length;i++){var a=route[i-1];var b=route[i];int count=Mathf.CeilToInt(Vector2.Distance(a,b)*2);var dir=(b-a).normalized;var side=new Vector2(-dir.y,dir.x);for(int j=0;j<count;j++){
   var p=Vector2.Lerp(a,b,Rand(0,1))+side*Rand(-4.3f,4.3f);if(Building(p)||!Surface(p,out var h))continue;float radius=Rand(.07f,.19f);int n=rockv.Count;var q=h.point+Vector3.up*.055f;rockv.Add(q+Vector3.up*radius*.6f);for(int k=0;k<5;k++){float an=k*Mathf.PI*2/5;rockv.Add(q+new Vector3(Mathf.Cos(an)*radius,0,Mathf.Sin(an)*radius));}for(int k=0;k<5;k++)rockt.AddRange(new[]{n,n+1+k,n+1+(k+1)%5});Color color=Color.Lerp(new Color(.40f,.38f,.29f),new Color(.64f,.59f,.45f),Rand(0,1));for(int k=0;k<6;k++)rockc.Add(color);pebbles++;
  }}MeshObject(env,"Path_Pebbles",rockv,rockt,rockc,MaterialAsset("Path_Stone",Color.white));
  File.WriteAllText(Out+"environment.json",$"{{\"grass_clumps\":{clumps},\"grass_chunks\":{chunks.Count},\"grass_blades\":{clumps*11},\"pebbles\":{pebbles},\"path_triangles\":{pt.Count/3}}}");
 }
 public static void Install(){
  try {
   var scene=EditorSceneManager.OpenScene(ScenePath);Directory.CreateDirectory(Dir);Directory.CreateDirectory(Out);
   string backup="Assets/Remains/Scenes/Backups/Before_CastleArtV3_20261009.unity";if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
   var root=GameObject.Find("Map_LargeCastle_Blockout").transform;var castle=root.Find("Castle_150x130m");
   var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Out+"models.json"));int oldColliders=castle.GetComponentsInChildren<Collider>(true).Length;
   foreach(var p in manifest.parts){
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Models/"+p.id+".glb");if(!model)throw new Exception("Missing model: "+p.id);
    Transform target=castle.Find(p.kind=="wall"?"Wall_"+p.id.Split('-')[1]:p.kind=="tower"?"Round_Tower_"+p.id.Split('-')[1]:p.hinge_local?"Gatehouse_DoubleDoors/"+(p.id=="gate-west"?"Left_Hinge":"Right_Hinge"):"Gatehouse_DoubleDoors");
    var previous=target.Find("ArtV3_"+p.id);if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
    foreach(var r in target.GetComponentsInChildren<MeshRenderer>(true)){
     if(p.kind=="wall"&&(r.name.StartsWith("Wall_Step_")||r.name=="Stair_Guard"))continue;
     if(p.id=="gatehouse"&&(r.transform.IsChildOf(target.Find("Left_Hinge"))||r.transform.IsChildOf(target.Find("Right_Hinge"))))continue;
     r.enabled=false;
    }
    var holder=NewGroup("ArtV3_"+p.id,target);if(!p.hinge_local){holder.position=p.anchor;holder.rotation=Quaternion.identity;}
    var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,holder);instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;instance.transform.localScale=Vector3.one;
    foreach(var r in instance.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.TwoSided;r.receiveShadows=true;}
    PrefabUtility.SaveAsPrefabAsset(instance,Dir+p.id+".prefab");
   }
   if(castle.GetComponentsInChildren<Collider>(true).Length!=oldColliders)throw new Exception("Collider count changed unexpectedly");
   Physics.SyncTransforms();Environment();ExportLandscape();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   File.WriteAllText(Out+"install.json",$"{{\"modules\":{manifest.parts.Length},\"preserved_colliders\":{oldColliders},\"scene\":\"{ScenePath}\"}}");
   SchedulePreview();
  }catch(Exception e){File.WriteAllText(Out+"install-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
 }
 static int frames;
 static void SchedulePreview(){frames=0;EditorApplication.update+=WaitPreview;}
 static void WaitPreview(){if(++frames<80)return;EditorApplication.update-=WaitPreview;try{PreviewScene();File.WriteAllText(Out+"install.done","ok");EditorApplication.Exit(0);}catch(Exception e){File.WriteAllText(Out+"preview-error.txt",e.ToString());EditorApplication.Exit(1);}}
 static void CaptureV3(Camera cam,string filename){var target=new RenderTexture(1800,1300,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.antiAliasing=4;RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=target});var prev=RenderTexture.active;RenderTexture.active=target;var tex=new Texture2D(1800,1300,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1800,1300),0,0);tex.Apply();File.WriteAllBytes(Out+filename+".png",tex.EncodeToPNG());RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(target);}
 static void PreviewScene(){var root=GameObject.Find("Map_LargeCastle_Blockout").transform;var cam=root.Find("OverviewCamera").GetComponent<Camera>();var pos=cam.transform.position;var rot=cam.transform.rotation;float size=cam.orthographicSize;bool ortho=cam.orthographic;
  CaptureV3(cam,"map-after");cam.transform.position=new Vector3(120,115,-176);cam.transform.LookAt(new Vector3(0,24,-28));cam.orthographic=true;cam.orthographicSize=86;CaptureV3(cam,"castle-after");cam.orthographic=false;cam.fieldOfView=65;cam.transform.position=new Vector3(8,19,-148);cam.transform.LookAt(new Vector3(0,24,-52));CaptureV3(cam,"gate-approach");cam.transform.position=new Vector3(-65,21,-164);cam.transform.LookAt(new Vector3(-89,18,-151));CaptureV3(cam,"meadow-path");cam.transform.SetPositionAndRotation(pos,rot);cam.orthographic=ortho;cam.orthographicSize=size;
 }
 public static void RenderOnly(){EditorSceneManager.OpenScene(ScenePath);SchedulePreview();}
}
}
