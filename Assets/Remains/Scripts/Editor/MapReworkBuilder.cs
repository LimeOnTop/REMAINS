using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
namespace Remains.Editor {
// Island map rework (Oct 2026): irregular shore, river banks, mouths and outer rim; hills, pits and sunken trail;
// new ground shader, grass, and the low-poly nature kit from output/map-rework/build_nature.py.
// The castle island interior and the gate approach keep the previous ground heights exactly (siege and castle depend on them).
public static class MapReworkBuilder {
 const string Dir="Assets/Remains/MapRework/";
 const string Models=Dir+"Models/";
 const string ScenePath="Assets/Remains/Scenes/Remains_RoundMap.unity";
 static System.Random rng;
 static float R(float a,float b)=>a+(b-a)*(float)rng.NextDouble();
 static Vector2 P(float a,float r)=>new Vector2(Mathf.Sin(a)*r,Mathf.Cos(a)*r+8);
 static float Ang(Vector2 p)=>Mathf.Atan2(p.x,p.y-8);
 static float Rad(Vector2 p)=>new Vector2(p.x,p.y-8).magnitude;
 static float SS(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
 static float Seg(Vector2 p,Vector2 a,Vector2 b,out float t){Vector2 d=b-a;t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return Vector2.Distance(p,a+d*t);}

 // ---- Previous contours (BattlefieldV2Builder) ----
 static float Inner0(float a)=>91+5*Mathf.Cos(a)+4*Mathf.Sin(a*3+.5f);
 static float Outer0(float a)=>Inner0(a)+43+3*Mathf.Sin(a*2);
 static float Rim0(float a)=>292+4*Mathf.Sin(a*7)+3*Mathf.Sin(a*13+1);
 static readonly Vector2[] camps={new Vector2(-178,-35),new Vector2(0,-195),new Vector2(178,-35)};

 // ---- Irregularity ----
 static readonly int[] freqs={2,3,5,7,11,17,23,31};
 static readonly float[] amps={1,.8f,.65f,.5f,.38f,.26f,.18f,.12f};
 static float[,] phases;
 static float Wave(float a,int seed){float s=0,n=0;for(int i=0;i<freqs.Length;i++){s+=amps[i]*Mathf.Sin(freqs[i]*a+phases[seed,i]);n+=amps[i];}return Mathf.Clamp(s/n*2.2f,-1,1);}
 static float Warp(Vector2 p,float o)=>(Mathf.PerlinNoise(p.x*.031f+o,p.y*.031f+5)-.5f)*9+(Mathf.PerlinNoise(p.x*.085f+40+o,p.y*.085f+3)-.5f)*3.5f;
 // Fades the irregularity out in the south sector, where the bridge and siege approach cross the river.
 static float BridgeMask(float a)=>SS(.2f,.42f,Mathf.Abs(Mathf.DeltaAngle(a*Mathf.Rad2Deg,180))*Mathf.Deg2Rad);
 static float Shore(Vector2 p){float a=Ang(p),m=BridgeMask(a);return Mathf.Max(Inner0(a)-1.5f,Inner0(a)+m*(4+8.5f*Wave(a,1)+1.3f*Warp(p,0)));}
 static float Outer(Vector2 p){float a=Ang(p),m=BridgeMask(a);float o=Outer0(a)+m*(2+3*Wave(a,1)+9*Wave(a,2)+1.3f*Warp(p,60));return Mathf.Max(o,Shore(p)+28);}
 static float Rim(float a)=>Rim0(a)+9*Wave(a,3)-2;
 static float BeachWidth(float a)=>3+5*(Wave(a,4)*.5f+.5f);
 static float CliffWidth(float a)=>2.5f+3.5f*(Wave(a,5)*.5f+.5f);

 // ---- River mouths: meandering channels with varying half-width ----
 static List<Vector2>[] mouthLine;static List<float>[] mouthHalf;
 static void BuildMouths(){
  float[] at={-.70f,.78f};mouthLine=new List<Vector2>[2];mouthHalf=new List<float>[2];
  for(int m=0;m<2;m++){mouthLine[m]=new List<Vector2>();mouthHalf[m]=new List<float>();float s1=R(0,6),s2=R(0,6),s3=R(0,6);
   for(int i=0;i<=32;i++){float t=i/32f;float r=Mathf.Lerp(108,Rim0(at[m])+40,t);
    float lateral=(Mathf.Sin(t*Mathf.PI*2.2f+s1)*10+Mathf.Sin(t*Mathf.PI*5.3f+s2)*4)*SS(0,.22f,t);
    mouthLine[m].Add(P(at[m]+lateral/r,r));
    float half=12+3.5f*Mathf.Sin(t*7+s3)+2*Mathf.Sin(t*13.7f+s1);half=Mathf.Lerp(17,half,SS(0,.18f,t));mouthHalf[m].Add(half);}}
 }
 // Signed distance to a mouth channel edge (negative inside the water).
 static float Mouth(Vector2 p){float best=999;for(int m=0;m<2;m++){var l=mouthLine[m];var h=mouthHalf[m];for(int i=1;i<l.Count;i++){float d=Seg(p,l[i-1],l[i],out float t)-Mathf.Lerp(h[i-1],h[i],t);if(d<best)best=d;}}return best-.3f*Warp(p,120);}

 // ---- Trail (ring path between the camps) ----
 static List<Vector2> trail=new List<Vector2>();
 static List<Vector2[]> routes=new List<Vector2[]>();
 static void Routes(){trail.Clear();routes.Clear();for(int i=0;i<=100;i++){float a=Mathf.Lerp(-Mathf.PI/2-.25f,-Mathf.PI*1.5f+.25f,i/100f);float r=182+5*Mathf.Sin(i*.24f);trail.Add(P(a,r));}routes.Add(trail.ToArray());routes.Add(new[]{new Vector2(0,-214),new Vector2(0,-195),new Vector2(0,-151),new Vector2(0,-128)});routes.Add(new[]{new Vector2(0,-55),new Vector2(0,-35),new Vector2(-4,-15),new Vector2(3,5),new Vector2(-2,24)});routes.Add(new[]{new Vector2(-4,-15),new Vector2(-17,-20),new Vector2(-24,-25)});routes.Add(new[]{new Vector2(3,5),new Vector2(15,0),new Vector2(25,-1)});}
 static float TrailDist(Vector2 p){float d=999;for(int i=1;i<trail.Count;i++)d=Mathf.Min(d,Seg(p,trail[i-1],trail[i],out _));return d;}
 static float RouteDist(Vector2 p){float d=999;foreach(var r in routes)for(int i=1;i<r.Length;i++)d=Mathf.Min(d,Seg(p,r[i-1],r[i],out _));return d;}

 // ---- Preserved heights (castle island interior + gate approach), sampled from the previous ground collider ----
 const float PX0=-110,PZ0=-232,PStep=1;static float[,] preserved;static int PW,PH;
 static void SamplePreserved(MeshCollider old){PW=221;PH=346;preserved=new float[PW,PH];
  for(int i=0;i<PW;i++)for(int j=0;j<PH;j++){float x=PX0+i*PStep,z=PZ0+j*PStep;preserved[i,j]=old.Raycast(new Ray(new Vector3(x,200,z),Vector3.down),out var hit,400)?hit.point.y:3.4f;}}
 static float Preserved(Vector2 p){float fx=Mathf.Clamp((p.x-PX0)/PStep,0,PW-1.001f),fz=Mathf.Clamp((p.y-PZ0)/PStep,0,PH-1.001f);int i=(int)fx,j=(int)fz;float u=fx-i,v=fz-j;return Mathf.Lerp(Mathf.Lerp(preserved[i,j],preserved[i+1,j],u),Mathf.Lerp(preserved[i,j+1],preserved[i+1,j+1],u),v);}
 // 1 inside the castle zone or the gate approach box, 0 where the new landscape applies.
 static float Keep(Vector2 p){float a=Ang(p),r=Rad(p);float castle=1-SS(Inner0(a)-17,Inner0(a)-15,r);float box=(1-SS(20,30,Mathf.Abs(p.x)))*SS(-232,-220,p.y)*(1-SS(-80,-74,p.y));return Mathf.Max(castle,box);}

 // ---- Height field ----
 static float Plateau(Vector2 p)=>16+2.1f*Mathf.PerlinNoise(p.x*.013f+12,p.y*.013f+17)+1.1f*Mathf.Sin(p.x*.024f)*Mathf.Cos(p.y*.026f);
 static float Hills(Vector2 p){float h=(Mathf.PerlinNoise(p.x*.011f+3,p.y*.011f+91)-.5f)*9+(Mathf.PerlinNoise(p.x*.03f+70,p.y*.03f+10)-.5f)*3;
  float pit=Mathf.PerlinNoise(p.x*.024f+55,p.y*.024f+21);h-=SS(.6f,.76f,pit)*4.2f;
  float bump=Mathf.PerlinNoise(p.x*.05f+9,p.y*.05f+77);h+=SS(.66f,.82f,bump)*2.2f;return h;}
 static float CampBlend(Vector2 p){float w=0;foreach(var c in camps)w=Mathf.Max(w,1-SS(16,25,Vector2.Distance(p,c)));return w;}
 public static float Ground(Vector2 p){
  float a=Ang(p),r=Rad(p),keep=Keep(p);
  float h=Landscape(p,a,r);
  if(keep>0)h=Mathf.Lerp(h,Preserved(p),keep);
  return h;
 }
 static float Landscape(Vector2 p,float a,float r){
  float shore=Shore(p),outer=Outer(p),rim=Rim(a);float h;
  if(r<shore){float top=Inner0(a)-16;float beachTop=Mathf.Max(top+5,shore-1-BeachWidth(a));float pres=Preserved(p);
   if(r<beachTop)h=Mathf.Lerp(pres,7.8f,SS(top,beachTop,r));
   else h=Mathf.Lerp(7.8f,7.15f,Mathf.InverseLerp(beachTop,shore-1,r))+.18f*(Mathf.PerlinNoise(p.x*.35f,p.y*.35f)-.5f);
   if(r>shore-1)h=Mathf.Lerp(h,7.0f,Mathf.InverseLerp(shore-1,shore,r));}
  else if(r<outer-2)h=Mathf.Lerp(7.0f,3.4f,SS(shore,shore+3,r));
  else{float approach=Mathf.Max(Mathf.Abs(p.x)-20,Mathf.Max(-226-p.y,p.y+74));float landMask=SS(outer+4,outer+22,r)*SS(rim-4,rim-18,r)*SS(4,26,approach);
   float camp=CampBlend(p);float land=Plateau(p)+Hills(p)*landMask*(1-camp);land=Mathf.Lerp(land,17,camp);
   h=Mathf.Lerp(3.4f,land,SS(outer-2,outer-2+CliffWidth(a),r));}
  // Mouth channels cut through the outer land to the rim waterfalls.
  if(r>shore+6){float m=Mouth(p);h=Mathf.Lerp(3.4f,h,SS(-.5f,4.5f,m));}
  // Sunken trail: about half a metre below the surrounding ground with soft shoulders.
  float td=TrailDist(p);if(td<7)h-=.55f*(1-SS(2.4f,5.6f,td))*(1-CampBlend(p));
  return h;
 }

 // ---- Surface colour / material weights ----
 static Color Grass(Vector2 p){float patch=Mathf.PerlinNoise(p.x*.045f+5,p.y*.045f+7);float dry=SS(.62f,.8f,Mathf.PerlinNoise(p.x*.012f+31,p.y*.012f+4));
  Color c=Color.Lerp(new Color(.20f,.37f,.11f),new Color(.36f,.53f,.16f),patch);return Color.Lerp(c,new Color(.52f,.55f,.22f),dry*.65f);}
 static void Surface(Vector2 p,float h,float slope,out Color col,out Vector2 uv){
  uv=Vector2.zero;float r=Rad(p),a=Ang(p);
  col=Grass(p);
  // Pits read slightly darker and lusher, crests slightly sun-bleached.
  float hill=r>Outer(p)?Hills(p):0;col*=1+Mathf.Clamp(hill,-3,3)*.025f;
  if(h<7.45f){col=Color.Lerp(new Color(.40f,.37f,.28f),new Color(.52f,.47f,.34f),Mathf.PerlinNoise(p.x*.2f,p.y*.2f));uv.y=1;}
  else if(h<8.25f&&r<Outer(p)){float s=SS(8.25f,7.8f,h);col=Color.Lerp(col,Color.Lerp(new Color(.70f,.62f,.43f),new Color(.80f,.72f,.52f),Mathf.PerlinNoise(p.x*.15f+3,p.y*.15f)),s);uv.y=s;}
  if(slope>.62f&&h>7.4f){float k=SS(.62f,.9f,slope);col=Color.Lerp(col,Color.Lerp(new Color(.36f,.35f,.31f),new Color(.55f,.52f,.45f),Mathf.PerlinNoise(p.x*.3f,p.y*.3f)),k);uv.y*=1-k;}
  float d=RouteDist(p);if(h>8&&d<5.2f){float w=1-SS(2.9f,4.9f,d);col=Color.Lerp(col,Color.Lerp(new Color(.47f,.35f,.21f),new Color(.60f,.46f,.29f),Mathf.PerlinNoise(p.x*.19f+31,p.y*.19f+17)),w);uv.x=w;}
  float camp=CampBlend(p);if(camp>.5f){col=Color.Lerp(col,new Color(.50f,.40f,.26f),SS(.5f,1,camp)*.9f);uv.x=Mathf.Max(uv.x,SS(.5f,1,camp)*.7f);}
 }

 // ---- Asset helpers ----
 static void SaveMesh(Mesh m,string name){Directory.CreateDirectory(Dir+"Generated");string path=Dir+"Generated/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){old.Clear();EditorUtility.CopySerialized(m,old);UnityEngine.Object.DestroyImmediate(m);}else AssetDatabase.CreateAsset(m,path);}
 static Mesh LoadMesh(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(Dir+"Generated/"+name+".asset");
 static Mesh MakeMesh(string name,List<Vector3> v,List<int> t,List<Color> c=null,List<Vector2> uv=null){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetTriangles(t,0);if(c!=null)m.SetColors(c);if(uv!=null)m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();SaveMesh(m,name);return LoadMesh(name);}
 static Material Mat(string name,Shader shader,Action<Material> setup){Directory.CreateDirectory(Dir+"Materials");string path=Dir+"Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;setup(m);EditorUtility.SetDirty(m);return m;}
 static Transform Group(Transform parent,string name,Vector3 pos=default){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;return t;}
 static GameObject MeshObject(Transform parent,string name,Mesh mesh,Material mat,bool collider=false){var g=Group(parent,name).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collider)g.AddComponent<MeshCollider>().sharedMesh=mesh;g.isStatic=true;return g;}

 static Dictionary<string,Material> nature=new Dictionary<string,Material>();
 static Material NatureMat(Material src){string name=src.name;if(nature.TryGetValue(name,out var m))return m;
  Color c=src.HasProperty("baseColorFactor")?src.GetColor("baseColorFactor"):Color.magenta;
  // Underwater life uses the sway shader (kelp bends, fish tails wag); everything else is plain URP Lit.
  if(name.StartsWith("Sea_")){bool fishMat=name.StartsWith("Sea_Fish");m=Mat(name,Shader.Find("Remains/UnderwaterSway"),x=>{x.SetColor("_BaseColor",c);x.SetFloat("_Mode",fishMat?1:0);x.SetFloat("_Height",2.4f);x.SetFloat("_Sway",.32f);x.SetFloat("_Speed",fishMat?1:.7f);x.enableInstancing=true;});}
  else m=Mat(name,Shader.Find("Universal Render Pipeline/Lit"),x=>{x.SetColor("_BaseColor",c);x.SetFloat("_Smoothness",.06f);x.enableInstancing=true;});
  nature[name]=m;return m;}
 // Instantiates a nature model so that its footprint is centred on p and its base rests at y (scale per axis: width, height, depth).
 static GameObject Place(Transform parent,string model,Vector3 pos,Vector3 scale,float yaw,float tiltX=0,float tiltZ=0,bool collider=true){
  var src=AssetDatabase.LoadAssetAtPath<GameObject>(Models+model+".glb");var mf=src.GetComponentInChildren<MeshFilter>();var mesh=mf.sharedMesh;var b=mesh.bounds;
  var g=new GameObject(model);g.transform.SetParent(parent,false);g.transform.position=pos;g.transform.rotation=Quaternion.Euler(tiltX,yaw,tiltZ);
  var child=new GameObject("mesh");child.transform.SetParent(g.transform,false);child.transform.localScale=scale;child.transform.localPosition=Vector3.Scale(new Vector3(-b.center.x,-b.min.y,-b.center.z),scale);
  child.AddComponent<MeshFilter>().sharedMesh=mesh;var mr=child.AddComponent<MeshRenderer>();mr.sharedMaterials=mf.GetComponent<MeshRenderer>().sharedMaterials.Select(NatureMat).ToArray();
  if(collider)child.AddComponent<MeshCollider>().sharedMesh=mesh;g.isStatic=true;child.isStatic=true;return g;}

 // ---- Builders ----
 static Transform field;
 public static void Build(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before rebuilding the map.");
  var scene=EditorSceneManager.GetActiveScene();if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath);
  string backup="Assets/Remains/Scenes/Backups/Before_MapRework_20261010.unity";if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
  rng=new System.Random(20261010);phases=new float[8,freqs.Length];for(int i=0;i<8;i++)for(int j=0;j<freqs.Length;j++)phases[i,j]=R(0,Mathf.PI*2);
  nature.Clear();Routes();BuildMouths();
  field=GameObject.Find("Battlefield_V2").transform;
  var groundGo=field.Find("Round_Battlefield_Ground").gameObject;
  if(preserved==null&&!LoadPreserved()){SamplePreserved(groundGo.GetComponent<MeshCollider>());SavePreserved();}
  foreach(var n in new[]{"Ring_River_Two_Mouths","Water_Ripples","River_Bank_Rocks","Trees_And_Firs","Climb_Ropes","Meadow_Upgrade_V3","Three_Dimensional_Grass","Meadow_V4","Nature_V4","Escarpment_Rocks"})foreach(Transform t in field.Cast<Transform>().Where(x=>x.name==n).ToList())UnityEngine.Object.DestroyImmediate(t.gameObject);
  foreach(Transform t in field.Cast<Transform>().Where(x=>x.name=="Mouth_Waterfall").ToList())UnityEngine.Object.DestroyImmediate(t.gameObject);
  Land(groundGo);Escarpment();Water();Ropes();
  Physics.SyncTransforms();
  var natureRoot=Group(field,"Nature_V4");Rocks(natureRoot);Trees(natureRoot);Underwater(natureRoot);Fish(natureRoot);
  Physics.SyncTransforms();
  Meadow();
  var fp=GameObject.Find("FirstPersonCamera");if(fp&&!fp.GetComponent<Remains.UnderwaterView>())fp.AddComponent<Remains.UnderwaterView>();
  foreach(var c in new[]{"Attack_Camp_West","Attack_Camp_South","Attack_Camp_East"}){var t=field.Find(c);if(t){var p=new Vector2(t.position.x,t.position.z);t.position=new Vector3(p.x,Ground(p),p.y);}}
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 }
 // Preserved heights are cached on disk so a rebuild never re-samples the already reworked ground.
 const string PreservedPath="output/map-rework/preserved_heights.bin";
 static void SavePreserved(){Directory.CreateDirectory("output/map-rework");using(var w=new BinaryWriter(File.Create(PreservedPath))){w.Write(PW);w.Write(PH);for(int i=0;i<PW;i++)for(int j=0;j<PH;j++)w.Write(preserved[i,j]);}}
 static bool LoadPreserved(){if(!File.Exists(PreservedPath))return false;using(var r=new BinaryReader(File.OpenRead(PreservedPath))){PW=r.ReadInt32();PH=r.ReadInt32();preserved=new float[PW,PH];for(int i=0;i<PW;i++)for(int j=0;j<PH;j++)preserved[i,j]=r.ReadSingle();}return true;}

 static MeshCollider groundCollider;
 static void Land(GameObject ground){
  const float step=2,X0=-316,Z0=-306;const int NX=317,NZ=317;
  var v=new List<Vector3>(NX*NZ);var c=new List<Color>(NX*NZ);var uv=new List<Vector2>(NX*NZ);var t=new List<int>();var inside=new bool[NX*NZ];
  var hs=new float[NX*NZ];
  for(int j=0;j<NZ;j++)for(int i=0;i<NX;i++){var p=new Vector2(X0+i*step,Z0+j*step);float a=Ang(p),rim=Rim(a)-1;inside[j*NX+i]=Rad(p)<=rim;if(Rad(p)>rim)p=P(a,rim);hs[j*NX+i]=Ground(p);v.Add(new Vector3(p.x,hs[j*NX+i],p.y));}
  for(int j=0;j<NZ;j++)for(int i=0;i<NX;i++){int k=j*NX+i;float hx=hs[Mathf.Min(i+1,NX-1)+j*NX]-hs[Mathf.Max(i-1,0)+j*NX],hz=hs[i+Mathf.Min(j+1,NZ-1)*NX]-hs[i+Mathf.Max(j-1,0)*NX];float slope=new Vector2(hx,hz).magnitude/(2*step);
   var p=new Vector2(v[k].x,v[k].z);Surface(p,v[k].y,slope,out var col,out var w);
   // Per-vertex value jitter gives the faceted, hand-painted low-poly look.
   col*=1+((float)rng.NextDouble()-.5f)*.07f;col.a=1;c.Add(col);uv.Add(w);}
  for(int j=0;j<NZ-1;j++)for(int i=0;i<NX-1;i++){int a=j*NX+i,b=a+1,d=a+NX,e=d+1;if(!(inside[a]||inside[b]||inside[d]||inside[e]))continue;
   // Alternate the diagonal so slopes don't show a uniform grain.
   if(((i+j)&1)==0){t.AddRange(new[]{a,d,e,a,e,b});}else{t.AddRange(new[]{a,d,b,b,d,e});}}
  var mesh=MakeMesh("Ground_V4",v,t,c,uv);
  var mat=Mat("Ground_V4",Shader.Find("Remains/GroundV4"),m=>{m.SetColor("_BaseColor",Color.white);m.SetFloat("_Detail",1);});
  ground.GetComponent<MeshFilter>().sharedMesh=mesh;ground.GetComponent<MeshRenderer>().sharedMaterial=mat;groundCollider=ground.GetComponent<MeshCollider>();groundCollider.sharedMesh=mesh;
 }
 static void Escarpment(){
  var go=field.Find("Outer_Rock_Escarpment").gameObject;
  // Rings from the rim lip down to the tapered underside of the floating island.
  float[] dy={0,-3.5f,-11,-21,-32,-44,-58};float[] dr={0,1.2f,2.2f,1.4f,.4f,-1.5f,-4.5f};
  int N=900;var v=new List<Vector3>();var c=new List<Color>();var t=new List<int>();var uv=new List<Vector2>();
  var jag=new float[dy.Length,N];for(int k=1;k<dy.Length;k++)for(int i=0;i<N;i++)jag[k,i]=R(-1.4f,1.8f)+2.2f*Wave(i*Mathf.PI*2/N,6+(k&1));
  for(int i=0;i<N;i++){float a=i*Mathf.PI*2/N;float rim=Rim(a)-1;var top=P(a,rim);float h=Ground(top);
   float y=h;for(int k=0;k<dy.Length;k++){float r=rim+dr[k]+(k==0?0:jag[k,i]);var q=P(a,r);if(k==1)y=h-3.5f;else if(k>1)y=Mathf.Min(y-4,14+dy[k]+R(-1.2f,1.2f));v.Add(new Vector3(q.x,y,q.y));
    float s=Mathf.PerlinNoise(a*40,k*.7f);c.Add(k==0?new Color(.40f,.39f,.34f):Color.Lerp(new Color(.38f,.37f,.35f),new Color(.60f,.58f,.53f),s)*(1-k*.035f));uv.Add(new Vector2(0,1));}}
  int L=dy.Length;for(int i=0;i<N;i++){int n=(i+1)%N;for(int k=0;k<L-1;k++){int a=i*L+k,b=n*L+k,d=i*L+k+1,e=n*L+k+1;t.AddRange(new[]{a,d,b,b,d,e});}}
  // Close the underside with a fan to a low centre so no hole is visible from below.
  int centre=v.Count;v.Add(new Vector3(0,-90,8));c.Add(new Color(.28f,.27f,.24f));uv.Add(new Vector2(0,1));for(int i=0;i<N;i++){int n=(i+1)%N;t.AddRange(new[]{i*L+L-1,centre,n*L+L-1});}
  var mesh=MakeMesh("Escarpment_V4",v,t,c,uv);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=Mat("Escarpment_V4",Shader.Find("Remains/GroundV4"),m=>{m.SetColor("_BaseColor",Color.white);m.SetFloat("_Detail",0);});var mc=go.GetComponent<MeshCollider>();if(mc)mc.sharedMesh=mesh;
  // Big rock buttresses break up the rim face.
  var rocks=Group(field,"Escarpment_Rocks");for(int i=0;i<150;i++){float a=i*Mathf.PI*2/150+R(-.015f,.015f);var p=P(a,Rim(a)+R(-2.5f,0));if(Mouth(p)<6)continue;float top=Ground(P(a,Rim(a)-2))-R(1.5f,6);float bottom=R(-50,-30);
   Place(rocks,"Cliff_Rock_"+rng.Next(4),new Vector3(p.x,bottom,p.y),new Vector3(R(9,15),(top-bottom)/2.4f,R(5,8)),a*Mathf.Rad2Deg+R(-12,12),R(-4,4),R(-4,4),false);}
 }
 static bool Wet(Vector2 p)=>Ground(p)<7.02f;
 static void Water(){
  // Indexed 1.5 m grid so the Gerstner swell in WaterSoT bends smoothly; only cells near wet ground are kept.
  const float st=1.5f;var v=new List<Vector3>();var t=new List<int>();var index=new Dictionary<Vector2Int,int>();
  int Vert(int i,int j){var key=new Vector2Int(i,j);if(!index.TryGetValue(key,out int k)){k=v.Count;index[key]=k;v.Add(new Vector3(i*st,7,j*st));}return k;}
  for(int j=-206;j<214;j++)for(int i=-212;i<212;i++){float x=i*st,z=j*st;var c=new Vector2(x+st/2,z+st/2);if(Rad(c)>Rim(Ang(c))+3)continue;
   if(!(Wet(new Vector2(x,z))||Wet(new Vector2(x+st,z))||Wet(new Vector2(x,z+st))||Wet(new Vector2(x+st,z+st))||Wet(c)))continue;
   int a0=Vert(i,j),b0=Vert(i,j+1),c0=Vert(i+1,j+1),d0=Vert(i+1,j);t.AddRange(new[]{a0,b0,c0,a0,c0,d0});}
  var water=Mat("Water_SoT",Shader.Find("Remains/WaterSoT"),m=>{m.SetColor("_ShallowColor",new Color(.25f,.85f,.76f));m.SetColor("_DeepColor",new Color(.03f,.33f,.46f));m.SetFloat("_DepthRange",5);m.SetFloat("_Visibility",4.2f);m.SetFloat("_WaveHeight",.16f);m.SetFloat("_FoamDistance",.9f);});
  var river=MeshObject(field,"Ring_River_Two_Mouths",MakeMesh("River_V4",v,t),water);var rr=river.GetComponent<MeshRenderer>();rr.shadowCastingMode=ShadowCastingMode.Off;rr.receiveShadows=true;
  var fall=Mat("Waterfall_V4",Shader.Find("Remains/Waterfall"),m=>{});
  // Waterfalls where each mouth channel leaves the rim: a ribbon draped over the lip (uv.y runs downward).
  for(int m=0;m<2;m++){var l=mouthLine[m];var h=mouthHalf[m];int i=0;while(i<l.Count-1&&Rad(l[i])<Rim(Ang(l[i]))-1)i++;var lip=l[i];float a=Ang(lip);float half=h[i]+1;var side=new Vector2(Mathf.Cos(a),-Mathf.Sin(a));var outward=new Vector2(Mathf.Sin(a),Mathf.Cos(a));
   var fv=new List<Vector3>();var ft=new List<int>();var fu=new List<Vector2>();float[] fy={7,4,-6,-22,-42};float[] fo={-3,.5f,2.5f,4.5f,7};int cols=10;
   for(int k=0;k<fy.Length;k++)for(int j=0;j<=cols;j++){var q=lip+side*Mathf.Lerp(-half,half,j/(float)cols)+outward*(fo[k]+Mathf.Sin(j*1.7f+k)*.5f);fv.Add(new Vector3(q.x,fy[k],q.y));fu.Add(new Vector2(j/(float)cols,(7-fy[k])/49f));}
   for(int k=0;k<fy.Length-1;k++)for(int j=0;j<cols;j++){int a0=k*(cols+1)+j,b0=a0+1,d0=a0+cols+1,e0=d0+1;ft.AddRange(new[]{a0,d0,b0,b0,d0,e0});}
   var wf=MeshObject(field,"Mouth_Waterfall",MakeMesh("Waterfall_V4_"+m,fv,ft,null,fu),fall);wf.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;}
 }
 // Riverbed life: kelp clumps (deeper water, thicker near banks), starfish and pebbles on the sand, a few sunken boulders.
 static void Underwater(Transform root){
  var g=Group(root,"Riverbed");int kelp=0,stars=0,stones=0,boulders=0;
  for(int i=0;i<30000&&(kelp<520||stars<220||stones<420||boulders<70);i++){
   var p=P(R(-Mathf.PI,Mathf.PI),R(80,300));if(Rad(p)>Rim(Ang(p))-4)continue;float gy=Ground(p);if(gy>6.1f)continue;
   if(OnApproach(p)&&Mathf.Abs(p.x)<9)continue; // keep the bridge piers clear
   if(!GroundHit(p,out var hit)||hit.point.y>6.2f)continue;float depth=7-hit.point.y;
   float patch=Mathf.PerlinNoise(p.x*.06f+11,p.y*.06f+4);
   if(kelp<520&&depth>1.3f&&patch>.45f&&Free(p,.8f)){float s=R(.7f,1.25f)*Mathf.Min(1,depth/3.2f);Place(g,"Kelp_"+rng.Next(3),hit.point-Vector3.up*.05f,Vector3.one*s,R(0,360),0,0,false);Occupy(p,.8f);kelp++;continue;}
   if(stars<220&&R(0,1)<.25f&&Free(p,.3f)){Place(g,"Starfish_"+rng.Next(3),hit.point+Vector3.up*.01f,Vector3.one*R(.8f,1.3f),R(0,360),R(-6,6),R(-6,6),false);Occupy(p,.3f);stars++;continue;}
   if(boulders<70&&R(0,1)<.08f&&Free(p,2)){float s=R(1.2f,2.6f);Place(g,"Boulder_"+rng.Next(4),hit.point-Vector3.up*s*.15f,new Vector3(s,s*R(.7f,1),s),R(0,360),R(-8,8),R(-8,8));Occupy(p,2);boulders++;continue;}
   if(stones<420&&Free(p,.25f)){Place(g,"Stone_"+rng.Next(3),hit.point-Vector3.up*.04f,Vector3.one*R(.3f,.8f),R(0,360),R(-10,10),R(-10,10),false);Occupy(p,.25f);stones++;}
  }
  File.WriteAllText("output/map-rework/underwater.json",$"{{\"kelp\":{kelp},\"starfish\":{stars},\"stones\":{stones},\"boulders\":{boulders}}}");
 }
 // Scripted fish loops: rings around the island at varied radius/depth and out-and-back loops up each river mouth.
 static float SwimHeight(Vector2 p,float frac){float bed=Ground(p);return Mathf.Clamp(Mathf.Lerp(bed+.5f,6.45f,frac),bed+.4f,6.45f);}
 static void Fish(Transform root){
  var g=Group(root,"Fish_Schools");var loops=new List<Vector3[]>();
  for(int k=0;k<6;k++){float lane=R(-.25f,.25f),frac=R(.25f,.8f),ph=R(0,6.28f),wig=R(2,5);bool reverse=k%2==1;var pts=new List<Vector3>();
   for(int a=0;a<360;a+=3){float ang=a*Mathf.Deg2Rad;var probe=P(ang,Inner0(ang));float rin=Shore(probe)+2.5f,rout=Outer(probe)-3.5f;float r=Mathf.Lerp(rin,rout,.5f+lane+.18f*Mathf.Sin(ang*wig+ph));var p=P(ang,r);pts.Add(new Vector3(p.x,SwimHeight(p,frac),p.y));}
   if(reverse)pts.Reverse();loops.Add(pts.ToArray());}
  for(int m=0;m<2;m++){var l=mouthLine[m];var h=mouthHalf[m];foreach(float frac in new[]{.35f,.7f}){var outbound=new List<Vector3>();var back=new List<Vector3>();
   for(int i=2;i<l.Count;i++){if(Rad(l[i])>Rim(Ang(l[i]))-18)break;var d=(l[i]-l[i-1]).normalized;var side=new Vector2(-d.y,d.x);float off=h[i]*.35f;
    var a=l[i]+side*off;var b=l[i]-side*off;outbound.Add(new Vector3(a.x,SwimHeight(a,frac),a.y));back.Add(new Vector3(b.x,SwimHeight(b,frac),b.y));}
   back.Reverse();outbound.AddRange(back);if(outbound.Count>6)loops.Add(outbound.ToArray());}}
  int total=0;
  for(int k=0;k<loops.Count;k++){var school=Group(g,"School_"+k);var fs=school.gameObject.AddComponent<Remains.FishSchool>();int n=rng.Next(4,9);int kind=rng.Next(3);
   fs.path=loops[k];fs.speed=R(1.6f,2.8f);fs.fish=new Transform[n];fs.offsets=new Vector3[n];fs.phases=new float[n];
   for(int i=0;i<n;i++){var f=Place(school,"Fish_"+(R(0,1)<.8f?kind:rng.Next(3)),loops[k][0],Vector3.one*R(1.4f,2.0f),0,0,0,false);f.isStatic=false;foreach(Transform c in f.transform)c.gameObject.isStatic=false;
    fs.fish[i]=f.transform;fs.offsets[i]=new Vector3(R(-1.2f,1.2f),R(-.35f,.35f),-i*R(.6f,1.1f));fs.phases[i]=R(0,6.28f);}total+=n;}
  File.WriteAllText("output/map-rework/fish.json",$"{{\"schools\":{loops.Count},\"fish\":{total}}}");
 }
 // Finds the toe (water line) and lip (top) of the outer bank along a ray from the island outwards.
 static bool OuterBank(float a,out Vector2 toe,out Vector2 lip,out float lipH){toe=lip=Vector2.zero;lipH=0;var probe=P(a,Outer0(a));float o=Outer(probe);float r0=o-10;bool found=false;
  for(float r=r0;r<o+16;r+=.5f){var p=P(a,r);float h=Ground(p);if(!found&&h>6.6f){toe=p;found=true;}if(found&&h>Ground(P(a,r+1.5f))-.15f&&h>10){lip=p;lipH=h;return true;}}return false;}
 static readonly float[] ropeAngles={-2.7f,-2.1f,-1.55f,-1.1f,.1f,1.12f,1.6f,2.1f,2.7f};
 static List<Vector2> ropeSpots=new List<Vector2>();
 static void Ropes(){ropeSpots.Clear();var g=Group(field,"Climb_Ropes");var iron=AssetDatabase.LoadAssetAtPath<Material>("Assets/Remains/BattlefieldV2/Dark_Iron.mat");var rope=AssetDatabase.LoadAssetAtPath<Material>("Assets/Remains/BattlefieldV2/Hemp.mat");
  foreach(float a in ropeAngles){if(!OuterBank(a,out var toe,out var lip,out float lipH))continue;var bankTop=lip+(lip-toe).normalized*1.2f;if(Mouth(bankTop)<10)continue;ropeSpots.Add(toe);ropeSpots.Add(lip);
   var top=new Vector3(bankTop.x,Ground(bankTop)+.25f,bankTop.y);var below=toe-(lip-toe).normalized*2.5f;var bottom=new Vector3(below.x,3.6f,below.y);var r=Group(g,"Climbable_Rope");
   Beam(r,"Iron_Stake",top-Vector3.up*.8f,top+Vector3.up*.7f,.12f,iron);var over=new Vector3(bottom.x,top.y+.5f,bottom.z);Beam(r,"Rope_Over_Lip",top+Vector3.up*.5f,over,.09f,rope);Beam(r,"Thick_Rope",over,bottom,.09f,rope);
   for(int j=0;j<10;j++){var k=GameObject.CreatePrimitive(PrimitiveType.Sphere);UnityEngine.Object.DestroyImmediate(k.GetComponent<Collider>());k.name="Knot";k.transform.SetParent(r,false);k.transform.localPosition=Vector3.Lerp(bottom,over,j/10f);k.transform.localScale=Vector3.one*.24f;k.GetComponent<Renderer>().sharedMaterial=rope;}
   var c=r.gameObject.AddComponent<Remains.BattlefieldClimbRope>();var outward=(lip-toe).normalized;c.bottom=bottom;c.top=top+new Vector3(outward.x,0,outward.y)*1.4f;}}
 static void Beam(Transform p,string n,Vector3 a,Vector3 b,float rad,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=(a+b)/2;g.transform.localScale=new Vector3(rad*2,(b-a).magnitude/2,rad*2);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);g.GetComponent<Renderer>().sharedMaterial=mat;g.isStatic=true;}

 // Spatial hash of occupied circles (x, radius, z) so scatter checks stay fast.
 static Dictionary<Vector2Int,List<Vector3>> occupied=new Dictionary<Vector2Int,List<Vector3>>();
 static Vector2Int Cell(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/8),Mathf.FloorToInt(p.y/8));
 static bool Free(Vector2 p,float radius){var c=Cell(p);for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)if(occupied.TryGetValue(c+new Vector2Int(x,y),out var list))foreach(var o in list)if((new Vector2(o.x,o.z)-p).sqrMagnitude<(o.y+radius)*(o.y+radius))return false;return true;}
 static void Occupy(Vector2 p,float radius){var c=Cell(p);if(!occupied.TryGetValue(c,out var list))occupied[c]=list=new List<Vector3>();list.Add(new Vector3(p.x,radius,p.y));}
 static bool NearRope(Vector2 p,float d)=>ropeSpots.Any(s=>Vector2.Distance(s,p)<d);
 static bool OnApproach(Vector2 p)=>Mathf.Abs(p.x)<24&&p.y<-70&&p.y>-226;
 // A bank face built from stacked, overlapping natural rocks: wide tiers (never stretched into columns), each set back a little,
 // with smaller boulders at the waterline.
 static void BankRocks(Transform parent,Vector2 toe,Vector2 landward,float lipH){
  float bottom=2.6f,top=lipH+R(-.6f,.8f),H=top-bottom;int tiers=Mathf.Max(1,Mathf.CeilToInt(H/5f));float th=H/tiers;
  float yawBase=Mathf.Atan2(-landward.x,-landward.y)*Mathf.Rad2Deg;
  for(int k=0;k<tiers;k++){var p=toe+landward*(k*.9f+R(-.4f,.4f))+new Vector2(-landward.y,landward.x)*R(-1.2f,1.2f);float w=R(2.8f,4.2f);float h=Mathf.Min((th+1.4f)/2.3f,w*1.25f);
   Place(parent,"Cliff_Rock_"+rng.Next(4),new Vector3(p.x,bottom+k*th-.7f,p.y),new Vector3(w,h,R(2.6f,3.8f)),yawBase+R(-28,28),R(-9,9),R(-7,7));}
  if(R(0,1)<.6f){var q=toe-landward*R(.8f,2f)+new Vector2(-landward.y,landward.x)*R(-2,2);float s=R(1.6f,3.2f);Place(parent,"Boulder_"+rng.Next(4),new Vector3(q.x,R(5.2f,6.4f),q.y),new Vector3(s,s*R(.8f,1.1f),s),R(0,360),R(-10,10),R(-10,10));}
 }
 static void Rocks(Transform root){
  occupied.Clear();var banks=Group(root,"River_Bank_Rocks");
  // Outer bank: stratified cliff blocks from the river bed up past the lip, facing the castle.
  int outerCount=0;for(float a=-Mathf.PI;a<Mathf.PI;a+=.03f){float aa=a+R(-.01f,.01f);if(!OuterBank(aa,out var toe,out var lip,out float lipH))continue;var mid=(toe+lip)/2;if(Mouth(mid)<3||Mouth(lip)<2||NearRope(mid,4)||OnApproach(mid))continue;
   var dir=(lip-toe).normalized;BankRocks(banks,toe+dir*R(-.6f,.6f),dir,lipH);Occupy(lip,3.5f);outerCount++;}
  // Mouth channel banks, both sides.
  for(int m=0;m<2;m++){var l=mouthLine[m];var h=mouthHalf[m];for(int i=1;i<l.Count;i++){float len=Vector2.Distance(l[i-1],l[i]);for(float s=0;s<len;s+=R(5.5f,7f)){float t=s/len;var c=Vector2.Lerp(l[i-1],l[i],t);if(Rad(c)<Outer(c)+2||Rad(c)>Rim(Ang(c))-5)continue;var d=(l[i]-l[i-1]).normalized;var side=new Vector2(-d.y,d.x);float half=Mathf.Lerp(h[i-1],h[i],t);
     foreach(int sg in new[]{-1,1}){Vector2 toe=c,lip=c;float lipH=0;bool ok=false;bool f=false;for(float o=half-6;o<half+10;o+=.5f){var q=c+side*sg*o;float gh=Ground(q);if(!f&&gh>6.6f){toe=q;f=true;}if(f&&gh>10&&gh>Ground(q+side*sg*1.5f)-.15f){lip=q;lipH=gh;ok=true;break;}}if(!ok)continue;
      BankRocks(banks,toe,side*sg,lipH);Occupy(lip,3.5f);}}}}
  // Island: outcrops on the slope above the beach and boulders on the sand.
  int tries=0,outcrops=0,boulders=0;while(outcrops<95&&tries++<4000){float a=R(-Mathf.PI,Mathf.PI);if(BridgeMask(a)<.5f)continue;float top=Inner0(a)-15,beachTop=Mathf.Max(top+5,Shore(P(a,Inner0(a)))-1-BeachWidth(a));var p=P(a,R(top+1,beachTop));if(!GroundHit(p,out var hit)||hit.normal.y<.55f)continue;
   Place(banks,"Cliff_Rock_"+rng.Next(4),hit.point-Vector3.up*R(.6f,1.4f),Vector3.one*R(1.1f,2.2f)+new Vector3(0,R(0,.6f),0),Mathf.Atan2(p.x,p.y-8)*Mathf.Rad2Deg+180+R(-25,25),R(-8,8),R(-8,8));outcrops++;}
  tries=0;while(boulders<120&&tries++<5000){float a=R(-Mathf.PI,Mathf.PI);if(BridgeMask(a)<.5f)continue;var p=P(a,Shore(P(a,Inner0(a)))-R(.5f,BeachWidth(a)+1.5f));if(!GroundHit(p,out var hit)||hit.point.y>8.6f)continue;
   Place(banks,"Boulder_"+rng.Next(4),hit.point-Vector3.up*R(.15f,.5f),Vector3.one*R(1.2f,3.6f),R(0,360));boulders++;}
  // Plateau: boulder groups with pebbles around them, mostly in pits and on crests.
  var field2=Group(root,"Plateau_Boulders");int groups=0;tries=0;while(groups<75&&tries++<6000){var p=P(R(-Mathf.PI,Mathf.PI),R(150,280));if(!LandClear(p,10)||!Free(p,6))continue;int n=rng.Next(1,4);for(int i=0;i<n;i++){var q=p+new Vector2(R(-4,4),R(-4,4));if(!GroundHit(q,out var hit))continue;float s=R(1.2f,3.4f)*(i==0?1.3f:.8f);Place(field2,"Boulder_"+rng.Next(4),hit.point-Vector3.up*s*.12f,new Vector3(s,s*R(.8f,1.2f),s),R(0,360),R(-6,6),R(-6,6));}
   for(int i=0;i<rng.Next(2,6);i++){var q=p+new Vector2(R(-7,7),R(-7,7));if(!GroundHit(q,out var hit))continue;Place(field2,"Stone_"+rng.Next(3),hit.point-Vector3.up*.05f,Vector3.one*R(.35f,.9f),R(0,360),R(-10,10),R(-10,10),false);}
   Occupy(p,6);groups++;}
 }
 static bool GroundHit(Vector2 p,out RaycastHit hit)=>groundCollider.Raycast(new Ray(new Vector3(p.x,150,p.y),Vector3.down),out hit,300);
 static bool LandClear(Vector2 p,float pathClear){float a=Ang(p),r=Rad(p);if(r<Outer(p)+9||r>Rim(a)-9||Mouth(p)<9||TrailDist(p)<pathClear||OnApproach(p)||NearRope(p,7))return false;foreach(var c in camps)if(Vector2.Distance(p,c)<24)return false;return Ground(p)>9;}
 static void Trees(Transform root){
  var g=Group(root,"Trees_And_Firs");int trees=0,bushes=0;
  for(int i=0;i<20000&&trees<1050;i++){var p=P(R(-Mathf.PI,Mathf.PI),R(140,288));if(!LandClear(p,7.5f))continue;float forest=Mathf.PerlinNoise(p.x*.025f+8,p.y*.025f+7);if(forest<.40f||!Free(p,2.2f))continue;
   bool pine=Mathf.PerlinNoise(p.x*.04f+21,p.y*.04f+2)+R(-.2f,.2f)>.48f;float h=pine?R(10,18):R(8,13.5f);float w=h*(pine?R(.85f,1.05f):R(.9f,1.15f));
   if(!GroundHit(p,out var hit))continue;var t=Place(g,(pine?"Pine_":"Oak_")+rng.Next(3),hit.point-Vector3.up*.15f,new Vector3(w,h,w),R(0,360),R(-3,3),R(-3,3),false);
   var col=t.AddComponent<CapsuleCollider>();col.height=h*.55f;col.radius=Mathf.Max(.45f,w*.05f);col.center=new Vector3(0,col.height/2,0);Occupy(p,2.2f);trees++;}
  for(int i=0;i<9000&&bushes<300;i++){var p=P(R(-Mathf.PI,Mathf.PI),R(138,288));if(!LandClear(p,5.5f))continue;float forest=Mathf.PerlinNoise(p.x*.025f+8,p.y*.025f+7);if(forest<.34f||!Free(p,1.2f))continue;if(!GroundHit(p,out var hit))continue;
   float s=R(1.6f,3.4f);Place(g,"Bush_"+rng.Next(2),hit.point-Vector3.up*.1f,new Vector3(s,s*R(.8f,1.1f),s),R(0,360),0,0,false);Occupy(p,1.2f);bushes++;}
 }
 static void Meadow(){
  var env=Group(field,"Meadow_V4");
  var grassMat=Mat("Grass_V4",Shader.Find("Remains/MeadowV3"),m=>{m.SetColor("_BaseColor",Color.white);m.SetFloat("_DetailStrength",0);m.SetFloat("_WindStrength",.09f);});
  var chunks=new Dictionary<Vector2Int,(List<Vector3> v,List<int> t,List<Color> c,List<Vector2> uv)>();int clumps=0;
  // Dense, tall meadow (v4.1): ~0.8 m spacing, 10-14 blades up to ~1 m.
  for(float z=-300;z<312;z+=.8f)for(float x=-306;x<306;x+=.8f){
   var p=new Vector2(x+R(-.35f,.35f),z+R(-.35f,.35f));float r=Rad(p);if(r>Rim(Ang(p))-2)continue;if(RouteDist(p)<4.2f||CampBlend(p)>.3f||OnApproach(p))continue;
   if(!GroundHit(p,out var hit)||hit.point.y<8.3f||hit.normal.y<.8f)continue;
   if(r<90&&Physics.Raycast(hit.point+Vector3.up*2.3f,Vector3.down,out var topHit,3f)&&topHit.collider!=groundCollider)continue;
   if(!Free(p,.2f))continue;
   float patch=Mathf.PerlinNoise(p.x*.071f+3,p.y*.071f+8);if(patch<.1f||R(0,1)>.96f)continue;
   var key=new Vector2Int(Mathf.FloorToInt(p.x/32),Mathf.FloorToInt(p.y/32));if(!chunks.TryGetValue(key,out var ch)){ch=(new List<Vector3>(),new List<int>(),new List<Color>(),new List<Vector2>());chunks[key]=ch;}
   Color baseC=Grass(p);Color dark=baseC*R(.55f,.7f);dark.a=1;Color light=Color.Lerp(baseC*1.35f,new Color(.70f,.78f,.36f),R(.1f,.4f));light.a=1;
   float height=R(.5f,.85f)*(patch>.65f?1.25f:1);clumps++;int blades=rng.Next(10,15);
   for(int b=0;b<blades;b++){float a=R(0,Mathf.PI*2);var dx=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*R(.05f,.085f);var bend=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*R(.08f,.28f);var foot=hit.point+new Vector3(R(-.32f,.32f),-.02f,R(-.32f,.32f));float h=height*R(.55f,1.2f);int k=ch.v.Count;
    ch.v.AddRange(new[]{foot-dx,foot+dx,foot+Vector3.up*h*.55f+bend*.35f-dx*.6f,foot+Vector3.up*h*.55f+bend*.35f+dx*.6f,foot+Vector3.up*h+bend});
    ch.t.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3,k+2,k+4,k+3});var mid=Color.Lerp(dark,light,.6f);ch.c.AddRange(new[]{dark,dark,mid,mid,light});ch.uv.AddRange(new[]{Vector2.zero,Vector2.zero,new Vector2(0,.55f),new Vector2(0,.55f),Vector2.up});}
   // Occasional seed stalk or wildflower above the clump.
   if(R(0,1)<.07f){var top=hit.point+Vector3.up*height*R(1.2f,1.6f)+new Vector3(R(-.1f,.1f),0,R(-.1f,.1f));float fr=R(0,1);Color fc=fr<.4f?new Color(.93f,.92f,.82f):fr<.75f?new Color(.95f,.78f,.22f):new Color(.62f,.48f,.86f);int k=ch.v.Count;var s=new Vector3(.012f,0,0);
    ch.v.AddRange(new[]{hit.point-s,hit.point+s,top});ch.t.AddRange(new[]{k,k+2,k+1});ch.c.AddRange(new[]{dark,dark,MidTone(dark,light)});ch.uv.AddRange(new[]{Vector2.zero,Vector2.zero,Vector2.up});
    for(int q=0;q<4;q++){float a=q*Mathf.PI/2+R(0,.4f);var o=new Vector3(Mathf.Cos(a),R(-.2f,.25f),Mathf.Sin(a))*.07f;var o2=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*.035f;int f=ch.v.Count;ch.v.AddRange(new[]{top,top+o+o2,top+o-o2});ch.t.AddRange(new[]{f,f+1,f+2});ch.c.AddRange(new[]{fc,fc,fc});ch.uv.AddRange(new[]{Vector2.up,Vector2.up,Vector2.up});}}
  }
  foreach(var pair in chunks){var mesh=MakeMesh("Grass_V4_"+pair.Key.x+"_"+pair.Key.y,pair.Value.v,pair.Value.t,pair.Value.c,pair.Value.uv);var go=MeshObject(env,"Grass_"+pair.Key.x+"_"+pair.Key.y,mesh,grassMat);var mr=go.GetComponent<MeshRenderer>();mr.shadowCastingMode=ShadowCastingMode.Off;
   var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.045f,new Renderer[]{mr})});lod.RecalculateBounds();}
  // Draped earth paths with a broken edge (same routes as before, following the sunken trail).
  var pv=new List<Vector3>();var pt=new List<int>();var pc=new List<Color>();var puv=new List<Vector2>();
  foreach(var route in routes)for(int s=1;s<route.Length;s++){Vector2 a=route[s-1],b=route[s];int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));Vector2 dir=(b-a).normalized,side=new Vector2(-dir.y,dir.x);
   for(int j=0;j<steps;j++)for(int lane=0;lane<6;lane++){Vector2 p0=Vector2.Lerp(a,b,j/(float)steps),p1=Vector2.Lerp(a,b,(j+1f)/steps);float w0=3.55f+.35f*Mathf.Sin(p0.x*.8f+p0.y*.65f),w1=3.55f+.35f*Mathf.Sin(p1.x*.8f+p1.y*.65f);float l=lane/3f-1,rn=(lane+1)/3f-1;
    var qs=new[]{p0+side*l*w0,p1+side*l*w1,p1+side*rn*w1,p0+side*rn*w0};var hits=new RaycastHit[4];bool ok=true;for(int k=0;k<4;k++)if(!GroundHit(qs[k],out hits[k])||hits[k].point.y<8)ok=false;if(!ok)continue;
    int n=pv.Count;foreach(var h in hits){float variation=Mathf.PerlinNoise(h.point.x*.19f+31,h.point.z*.19f+17);pv.Add(h.point+Vector3.up*.04f);pc.Add(Color.Lerp(new Color(.47f,.35f,.21f),new Color(.60f,.46f,.29f),variation));puv.Add(new Vector2(1,0));}pt.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}}
  MeshObject(env,"Earth_Paths",MakeMesh("Earth_Paths_V4",pv,pt,pc,puv),Mat("Earth_Path_V4",Shader.Find("Remains/GroundV4"),m=>{m.SetColor("_BaseColor",Color.white);m.SetFloat("_Detail",1);})).GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
  File.WriteAllText("output/map-rework/environment.json",$"{{\"grass_clumps\":{clumps},\"grass_chunks\":{chunks.Count},\"path_triangles\":{pt.Count/3}}}");
 }
 static Color MidTone(Color a,Color b)=>Color.Lerp(a,b,.6f);
}
}
