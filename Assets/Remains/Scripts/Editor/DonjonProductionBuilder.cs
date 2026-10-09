using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
namespace Remains.Editor {
 public static class DonjonProductionBuilder {
 const string Dir="Assets/Remains/DonjonProduction/";
 const string Out="output/donjon-production/previews/";
 public static void Build(){
  var root=new GameObject("Donjon_Detailed");
  var lods=new LOD[3];
  for(int i=0;i<3;i++){
   var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Donjon_LOD"+i+".glb");if(!asset)throw new Exception("Missing imported model");
   var obj=UnityEngine.Object.Instantiate(asset,root.transform);obj.name="LOD"+i;obj.transform.localPosition=Vector3.zero;obj.transform.localRotation=Quaternion.Euler(0,180,0);
   lods[i]=new LOD(new[]{.35f,.14f,.015f}[i],obj.GetComponentsInChildren<Renderer>());
   foreach(var r in obj.GetComponentsInChildren<Renderer>())r.shadowCastingMode=ShadowCastingMode.TwoSided;
  }
  var cg=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Donjon_Collision.glb"),root.transform);cg.name="Collision";cg.transform.localRotation=Quaternion.Euler(0,180,0);
  foreach(var f in cg.GetComponentsInChildren<MeshFilter>()) { var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;UnityEngine.Object.DestroyImmediate(f.GetComponent<Renderer>()); }
  var lod=root.AddComponent<LODGroup>();lod.SetLODs(lods);lod.RecalculateBounds();
  PrefabUtility.SaveAsPrefabAsset(root,Dir+"Donjon_Detailed.prefab");UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
 }
 static void Layer(GameObject g){foreach(var t in g.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;}
 static void Capture(Camera cam,string name){var rt=new RenderTexture(1600,1400,24);rt.antiAliasing=4;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var t=new Texture2D(1600,1400,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1600,1400),0,0);t.Apply();File.WriteAllBytes(Out+name+".png",t.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(t);}
 public static void Preview(){
  Directory.CreateDirectory(Out);var oldScene=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
  var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Donjon_Detailed.prefab"),scene);Layer(root);root.GetComponent<LODGroup>().enabled=false;root.transform.Find("LOD1").gameObject.SetActive(false);root.transform.Find("LOD2").gameObject.SetActive(false);
  var light=new GameObject("Studio_Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.color=new Color(1,.94f,.84f);light.transform.rotation=Quaternion.Euler(45,-30,0);light.cullingMask=1<<30;light.shadows=LightShadows.Soft;
  var fill=new GameObject("Studio_Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.22f;fill.color=new Color(.70f,.83f,1);fill.transform.rotation=Quaternion.Euler(30,150,0);fill.cullingMask=1<<30;
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.28f,.32f,.38f);RenderSettings.fog=false;
  var cam=new GameObject("Donjon_Camera").AddComponent<Camera>();cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.25f,.30f,.35f);cam.orthographic=true;cam.orthographicSize=19;cam.farClipPlane=200;cam.nearClipPlane=.05f;cam.transform.position=new Vector3(36,30,-46);cam.transform.LookAt(new Vector3(0,9,-2));
  var rs=root.transform.Find("LOD0").GetComponentsInChildren<Renderer>();
  foreach(var r in rs)r.enabled=!r.name.StartsWith("F-1")&&!r.name.StartsWith("B1_")&&!r.name.StartsWith("Foundation");
  Capture(cam,"exterior-model");
  foreach(var r in rs){string n=r.name;r.enabled=!(n.StartsWith("Roof")||n.StartsWith("ExteriorDecor")||n.Contains("Shell")||n.Contains("Tower")||n.Contains("Ceiling"));}
  cam.transform.position=new Vector3(34,36,-47);cam.transform.LookAt(new Vector3(0,5.4f,0));cam.orthographicSize=18;Capture(cam,"cutaway-model");
  for(int floor=-1;floor<3;floor++) {
   foreach(var r in rs)r.enabled=(r.name.StartsWith("F"+floor+"_")||(floor==-1&&r.name.StartsWith("B1_")))&&!r.name.Contains("Ceiling");
   cam.transform.position=new Vector3(0,48,0);cam.transform.rotation=Quaternion.Euler(90,0,0);cam.orthographicSize=14;Capture(cam,"plan-level-"+floor);
  }
  foreach(var r in rs)r.enabled=r.name.Contains("Stairs")||r.name.Contains("Floor");
  cam.transform.position=new Vector3(-25,18,-27);cam.transform.LookAt(new Vector3(-9,5.2f,-8));cam.orthographicSize=13;Capture(cam,"stairs-model");
  foreach(var r in rs)r.enabled=true;
  for(int f=-1;f<3;f++)foreach(float x in new[]{-4.4f,4.4f}){var p=new GameObject("Room_Light").AddComponent<Light>();p.type=LightType.Point;p.transform.position=new Vector3(x,f*5.4f+3.4f,0);p.intensity=2.5f;p.range=13;p.color=new Color(1,.84f,.61f);p.cullingMask=1<<30;}
  cam.orthographic=false;cam.fieldOfView=78;
  cam.transform.position=new Vector3(-1.1f,2.0f,-6.7f);cam.transform.LookAt(new Vector3(-5.1f,2.0f,1.5f));Capture(cam,"great-hall");
  cam.transform.position=new Vector3(-4.25f,7.3f,-6.7f);cam.transform.LookAt(new Vector3(-4.25f,7.2f,5.4f));Capture(cam,"throne-room");
  cam.fieldOfView=84;cam.transform.position=new Vector3(-.85f,13.0f,.55f);cam.transform.LookAt(new Vector3(-4.5f,12.55f,4.4f));Capture(cam,"princess-room");cam.fieldOfView=78;
  cam.transform.position=new Vector3(-4.2f,-3.7f,-6.9f);cam.transform.LookAt(new Vector3(-4.2f,-3.25f,2.5f));Capture(cam,"treasury");
  cam.transform.position=new Vector3(4.3f,-3.7f,-6.9f);cam.transform.LookAt(new Vector3(4.3f,-3.3f,2.5f));Capture(cam,"wine-cellar");
  cam.orthographic=true;foreach(var r in rs)r.enabled=r.name.StartsWith("Roof")||r.name.StartsWith("PrincessRoofExit")||(r.name.StartsWith("F2_")&&r.name.Contains("Stairs"));
  cam.transform.position=new Vector3(25,44,-32);cam.transform.LookAt(new Vector3(0,16.8f,0));cam.orthographicSize=15;Capture(cam,"roof-lookout");
  foreach(var r in rs)r.enabled=true;cam.transform.position=new Vector3(36,30,-46);cam.transform.LookAt(new Vector3(0,7,-2));cam.orthographicSize=19;
  EditorSceneManager.SaveScene(scene,Dir+"Donjon_Review.unity");EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(oldScene);
 }
 }
}
