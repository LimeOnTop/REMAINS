using System.IO;
using UnityEngine;
namespace Remains.Editor {
// Fixed camera set used to compare the island map before and after the rework.
public static class MapReworkShots {
 struct Shot{public string name;public Vector3 pos,look;public float fov;public Shot(string n,Vector3 p,Vector3 l,float f=50){name=n;pos=p;look=l;fov=f;}}
 static readonly Shot[] shots={
  new Shot("overview",new Vector3(250,330,-360),new Vector3(0,0,25),48),
  new Shot("beach_island",new Vector3(-150,40,-150),new Vector3(-85,8,-55),55),
  new Shot("river_mouth_ne",new Vector3(70,60,40),new Vector3(150,4,165),55),
  new Shot("outer_cliff",new Vector3(-360,30,-120),new Vector3(-250,-10,-40),55),
  new Shot("path_ground",new Vector3(-116,19.5f,-132),new Vector3(-70,16.5f,-160),60),
  new Shot("trees_plateau",new Vector3(150,32,-230),new Vector3(170,15,-120),60),
  new Shot("river_top",new Vector3(-20,170,10),new Vector3(-110,0,40),55),
 };
 public static void Capture(string dir,string prefix,int w=1600,int h=900){
  Directory.CreateDirectory(dir);
  var go=new GameObject("__ShotCamera");var cam=go.AddComponent<Camera>();cam.farClipPlane=2500;cam.nearClipPlane=.3f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.63f,.73f,.80f);
  try{
   foreach(var s in shots){
    go.transform.position=s.pos;go.transform.LookAt(s.look);cam.fieldOfView=s.fov;
    var rt=new RenderTexture(w,h,24){antiAliasing=4};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
    var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
    File.WriteAllBytes(Path.Combine(dir,prefix+"_"+s.name+".jpg"),tex.EncodeToJPG(88));
    cam.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
   }
  } finally {Object.DestroyImmediate(go);}
 }
}
}
