using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Remains.Editor {
// Installs the Blender fortifications (output/fortifications/build_fortifications.py): towers with inward doors and
// stone spiral stairs, the gatehouse with chain holes and two winch rooms, physics lifting chains and capstans.
// Old blockout/ArtV3 pieces are deactivated, not deleted, so the previous castle can be restored by re-enabling them.
public static class FortificationsV4 {
 const string Models="Assets/Remains/Fortifications/Models/";
 const string ScenePath="Assets/Remains/Scenes/Remains_RoundMap.unity";
 static readonly HashSet<string> towerMarkers=new HashSet<string>{"Room_Entrance","Stair_Start","Stair_End"};
 static readonly HashSet<string> gateKeep=new HashSet<string>{"Left_Hinge","Right_Hinge","Bridge_Hinge_Landing"};

 static GameObject Model(string name,Transform parent,Vector3 localPos,Quaternion localRot,bool collider,bool isStatic=true){
  var src=AssetDatabase.LoadAssetAtPath<GameObject>(Models+name+".glb");
  var g=(GameObject)PrefabUtility.InstantiatePrefab(src,parent);g.name=name;g.transform.localPosition=localPos;g.transform.localRotation=localRot;
  foreach(var mf in g.GetComponentsInChildren<MeshFilter>()){if(collider&&!mf.GetComponent<MeshCollider>())mf.gameObject.AddComponent<MeshCollider>().sharedMesh=mf.sharedMesh;}
  if(isStatic)foreach(var t in g.GetComponentsInChildren<Transform>())t.gameObject.isStatic=true;
  return g;
 }
 static void Clear(Transform parent,string name){foreach(var t in parent.Cast<Transform>().Where(c=>c.name==name).ToList())Object.DestroyImmediate(t.gameObject);}

 public static void Install(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit Play mode first.");
  var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath);
  string backup="Assets/Remains/Scenes/Backups/Before_FortV4_20261010.unity";if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
  var castle=GameObject.Find("Map_LargeCastle_Blockout/Castle_150x130m").transform;
  // Towers: replace art and blockout with the Blender tower (door toward the courtyard, stone stair, matched roof level).
  for(int i=0;i<8;i++){
   var t=castle.Find("Round_Tower_"+i);Clear(t,"Tower_V4_"+i);
   foreach(Transform c in t)if(!towerMarkers.Contains(c.name))c.gameObject.SetActive(false);
   Model("Tower_V4_"+i,t,Vector3.zero,Quaternion.identity,true);
  }
  // Gatehouse: new central wall with the arch, chain holes and winch rooms; keep the door leaves and their hinges.
  var gh=castle.Find("Gatehouse_DoubleDoors");Clear(gh,"Gatehouse_V4");Clear(gh,"Winch_Rooms_V4");
  foreach(Transform c in gh)if(!gateKeep.Contains(c.name))c.gameObject.SetActive(false);
  Model("Gatehouse_V4",gh,Vector3.zero,Quaternion.identity,true);
  gh.GetComponent<BlockoutGate>().openAngle=88;
  // Drawbridge machinery.
  var sys=GameObject.Find("Map_LargeCastle_Blockout/Drawbridge_System");var bridge=sys.GetComponent<BlockoutDrawbridge>();
  foreach(Transform c in sys.transform)if(c.name.StartsWith("Pulley_Assembly")||c.name.StartsWith("Winch_Assembly")||c.name.StartsWith("Lifting_Chain"))c.gameObject.SetActive(false);
  bridge.controls=new Transform[0];
  var leafBody=bridge.leaf.GetComponent<Rigidbody>();
  var rooms=new GameObject("Winch_Rooms_V4").transform;rooms.SetParent(gh,false);
  var siege=sys.GetComponent<SiegeBridge>();var chains=new DrawbridgeChain[2];
  var linkPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Models+"Chain_Link_Pair.glb");
  var log=new List<string>();
  for(int s=0;s<2;s++){
   float side=s==0?-1:1;string suffix=s==0?"West":"East";
   var room=new GameObject("Winch_Room_"+suffix).transform;room.SetParent(rooms,false);
   // Ceiling pulley just inside the chain hole, hung from a timber beam.
   var c1=new Vector3(side*8.5f,9.4f,-.6f);
   Model("Chain_Pulley",room,c1,Quaternion.identity,false,false).name="Ceiling_Pulley";
   Model("Pulley_Yoke",room,c1,Quaternion.identity,true);
   // Capstan on the room floor; the floor pulley turns the chain from vertical toward the drum.
   var capC=new Vector3(side*11.0f,0,4.3f);
   var h=new Vector3(capC.x-side*8.5f,0,capC.z).normalized;
   var p2=new Vector3(side*8.5f,1.05f,0)+h*.6f;var b2=p2-new Vector3(0,.6f,0);
   var yaw=Quaternion.LookRotation(h,Vector3.up);
   Model("Pulley_Floor_Stand",room,new Vector3(p2.x,0,p2.z),yaw,true);
   Model("Chain_Pulley",room,p2,yaw,false,false).name="Floor_Pulley";
   var cap=Model("Capstan_Base",room,capC,Quaternion.identity,true);cap.name="Capstan_"+suffix;
   var head=new GameObject("Capstan_Head").transform;head.SetParent(cap.transform,false);
   var bars=Model("Capstan_Bars",head,Vector3.zero,Quaternion.identity,true,false);
   var capstan=cap.AddComponent<DrawbridgeCapstan>();capstan.bridge=bridge;capstan.head=head;
   // Drum tangent on the side that continues the chain run from the floor pulley.
   const float drumR=.66f;var flat=new Vector2(capC.x-b2.x,capC.z-b2.z);float dist=flat.magnitude;float beta=Mathf.Asin(drumR/dist);
   var dirToC=flat/dist;var rot=new Vector2(dirToC.x*Mathf.Cos(-beta*side)-dirToC.y*Mathf.Sin(-beta*side),dirToC.x*Mathf.Sin(-beta*side)+dirToC.y*Mathf.Cos(-beta*side));
   float along=Mathf.Sqrt(dist*dist-drumR*drumR);var tangent=new Vector3(b2.x+rot.x*along,b2.y,b2.z+rot.y*along);
   // Inner chain path (gatehouse-local), from the hole inward.
   var pts=new List<Vector3>{new Vector3(side*8.5f,10f,-2.3f)};
   for(int k=0;k<=6;k++){float a=Mathf.PI/2*(1-k/6f);pts.Add(c1+new Vector3(0,.6f*Mathf.Sin(a),.6f*Mathf.Cos(a)));}
   for(int k=0;k<=6;k++){float a=Mathf.PI/2*k/6f;pts.Add(p2+.6f*(-Mathf.Cos(a)*h-Mathf.Sin(a)*Vector3.up));}
   pts.Add(tangent);
   var world=pts.Select(p=>gh.TransformPoint(p)).ToArray();
   // Physics chain under the siege chain target, so ballista bolts still register hits on the links.
   var target=siege&&siege.chains!=null&&siege.chains.Length>s?siege.chains[s].transform:sys.transform;
   Clear(target,"Chain_V4");
   var chainGo=new GameObject("Chain_V4");chainGo.transform.SetParent(target,false);chainGo.transform.position=world[0];
   var chain=chainGo.AddComponent<DrawbridgeChain>();
   chain.bridge=bridge;chain.leafBody=leafBody;chain.attachment=bridge.attachments[s];chain.capstanHead=head;chain.innerPath=world;
   chain.drumCenter=gh.TransformPoint(new Vector3(capC.x,b2.y,capC.z));chain.drumRadius=drumR;chain.linkPairPrefab=linkPrefab;chain.pairs=64;
   chains[s]=chain;
   // Anchor plate and bracket on the leaf end.
   var att=bridge.attachments[s];foreach(Transform c in att)c.gameObject.SetActive(false);Clear(att,"Leaf_Chain_Eye");
   Model("Leaf_Chain_Eye",att,Vector3.zero,Quaternion.identity,false,false);
   var bracket=GameObject.CreatePrimitive(PrimitiveType.Cube);bracket.name="Leaf_Chain_Bracket";Object.DestroyImmediate(bracket.GetComponent<Collider>());bracket.transform.SetParent(att,false);
   bracket.transform.localPosition=new Vector3(-side*.28f,0,0);bracket.transform.localScale=new Vector3(.62f,.22f,.5f);bracket.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Remains/MapKit/Materials/MapKit_VertexColor.mat");
   var iron=AssetDatabase.LoadAllAssetsAtPath(Models+"Chain_Pulley.glb").OfType<Material>().FirstOrDefault(m=>m.name=="Forged_iron");if(iron)bracket.GetComponent<Renderer>().sharedMaterial=iron;
   // Warm light so the winch room is readable.
   var light=new GameObject("Winch_Room_Light").AddComponent<Light>();light.transform.SetParent(room,false);light.transform.localPosition=new Vector3(side*11f,8.5f,3.2f);light.type=LightType.Point;light.range=14;light.intensity=3;light.color=new Color(1f,.78f,.52f);light.shadows=LightShadows.None;
   log.Add($"{suffix}: hole {world[0]} drum {chain.drumCenter} tangent {gh.TransformPoint(tangent)}");
  }
  if(siege)siege.physicsChains=chains;
  if(!sys.GetComponent<DrawbridgeWinch>())sys.AddComponent<DrawbridgeWinch>();
  bridge.manual=true;
  var walker=Object.FindAnyObjectByType<CastleWalkthrough>();if(walker){var cc=walker.GetComponent<CharacterController>();if(cc.stepOffset<.32f)cc.stepOffset=.32f;}
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  File.WriteAllText("output/fortifications/install.log",string.Join("\n",log));
 }
}
}
