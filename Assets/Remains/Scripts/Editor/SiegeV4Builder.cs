using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace Remains.Editor {
 public static class SiegeV4Builder {
  const string Scene="Assets/Remains/Scenes/Remains_RoundMap.unity",Dir="Assets/Remains/SiegeV4/",Out="output/siege-v4/";
  static Transform root;static MeshCollider ground;
  static GameObject Model(string n,Transform parent,Vector3 pos){var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Models/"+n+".glb");if(!asset)throw new Exception("Missing "+n);var holder=Group(n,parent,pos);var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,holder);g.name=n+"_Art";g.transform.localPosition=Vector3.zero;g.transform.localRotation=Quaternion.Euler(0,180,0);foreach(var r in g.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.TwoSided;return holder.gameObject;}

  static Transform Group(string n,Transform p,Vector3 pos){var t=new GameObject(n).transform;t.SetParent(p,false);t.localPosition=pos;return t;}
  static float Height(float x,float z){if(ground.Raycast(new Ray(new Vector3(x,150,z),Vector3.down),out var h,200))return h.point.y;return 16;}
  static void Box(GameObject g,Vector3 center,Vector3 size){var c=g.AddComponent<BoxCollider>();c.center=center;c.size=size;}
  static void SaveAsset(UnityEngine.Object obj,string path){var old=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);if(old){if(obj is Mesh source&&old is Mesh target)CastleArtUpgrade.CopyMesh(source,target);else {EditorUtility.CopySerialized(obj,old);EditorUtility.SetDirty(old);}UnityEngine.Object.DestroyImmediate(obj);}else AssetDatabase.CreateAsset(obj,path);}
  public static void Flatten(){
   var f=GameObject.Find("Battlefield_V2/Round_Battlefield_Ground").GetComponent<MeshFilter>();ground=f.GetComponent<MeshCollider>();var mesh=UnityEngine.Object.Instantiate(f.sharedMesh);var vs=mesh.vertices;
   for(int i=0;i<vs.Length;i++){var v=vs[i];if(v.z> -158&&v.z< -80&&Mathf.Abs(v.x)<12&&v.y>8){float side=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(8.2f,12,Mathf.Abs(v.x)));float start=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-158,-140,v.z));v.y=Mathf.Lerp(v.y,Mathf.Min(v.y,15.80f),side*start);vs[i]=v;}}
   mesh.vertices=vs;mesh.RecalculateNormals();mesh.RecalculateBounds();SaveAsset(mesh,Dir+"Ground_FlatBridge.asset");f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Dir+"Ground_FlatBridge.asset");ground.sharedMesh=f.sharedMesh;Physics.SyncTransforms();
  }
  public static void RepairApproach(){Flatten();var field=GameObject.Find("Battlefield_V2/Meadow_Upgrade_V3");foreach(var f in field.GetComponentsInChildren<MeshFilter>()){var vs=f.sharedMesh.vertices;bool changed=false;for(int i=0;i<vs.Length;i++){var v=vs[i];if(Mathf.Abs(v.x)<8.2f&&v.z> -145&&v.z< -80&&v.y>15.8f&&v.y<23){v.y=15.8f+(f.name=="Earth_Paths"?.045f:0);vs[i]=v;changed=true;}}if(changed){f.sharedMesh.vertices=vs;f.sharedMesh.RecalculateBounds();EditorUtility.SetDirty(f.sharedMesh);}}AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());}
  public static void RepairChains(){var m=UnityEngine.Object.FindFirstObjectByType<BlockoutDrawbridge>();var bridge=m.GetComponent<SiegeBridge>();for(int i=0;i<2;i++){float x=i==0?-8.5f:8.5f;var a=m.attachments[i].localPosition;a.x=x;m.attachments[i].localPosition=a;var b=m.pulleys[i].position;b.x=x;m.pulleys[i].position=b;}m.ApplyPose(78);for(int i=0;i<2;i++){var path=m.ChainPath(i);var target=bridge.chains[i];target.transform.position=(path[0]+path[1])/2;var cap=target.GetComponentInChildren<CapsuleCollider>();cap.transform.position=target.transform.position;cap.transform.rotation=Quaternion.FromToRotation(Vector3.up,(path[1]-path[0]).normalized);cap.height=Vector3.Distance(path[0],path[1])+.8f;}EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());}
  public static void Polish(){
   CastleArtUpgrade.RebuildMeadow();GrassLOD();CastleArtUpgrade.ExportLandscape();
   root=GameObject.Find("Siege_V4").transform;var d=root.GetComponent<SiegeDirector>();var old=root.Find("Arsenal_Sword");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);d.weaponPrefabs=new GameObject[2];for(int i=0;i<2;i++){var existing=root.Find(i==0?"sword":"hammer");if(existing)UnityEngine.Object.DestroyImmediate(existing.gameObject);var g=Model(i==0?"sword":"hammer",root,Vector3.zero);g.SetActive(false);d.weaponPrefabs[i]=g;}d.swordPrefab=d.weaponPrefabs[0];
   // Weapon handles, warm forge light and a readable smith station.
   foreach(var smith in d.smiths){var fire=smith.parent.Find("Forge_Light");if(!fire){fire=Group("Forge_Light",smith.parent,new Vector3(12,1.7f,3));var light=fire.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.36f,.10f);light.range=6;light.intensity=2.3f;}}
   EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();File.WriteAllText(Out+"polish.done","dense grass and weapons saved");
  }
  public static void RepairVisuals(){
   var names=new HashSet<string>(new[]{"ballista","ram","catapult","blacksmith","forge","war-tent","bolt","stone","gate-bar","timber-bridge","timber-leaf","sword","hammer","wall-blocks-0","wall-blocks-2","wall-blocks-3","wall-blocks-4","wall-blocks-5","wall-blocks-6","wall-blocks-7"});
   foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>names.Contains(x.name)).ToArray()){
    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Models/"+t.name+".glb");if(!asset)continue;
    foreach(Transform child in t.Cast<Transform>().ToArray())if(child.name!="Muzzle")UnityEngine.Object.DestroyImmediate(child.gameObject);
    var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,t);g.name=t.name+"_Art";g.transform.localPosition=Vector3.zero;g.transform.localRotation=Quaternion.Euler(0,180,0);g.transform.localScale=Vector3.one;
    var wall=t.GetComponent<SiegeWall>();if(wall){wall.bricks.Clear();foreach(var f in g.GetComponentsInChildren<MeshFilter>()){Box(f.gameObject,f.sharedMesh.bounds.center,f.sharedMesh.bounds.size);wall.bricks.Add(f.GetComponent<Renderer>());}}
   }
   var gate=UnityEngine.Object.FindFirstObjectByType<BlockoutGate>();foreach(var hinge in new[]{gate.leftHinge,gate.rightHinge})foreach(var f in hinge.GetComponentsInChildren<MeshFilter>())if(f.name.StartsWith("gate-"))f.transform.localRotation=Quaternion.Euler(0,180,0);
   Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());File.WriteAllText(Out+"visuals.done","corrected import orientation");
  }
  static Mesh Reduced(Mesh m,int stride){var vs=m.vertices;var ns=m.normals;var cs=m.colors;var uv=m.uv;var v=new List<Vector3>();var n=new List<Vector3>();var c=new List<Color>();var u=new List<Vector2>();var t=new List<int>();for(int i=0;i+4<vs.Length;i+=5*stride){int k=v.Count;for(int j=0;j<5;j++){v.Add(vs[i+j]);n.Add(ns[i+j]);c.Add(cs[i+j]);u.Add(uv[i+j]);}t.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3,k+2,k+4,k+3});}var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetColors(c);mesh.SetUVs(0,u);mesh.SetTriangles(t,0);mesh.RecalculateBounds();return mesh;}
  static void GrassLOD(){int count=0;var field=GameObject.Find("Battlefield_V2/Meadow_Upgrade_V3").transform;foreach(var f in field.GetComponentsInChildren<MeshFilter>().Where(x=>x.name.StartsWith("Grass_")).ToArray()){
   var r=f.GetComponent<MeshRenderer>();var lod=f.gameObject.AddComponent<LODGroup>();var levels=new List<LOD>{new LOD(.28f,new Renderer[]{r})};for(int i=1;i<3;i++){var mesh=Reduced(f.sharedMesh,i==1?3:9);string path=Dir+f.name+"_LOD"+i+".asset";SaveAsset(mesh,path);var g=Group("LOD"+i,f.transform,Vector3.zero).gameObject;g.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);var ren=g.AddComponent<MeshRenderer>();ren.sharedMaterial=r.sharedMaterial;ren.shadowCastingMode=i==2?ShadowCastingMode.Off:ShadowCastingMode.On;levels.Add(new LOD(i==1?.10f:.015f,new Renderer[]{ren}));}lod.SetLODs(levels.ToArray());lod.RecalculateBounds();count++;}File.WriteAllText(Out+"grass-lod.txt",count+" chunks, 3 LODs");}
  static void Walls(Transform castle){foreach(int i in new[]{0,2,3,4,5,6,7}){var wall=castle.Find("Wall_"+i);var previous=wall.Find("Destructible_V4");if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
   foreach(var r in wall.GetComponentsInChildren<Renderer>(true))if(!r.name.StartsWith("Wall_Step_")&&r.name!="Stair_Guard")r.enabled=false;
   foreach(var c in wall.GetComponentsInChildren<Collider>(true))if(!c.name.StartsWith("Wall_Step_")&&c.name!="Stair_Guard")c.enabled=false;
   var holder=Group("Destructible_V4",wall,Vector3.zero);var g=Model("wall-blocks-"+i,holder,Vector3.zero);var destruct=g.AddComponent<SiegeWall>();foreach(var f in g.GetComponentsInChildren<MeshFilter>()){Box(f.gameObject,f.sharedMesh.bounds.center,f.sharedMesh.bounds.size);destruct.bricks.Add(f.GetComponent<Renderer>());f.gameObject.isStatic=false;}
  }}
  [MenuItem("Remains/Build Siege V4")]
  public static void Build(){try{
   var scene=EditorSceneManager.OpenScene(Scene);string backup="Assets/Remains/Scenes/Backups/Before_SiegeV4_20261010.unity";if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);Directory.CreateDirectory(Out);Directory.CreateDirectory(Dir);
   var old=GameObject.Find("Siege_V4");if(old)UnityEngine.Object.DestroyImmediate(old);root=Group("Siege_V4",null,Vector3.zero);
   Flatten();CastleArtUpgrade.RebuildMeadow();GrassLOD();ground=GameObject.Find("Battlefield_V2/Round_Battlefield_Ground").GetComponent<MeshCollider>();
   var map=GameObject.Find("Map_LargeCastle_Blockout").transform;var castle=map.Find("Castle_150x130m");Walls(castle);
   var fixedBridge=map.Find("Bridge_Rectangle_12x72m");foreach(var r in fixedBridge.GetComponentsInChildren<Renderer>())r.enabled=false;foreach(var c in fixedBridge.GetComponentsInChildren<Collider>())c.enabled=false;
   var timber=Model("timber-bridge",root,new Vector3(0,16,-105.1f));Box(timber,new Vector3(0,-.18f,0),new Vector3(16,.36f,49.8f));foreach(int side in new[]{-1,1})Box(timber,new Vector3(side*7.8f,.7f,0),new Vector3(.28f,1.4f,49.8f));
   var director=root.gameObject.AddComponent<SiegeDirector>();director.player=UnityEngine.Object.FindFirstObjectByType<CastleWalkthrough>();var smiths=new List<Transform>();
   foreach(string name in new[]{"Attack_Camp_West","Attack_Camp_South","Attack_Camp_East"}){
    var camp=GameObject.Find("Battlefield_V2/"+name).transform;camp.Find("Tent").gameObject.SetActive(false);var oldArt=camp.Find("Camp_V4");if(oldArt)UnityEngine.Object.DestroyImmediate(oldArt.gameObject);var art=Group("Camp_V4",camp,Vector3.zero);var tent=Model("war-tent",art,Vector3.zero);Box(tent,new Vector3(-8,2.4f,0),new Vector3(.2f,4.8f,12));Box(tent,new Vector3(8,2.4f,0),new Vector3(.2f,4.8f,12));Box(tent,new Vector3(0,2.4f,-6),new Vector3(16,4.8f,.2f));
    var forge=Model("forge",art,new Vector3(12,0,3));Box(forge,new Vector3(0,1,0),new Vector3(3,2,2));var smith=Model("blacksmith",art,new Vector3(11,0,6));smith.transform.localRotation=Quaternion.Euler(0,180,0);Box(smith,new Vector3(0,1,0),new Vector3(.8f,2,.8f));smiths.Add(smith.transform);
   }director.smiths=smiths.ToArray();director.blacksmith=smiths[1];
   var bolt=Model("bolt",root,Vector3.zero);bolt.SetActive(false);var rock=Model("stone",root,Vector3.zero);rock.SetActive(false);
   director.engines=new SiegeEngineV4[3];string[] names={"ballista","ram","catapult"};for(int i=0;i<3;i++){float x=(i-1)*13;var g=Model(names[i],root,new Vector3(x,Height(x,-170)+.03f,-170));var e=g.AddComponent<SiegeEngineV4>();e.kind=(SiegeEngineKind)i;e.ammunition=i==0?bolt:rock;e.muzzle=Group("Muzzle",g.transform,new Vector3(0,i==0?2.35f:2.9f,i==0?2.2f:0));Box(g,new Vector3(0,1.2f,0),new Vector3(2.2f,2.2f,i==1?6.7f:4.8f));director.engines[i]=e;}
   // Reuse an existing sword asset if available; otherwise a small scene-ready blade.
   var sword=Group("Arsenal_Sword",root,Vector3.zero).gameObject;var blade=GameObject.CreatePrimitive(PrimitiveType.Cube);blade.transform.SetParent(sword.transform,false);blade.transform.localPosition=new Vector3(0,.55f,0);blade.transform.localScale=new Vector3(.09f,1.1f,.035f);var guard=GameObject.CreatePrimitive(PrimitiveType.Cube);guard.transform.SetParent(sword.transform,false);guard.transform.localScale=new Vector3(.34f,.045f,.07f);sword.SetActive(false);director.swordPrefab=sword;
   var mechanism=UnityEngine.Object.FindFirstObjectByType<BlockoutDrawbridge>();mechanism.enabled=false;
   foreach(var r in mechanism.leaf.GetComponentsInChildren<Renderer>())if(r.name=="Moving_Deck"||r.name=="Moving_Deck_Rail"||r.name=="Longitudinal_Beam")r.enabled=false;
   var oldLeaf=mechanism.leaf.Find("timber-leaf");if(oldLeaf)UnityEngine.Object.DestroyImmediate(oldLeaf.gameObject);Model("timber-leaf",mechanism.leaf,Vector3.zero);
   for(int side=0;side<2;side++){float x=side==0?-8.5f:8.5f;var a=mechanism.attachments[side].localPosition;a.x=x;mechanism.attachments[side].localPosition=a;var b=mechanism.pulleys[side].position;b.x=x;mechanism.pulleys[side].position=b;}mechanism.raised=true;mechanism.ApplyPose(78);var bridge=mechanism.gameObject.GetComponent<SiegeBridge>();if(!bridge)bridge=mechanism.gameObject.AddComponent<SiegeBridge>();bridge.mechanism=mechanism;bridge.chains=new SiegeTarget[2];bridge.broken=new bool[2];bridge.fallen=false;
   for(int i=0;i<2;i++){var go=Group("Chain_Target_"+i,root,Vector3.zero).gameObject;var target=go.AddComponent<SiegeTarget>();target.kind=SiegeTarget.TargetKind.Chain;target.hitsRequired=3;target.bridge=bridge;target.side=i;var path=mechanism.ChainPath(i);go.transform.position=Vector3.Lerp(path[0],path[1],.5f);
    var a=path[0];var b=path[1];var collider=Group("Chain_Hitbox",go.transform,Vector3.zero).gameObject;collider.transform.position=(a+b)/2;collider.transform.rotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);var cap=collider.AddComponent<CapsuleCollider>();cap.radius=.40f;cap.height=Vector3.Distance(a,b)+.8f;cap.isTrigger=true;bridge.chains[i]=target;
   }director.bridge=bridge;
   var gate=UnityEngine.Object.FindFirstObjectByType<BlockoutGate>();gate.enabled=false;gate.open=false;foreach(var hinge in new[]{gate.leftHinge,gate.rightHinge})foreach(var f in hinge.GetComponentsInChildren<MeshFilter>())if(f.name.StartsWith("gate-"))f.transform.localRotation=Quaternion.Euler(0,180,0);gate.leftHinge.localRotation=gate.rightHinge.localRotation=Quaternion.identity;var targetGate=Group("Gate_Locked_Target",root,new Vector3(0,20,-60.5f)).gameObject;Box(targetGate,Vector3.zero,new Vector3(11,8,.7f));var damage=targetGate.AddComponent<SiegeTarget>();damage.kind=SiegeTarget.TargetKind.Gate;damage.hitsRequired=10;damage.gate=gate;damage.crossbar=Model("gate-bar",root,new Vector3(0,20,-59.85f)).transform;director.gate=damage;
   // Clear trees only from the engine staging area and expanded camp footprint.
   var field=GameObject.Find("Battlefield_V2").transform;foreach(var t in field.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Pine_")||t.name.StartsWith("Oak_")||t.name.StartsWith("Tree_")).ToArray()){var p=t.position;if(Mathf.Abs(p.x)<24&&p.z> -210&&p.z< -158)t.gameObject.SetActive(false);}
   var player=director.player;var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.spawn=new Vector3(11,Height(11,-181)+.1f,-181);player.transform.position=player.spawn;player.transform.rotation=Quaternion.Euler(0,180,0);cc.enabled=true;
   Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText(Out+"build.done","Scene saved: 7 destructible walls, 3 camps, 3 siege engines, 2 chains, gate lock, dense grass LOD");if(Application.isBatchMode)EditorApplication.Exit(0);
  }catch(Exception ex){File.WriteAllText(Out+"build-error.txt",ex.ToString());Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}}
 }
}
