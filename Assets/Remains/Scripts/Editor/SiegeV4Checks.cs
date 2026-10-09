using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace Remains.Editor {
 [InitializeOnLoad]
 public static class SiegeV4Checks {
  const string Out="output/siege-v4/";static int step,shot,lastHits;static double at;static List<string> checks=new List<string>();static SiegeDirector d;static SiegeWall wall;
  static SiegeV4Checks(){EditorApplication.playModeStateChanged+=Changed;}
  [MenuItem("Remains/Check Siege V4 Gameplay")]
  public static void Run(){if(File.Exists(Out+"checks-error.txt"))File.Delete(Out+"checks-error.txt");SessionState.SetBool("SiegeV4Checks",true);EditorApplication.isPlaying=true;}
  static void Changed(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("SiegeV4Checks",false)){step=0;shot=0;checks.Clear();at=Time.time+2;EditorApplication.update+=Tick;}}
  static void Assert(bool b,string name){if(!b)throw new Exception(name);checks.Add("PASS: "+name);File.WriteAllLines(Out+"gameplay-checks.txt",checks);}
  static void SpawnShot(SiegeEngineV4 engine,Vector3 origin,Vector3 target,SiegeShot type){Assert(SiegeEngineV4.Ballistic(origin,target,type==SiegeShot.Bolt?75:38,type==SiegeShot.Stone,out var velocity),"Ballistic solution "+type);var go=UnityEngine.Object.Instantiate(engine.ammunition,origin,Quaternion.LookRotation(velocity));go.SetActive(true);var p=go.AddComponent<SiegeProjectile>();p.velocity=velocity;p.shot=type;p.owner=engine.transform;p.player=d.player.transform;}
  static void Tick(){if(Time.time<at)return;try{
   if(step==0){d=UnityEngine.Object.FindFirstObjectByType<SiegeDirector>();Assert(d&&d.engines.Length==3,"3 siege engines bound");Assert(d.smiths.Length==3,"3 blacksmith stations");Assert(!d.gate.Ram(2.7f,20,d.gate.transform.position)&&!d.gate.Ram(4,4.9f,d.gate.transform.position),"Gate rejects slow hits and short runup");Assert(d.bridge.mechanism.angle==78,"Bridge starts raised");
    var ground=GameObject.Find("Battlefield_V2/Round_Battlefield_Ground").GetComponent<MeshCollider>();for(float z=-140;z<=-126;z+=2){ground.Raycast(new Ray(new Vector3(0,80,z),Vector3.down),out var h,100);Assert(h.point.y<=16.1f,"Approach below bridge at z="+z);}
    step=1;
   }
   if(step==1){int side=shot/3;SpawnShot(d.engines[0],new Vector3(side==0?-7.3f:7.3f,18,-140),d.bridge.chains[side].transform.position,SiegeShot.Bolt);step=2;at=Time.time+2;return;}
   if(step==2){int side=shot/3;Assert(d.bridge.chains[side].hits==shot%3+1,"Physical bolt hits chain "+side+" count "+(shot%3+1)+" actual="+d.bridge.chains[side].hits+"; impact="+SiegeProjectile.LastImpact);if(shot<5)Assert(!d.bridge.fallen,"Bridge held before both chains broken");shot++;if(shot<6){step=1;return;}step=3;at=Time.time+3;return;}
   if(step==3){Assert(d.bridge.fallen&&d.bridge.mechanism.angle<.01f,"Both chains release bridge to flat deck");var e=d.engines[1];e.transform.SetPositionAndRotation(new Vector3(0,16.04f,-150),Quaternion.identity);e.speed=e.runup=0;Physics.SyncTransforms();step=4;at=Time.time;return;}
   if(step==4){var e=d.engines[1];e.Drive(1,0,d.player.transform,.1f);Physics.SyncTransforms();if(e.transform.position.z< -64.6f){if(Time.time-at>35)throw new Exception("Ram route blocked at "+e.transform.position);return;}Assert(d.gate.hits==1,"Ram crosses entire bridge and impacts gate");step=5;shot=1;at=Time.time+.8;return;}
   if(step==5){if(shot<10){Assert(d.gate.Ram(3.5f,6,d.gate.transform.position),"Valid ram hit "+(shot+1));shot++;at=Time.time+.7;return;}step=6;at=Time.time+2;return;}
   if(step==6){Assert(d.gate.Broken&&d.gate.gate.open&&Quaternion.Angle(d.gate.gate.leftHinge.localRotation,Quaternion.identity)>85,"Tenth hit breaks bar and opens leaves");wall=GameObject.Find("Map_LargeCastle_Blockout/Castle_150x130m/Wall_0/Destructible_V4/wall-blocks-0").GetComponent<SiegeWall>();lastHits=wall.destroyedCount;SpawnShot(d.engines[2],new Vector3(-35,20,-142),wall.transform.TransformPoint(new Vector3(0,22,-3)),SiegeShot.Stone);step=7;at=Time.time+10;return;}
   if(step==7){Assert(wall.destroyedCount>lastHits,"Physical catapult stone breaks wall blocks");var pos=wall.transform.TransformPoint(new Vector3(0,22,-10));bool blocked=Physics.Raycast(pos,wall.transform.forward,out var h,20)&&h.collider.GetComponentInParent<SiegeWall>()==wall;Assert(!blocked,"Wall breach has no intact wall collider");Capture("siege-breached",new Vector3(75,65,-142),new Vector3(0,23,-53),true,68);checks.Add("PASS: all integration checks completed");File.WriteAllLines(Out+"gameplay-checks.txt",checks);Finish();}
  }catch(Exception e){File.WriteAllText(Out+"checks-error.txt",e.ToString());Debug.LogException(e);Finish();}}
  static void Finish(){EditorApplication.update-=Tick;SessionState.SetBool("SiegeV4Checks",false);EditorApplication.isPlaying=false;}
  public static void Capture(string name,Vector3 pos,Vector3 look,bool ortho=false,float size=30){var go=new GameObject("PreviewCamera");var cam=go.AddComponent<Camera>();cam.transform.position=pos;cam.transform.LookAt(look);cam.orthographic=ortho;cam.orthographicSize=size;cam.fieldOfView=60;cam.farClipPlane=1500;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.45f,.60f,.65f);var rt=new RenderTexture(1600,1100,24);RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes(Out+name+".png",tex.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
  [MenuItem("Remains/Preview Siege V4")]
  public static void Preview(){Capture("camp",new Vector3(31,32,-157),new Vector3(0,19,-190),true,24);Capture("bridge",new Vector3(44,40,-142),new Vector3(0,17,-104),true,40);Capture("grass",new Vector3(-64,19,-160),new Vector3(-90,18,-150));Capture("castle-siege",new Vector3(130,110,-170),new Vector3(0,24,-32),true,90);File.WriteAllText(Out+"preview.done","ok");}
 }
}
