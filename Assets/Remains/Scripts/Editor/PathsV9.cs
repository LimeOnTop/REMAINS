using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Remains.Editor {
// Path layout V9: replaces the old evenly curved ring strip with meandering trails cut into the terrain.
// 1) the old trail groove is relaxed back to the surrounding ground and repainted as grass;
// 2) new routes (Catmull-Rom with noise meanders and varying width) sink the terrain with soft shoulders and bumps;
// 3) the paint layer gets ragged dirt (g), cart ruts (b) and grass (r) around the new and old trails;
// 4) vegetation on the trail is hidden, nearby objects follow the new ground, boulders and stones line the edges.
public static class PathsV9 {
 const string TerrainPath="Battlefield_V2/Round_Battlefield_Ground",Backup="Assets/Remains/Backups/";
 class Route{public string name;public Vector2[] ctrl;public float[] widths;public float amp,sink,ruts;public int seed;public List<Vector2> pts=new List<Vector2>();public List<float> s=new List<float>(),w=new List<float>();public float length;}
 struct Hit{public float d,s,w;public Route r;}
 static readonly Vector2[] camps={new Vector2(-178,-35),new Vector2(0,-195),new Vector2(178,-35)};
 static readonly Vector2[] ropes={new Vector2(-58,-115),new Vector2(-122.6f,-63.7f),new Vector2(58.2f,-115.1f),new Vector2(108.5f,-55.5f)};
 static List<Route> routes;
 static float SS(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
 static float N(float x,float y)=>Mathf.PerlinNoise(x+1000,y+1000)-.5f;
 static Route R(string n,float amp,float sink,float ruts,int seed,params (float x,float z,float w)[] c)=>new Route{name=n,amp=amp,sink=sink,ruts=ruts,seed=seed,ctrl=c.Select(p=>new Vector2(p.x,p.z)).ToArray(),widths=c.Select(p=>p.w).ToArray()};
 static List<Route> Build(){
  var list=new List<Route>{
   R("Ring_West",7,.42f,1,11,(-176,-50,4.5f),(-182,-80,5),(-168,-112,5.5f),(-140,-140,4.2f),(-104,-162,5.2f),(-66,-180,6),(-30,-187,5),(-4,-185,6)),
   R("Ring_East",7,.42f,1,23,(176,-50,4.5f),(171,-84,5),(151,-118,4.4f),(122,-146,5.6f),(84,-168,5),(46,-182,6),(14,-187,5),(4,-185,6)),
   R("Approach",1.3f,.14f,1,37,(1,-188,10),(0,-176,30),(-1,-166,30),(2,-152,10),(-1.5f,-130,9),(1,-108,8.5f),(0,-86,8)),
   R("Spur_SW",3,.26f,0,41,(-104,-162,3),(-90,-148,2.6f),(-75,-131,3),(-63,-121,2.4f)),
   R("Spur_SE",3,.26f,0,43,(84,-168,3),(75,-150,2.6f),(65,-129,3),(62,-121,2.4f)),
   R("Spur_W",3,.26f,0,47,(-180,-82,3),(-161,-74,2.6f),(-141,-68,3),(-127,-65,2.4f)),
   R("Spur_E",3,.26f,0,53,(171,-82,3),(150,-71,2.6f),(129,-61,3),(113,-57,2.4f)),
  };
  foreach(var r in list)SampleRoute(r);return list;
 }
 static Vector2 CR(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t){float t2=t*t,t3=t2*t;return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3);}
 static void SampleRoute(Route r){
  var raw=new List<Vector2>();var rw=new List<float>();int n=r.ctrl.Length;
  for(int i=0;i<n-1;i++){var a=r.ctrl[Mathf.Max(0,i-1)];var b=r.ctrl[i];var c=r.ctrl[i+1];var d=r.ctrl[Mathf.Min(n-1,i+2)];int steps=Mathf.CeilToInt(Vector2.Distance(b,c));for(int k=0;k<steps;k++){float t=k/(float)steps;raw.Add(CR(a,b,c,d,t));rw.Add(Mathf.Lerp(r.widths[i],r.widths[i+1],SS(0,1,t)));}}
  raw.Add(r.ctrl[n-1]);rw.Add(r.widths[n-1]);
  float L=0;var ls=new List<float>{0};for(int i=1;i<raw.Count;i++){L+=Vector2.Distance(raw[i-1],raw[i]);ls.Add(L);}
  for(int i=0;i<raw.Count;i++){
   var dir=(raw[Mathf.Min(i+1,raw.Count-1)]-raw[Mathf.Max(i-1,0)]).normalized;var perp=new Vector2(-dir.y,dir.x);float s=ls[i];
   float fade=SS(0,22,s)*SS(0,22,L-s);float m=(N(s*.011f,r.seed)*2+N(s*.037f,r.seed*3.1f)*.6f)*r.amp*fade;
   r.pts.Add(raw[i]+perp*m);r.w.Add(rw[i]*(1+.28f*N(s*.05f,r.seed*1.7f)*2));
  }
  r.s.Add(0);for(int i=1;i<r.pts.Count;i++)r.s.Add(r.s[i-1]+Vector2.Distance(r.pts[i-1],r.pts[i]));r.length=r.s[r.s.Count-1];
 }
 // Uniform grid of segments for nearest-trail queries.
 static Dictionary<long,List<(Route r,int i)>> grid;const float Cell=8;
 static long Key(int x,int y)=>((long)x<<32)^(uint)y;
 static void Index(){grid=new Dictionary<long,List<(Route,int)>>();foreach(var r in routes)for(int i=1;i<r.pts.Count;i++){var a=r.pts[i-1];var b=r.pts[i];float pad=r.w[i]*.5f+8;int x0=Mathf.FloorToInt((Mathf.Min(a.x,b.x)-pad)/Cell),x1=Mathf.FloorToInt((Mathf.Max(a.x,b.x)+pad)/Cell),y0=Mathf.FloorToInt((Mathf.Min(a.y,b.y)-pad)/Cell),y1=Mathf.FloorToInt((Mathf.Max(a.y,b.y)+pad)/Cell);for(int x=x0;x<=x1;x++)for(int y=y0;y<=y1;y++){long k=Key(x,y);if(!grid.TryGetValue(k,out var l))grid[k]=l=new List<(Route,int)>();l.Add((r,i));}}}
 static bool Query(Vector2 p,out Hit best){best=new Hit{d=1e9f};bool any=false;if(!grid.TryGetValue(Key(Mathf.FloorToInt(p.x/Cell),Mathf.FloorToInt(p.y/Cell)),out var l))return false;
  foreach(var (r,i) in l){var a=r.pts[i-1];var b=r.pts[i];var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(ab.sqrMagnitude,1e-6f));float d=Vector2.Distance(p,a+ab*t);float w=Mathf.Lerp(r.w[i-1],r.w[i],t);
   // Wider routes win at junctions: compare distance relative to half width.
   if(!any||d-w*.5f<best.d-best.w*.5f){best=new Hit{d=d,s=Mathf.Lerp(r.s[i-1],r.s[i],t),w=w,r=r};any=true;}}
  return any;}
 static float CampFade(Vector2 p){float f=1;foreach(var c in camps)f=Mathf.Min(f,SS(13,24,Vector2.Distance(p,c)));return f;}
 static float RopeFade(Vector2 p){float f=1;foreach(var c in ropes)f=Mathf.Min(f,SS(3,9,Vector2.Distance(p,c)));return f;}
 static float Sink(Vector2 p){if(!Query(p,out var h))return 0;float half=h.w*.5f;float inside=1-SS(half-.6f,half+2.4f,h.d);if(inside<=0)return 0;
  float bumps=(N(p.x*.31f,p.y*.31f)*.16f+N(p.x*.9f,p.y*.9f)*.06f)*(1-SS(half-1.2f,half+.4f,h.d));
  return (h.r.sink*inside+bumps*Mathf.Min(1,h.r.sink*4))*CampFade(p)*RopeFade(p);}
 // Paint coverage of the trail: ragged edge from two noise scales, plus cart ruts along the main routes.
 static float PathMask(Vector2 p,out float rut){rut=0;if(!Query(p,out var h))return 0;float half=h.w*.5f;
  float rag=N(p.x*.33f,p.y*.33f)*1.5f+N(p.x*1.4f,p.y*1.4f)*.55f;float m=1-SS(half-.55f,half+.65f,h.d+rag);
  if(h.r.ruts>0&&m>0){float off=Mathf.Min(1.05f,half*.32f);float lane=Mathf.Min(Mathf.Abs(h.d-off),Mathf.Abs(h.d+off));
   // Signed side is lost in |d|, so ruts are two bands at the same offset; noise breaks them up along the trail.
   rut=(1-SS(.12f,.38f,Mathf.Abs(h.d-off)))*SS(-.1f,.2f,N(h.s*.08f,3.3f))*h.r.ruts*.85f;}
  return m*Mathf.Lerp(.55f,1,CampFade(p));}
 // Restores terrain and paint from the earliest backup, puts grass blades and nearby objects back, removes V9 boulders.
 [MenuItem("Remains/Map/Revert Paths V9")]
 public static string Revert(){
  routes=Build();Index();
  string Earliest(string n)=>AssetDatabase.FindAssets(n,new[]{"Assets/Remains/Backups"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(q=>q).First();
  var ground=GameObject.Find(TerrainPath);var col=ground.GetComponent<MeshCollider>();var mesh=ground.GetComponent<MeshFilter>().sharedMesh;var surface=ground.GetComponent<MapPaintSurface>();
  float H(Vector3 p){return col.Raycast(new Ray(new Vector3(p.x,300,p.z),Vector3.down),out var hit,600)?hit.point.y:float.NaN;}
  var nature=GameObject.Find("Battlefield_V2/Nature_V4").transform;var list=new List<(Transform,float)>();
  foreach(var grp in new[]{"Trees_And_Firs","Plateau_Boulders","River_Bank_Rocks"})foreach(Transform t in nature.Find(grp))if(t.gameObject.activeSelf)list.Add((t,H(t.position)));
  mesh.vertices=AssetDatabase.LoadAssetAtPath<Mesh>(Earliest("terrain_before_paths_v9")).vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);col.sharedMesh=null;col.sharedMesh=mesh;
  int moved=0;foreach(var (t,h0) in list){float h1=H(t.position);if(float.IsNaN(h0)||float.IsNaN(h1)||Mathf.Abs(h1-h0)<.01f)continue;t.position+=Vector3.up*(h1-h0);moved++;}
  int chunks=0;foreach(var gm in new[]{"Battlefield_V2/Meadow_V4","Battlefield_V2/Cape_Meadow_V7"}){var g=GameObject.Find(gm);if(!g)continue;foreach(var f in g.GetComponentsInChildren<MeshFilter>(true)){if(f.name=="Earth_Paths")continue;var m=f.sharedMesh;if(!m||!m.isReadable)continue;var mv=m.vertices;bool ch=false;var tr=f.transform;for(int i=0;i<mv.Length;i++){var w=tr.TransformPoint(mv[i]);float s=Sink(new Vector2(w.x,w.z));if(s!=0){w.y+=s;mv[i]=tr.InverseTransformPoint(w);ch=true;}}if(ch){m.vertices=mv;m.RecalculateBounds();EditorUtility.SetDirty(m);chunks++;}}}
  var bp=AssetDatabase.LoadAssetAtPath<MapPaintData>(Earliest("MapPaint_before_paths_v9"));surface.data.pixels=(byte[])bp.pixels.Clone();EditorUtility.SetDirty(surface.data);
  var b=nature.Find("Path_Boulders_V9");if(b)Object.DestroyImmediate(b.gameObject);
  surface.Refresh(true);AssetDatabase.SaveAssets();return $"reverted: objects moved back {moved}, grass chunks restored {chunks}";
 }
 [MenuItem("Remains/Map/Apply Paths V9")]
 public static string Apply(){
  var log=new List<string>();routes=Build();Index();
  var ground=GameObject.Find(TerrainPath);var mf=ground.GetComponent<MeshFilter>();var col=ground.GetComponent<MeshCollider>();var mesh=mf.sharedMesh;var surface=ground.GetComponent<MapPaintSurface>();
  Directory.CreateDirectory(Backup);string stamp=System.DateTime.Now.ToString("MMdd_HHmm");
  AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(mesh),Backup+"terrain_before_paths_v9_"+stamp+".asset");AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(surface.data),Backup+"MapPaint_before_paths_v9_"+stamp+".asset");
  float H(Vector2 p){return col.Raycast(new Ray(new Vector3(p.x,300,p.y),Vector3.down),out var hit,600)?hit.point.y:float.NaN;}
  // Objects that rest on the ground near any change: remember their ground height to follow it afterwards.
  var overlayT=GameObject.Find("Battlefield_V2/Meadow_V4").transform.Find("Earth_Paths");var overlay=overlayT?overlayT.gameObject:null;var oldVerts=overlay?overlay.GetComponent<MeshFilter>().sharedMesh.vertices:new Vector3[0];
  var ogrid=new Dictionary<long,List<Vector2>>();foreach(var v in oldVerts){var p=new Vector2(v.x,v.z);if(p.magnitude<95)continue;long k=Key(Mathf.FloorToInt(p.x/4),Mathf.FloorToInt(p.y/4));if(!ogrid.TryGetValue(k,out var l))ogrid[k]=l=new List<Vector2>();l.Add(p);}
  float OldDist(Vector2 p){float best=99;int cx=Mathf.FloorToInt(p.x/4),cy=Mathf.FloorToInt(p.y/4);for(int x=cx-2;x<=cx+2;x++)for(int y=cy-2;y<=cy+2;y++)if(ogrid.TryGetValue(Key(x,y),out var l))foreach(var q in l)best=Mathf.Min(best,Vector2.Distance(p,q));return best;}
  var nature=GameObject.Find("Battlefield_V2/Nature_V4").transform;var movers=new List<(Transform t,float h)>();int hidden=0;
  foreach(var grp in new[]{"Trees_And_Firs","Plateau_Boulders","River_Bank_Rocks"})foreach(Transform t in nature.Find(grp)){var p=new Vector2(t.position.x,t.position.z);
   bool onTrail=Query(p,out var h)&&h.d<h.w*.5f+(grp=="Trees_And_Firs"?(t.name.StartsWith("Bush")?1f:2.2f):.4f);
   if(onTrail&&CampFade(p)>0){if(t.gameObject.activeSelf){Undo.RecordObject(t.gameObject,"Paths V9");t.gameObject.SetActive(false);hidden++;}continue;}
   if((Query(p,out h)&&h.d<h.w*.5f+4)||OldDist(p)<7)movers.Add((t,H(p)));}
  // 1) Relax the old groove: corridor vertices iterate toward the average of their neighbours.
  var verts=mesh.vertices;var tris=mesh.triangles;var nb=new List<int>[verts.Length];
  var corridor=new bool[verts.Length];int nCorr=0;for(int i=0;i<verts.Length;i++){var p=new Vector2(verts[i].x,verts[i].z);if(p.magnitude>95&&CampFade(p)>0&&OldDist(p)<5){corridor[i]=true;nCorr++;}}
  for(int i=0;i<tris.Length;i+=3)for(int a=0;a<3;a++){int u=tris[i+a],v=tris[i+(a+1)%3];if(!corridor[u]&&!corridor[v])continue;(nb[u]??=new List<int>()).Add(v);(nb[v]??=new List<int>()).Add(u);}
  var y=verts.Select(v=>v.y).ToArray();for(int it=0;it<90;it++){var ny=(float[])y.Clone();for(int i=0;i<verts.Length;i++)if(corridor[i]&&nb[i]!=null){float s=0;foreach(int j in nb[i])s+=y[j];ny[i]=s/nb[i].Count;}y=ny;}
  float maxRaise=0;for(int i=0;i<verts.Length;i++)if(corridor[i]){maxRaise=Mathf.Max(maxRaise,y[i]-verts[i].y);verts[i].y=Mathf.Min(verts[i].y+.6f,Mathf.Max(verts[i].y,y[i]));}
  // 2) Cut the new trails.
  int cut=0;for(int i=0;i<verts.Length;i++){float s=Sink(new Vector2(verts[i].x,verts[i].z));if(s!=0){verts[i].y-=s;cut++;}}
  mesh.vertices=verts;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);col.sharedMesh=null;col.sharedMesh=mesh;
  log.Add($"terrain: old corridor verts {nCorr} (max raise {maxRaise:F2} m), cut verts {cut}, hidden plants/rocks {hidden}");
  // Grass blade meshes follow the same sink so trail shoulders have no floating blades.
  int bladeChunks=0;foreach(var gm in new[]{"Battlefield_V2/Meadow_V4","Battlefield_V2/Cape_Meadow_V7"}){var g=GameObject.Find(gm);if(!g)continue;foreach(var f in g.GetComponentsInChildren<MeshFilter>(true)){if(f.name=="Earth_Paths")continue;var m=f.sharedMesh;if(!m||!m.isReadable)continue;var mv=m.vertices;bool ch=false;var tr=f.transform;for(int i=0;i<mv.Length;i++){var w=tr.TransformPoint(mv[i]);float s=Sink(new Vector2(w.x,w.z));if(s!=0){w.y-=s;mv[i]=tr.InverseTransformPoint(w);ch=true;}}if(ch){m.vertices=mv;m.RecalculateBounds();EditorUtility.SetDirty(m);bladeChunks++;}}}
  int moved=0;foreach(var (t,h0) in movers){var p=new Vector2(t.position.x,t.position.z);float h1=H(p);if(float.IsNaN(h0)||float.IsNaN(h1)||Mathf.Abs(h1-h0)<.01f)continue;Undo.RecordObject(t,"Paths V9");t.position+=Vector3.up*(h1-h0);moved++;}
  log.Add($"grass chunks adjusted {bladeChunks}, objects re-grounded {moved}");
  // 3) Paint: old corridor and old painted approach become grass; new trails get dirt and ruts.
  var data=surface.data;Undo.RegisterCompleteObjectUndo(data,"Paths V9");data.Ensure();var px=data.pixels;int res=data.resolution;var rect=data.worldRect;int painted=0;
  for(int j=0;j<res;j++)for(int i=0;i<res;i++){
   var p=new Vector2(rect.x+(i+.5f)/res*rect.z,rect.y+(j+.5f)/res*rect.w);if(p.magnitude<75)continue;int k=(j*res+i)*4;
   float a=px[k+3]/255f,r=px[k]/255f,g=px[k+1]/255f,b=px[k+2]/255f;
   // Old dirt (painted approach) turns back into grass; the old overlay strip becomes grass too.
   if(a>0&&(g+b)>.05f){r=1;g=0;b=0;}
   float oldC=CampFade(p)>0?1-SS(2.5f,4.5f,OldDist(p)):0;if(oldC>0){r=(r*a+oldC*(1-a))/Mathf.Max(a+oldC*(1-a),1e-4f);a=Mathf.Max(a,oldC);if(a>0&&r>0){g=0;b=0;r=1;}}
   float m=PathMask(p,out float rut);
   if(m>0){float na=m+a*(1-m);r=r*a*(1-m)/na;g=(m*(1-rut)+0)/na;b=m*rut/na;a=na;painted++;}
   px[k]=(byte)Mathf.RoundToInt(Mathf.Clamp01(r)*255);px[k+1]=(byte)Mathf.RoundToInt(Mathf.Clamp01(g)*255);px[k+2]=(byte)Mathf.RoundToInt(Mathf.Clamp01(b)*255);px[k+3]=(byte)Mathf.RoundToInt(Mathf.Clamp01(a)*255);
  }
  EditorUtility.SetDirty(data);log.Add($"paint pixels on new trails {painted}");
  if(overlay){Undo.RecordObject(overlay,"Paths V9");overlay.SetActive(false);}
  // 4) Boulders and stones along the edges, a few half-buried stones inside the trail.
  log.Add(Boulders(nature,H));
  surface.Refresh(true);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(ground.scene);EditorSceneManager.SaveScene(ground.scene);
  File.WriteAllText("output/map-rework/paths_v9.json","{\"routes\":["+string.Join(",",routes.Select(r=>$"{{\"name\":\"{r.name}\",\"length\":{r.length:F1},\"points\":[{string.Join(",",r.pts.Where((q,i)=>i%6==0).Select(q=>$"[{q.x:F1},{q.y:F1}]"))}]}}"))+"],\"log\":[\""+string.Join("\",\"",log)+"\"]}");
  return string.Join("\n",log)+"\n"+string.Join("\n",routes.Select(r=>$"{r.name}: {r.length:F0} m"));
 }
 static string Boulders(Transform nature,System.Func<Vector2,float> H){
  var old=nature.Find("Path_Boulders_V9");if(old)Object.DestroyImmediate(old.gameObject);
  var root=new GameObject("Path_Boulders_V9").transform;root.SetParent(nature,false);
  var src=nature.Find("Plateau_Boulders");var boulders=new List<GameObject>();var stones=new List<GameObject>();
  foreach(Transform t in src){if(t.name.StartsWith("Boulder")&&boulders.Count(b=>b.name==t.name)==0)boulders.Add(t.gameObject);if(t.name.StartsWith("Stone")&&stones.Count(b=>b.name==t.name)==0)stones.Add(t.gameObject);}
  var rng=new System.Random(9090);float U(float a,float b)=>a+(float)rng.NextDouble()*(b-a);int count=0;
  void Place(GameObject tpl,Vector2 p,float scale,float bury){float h=H(p);if(float.IsNaN(h)||h<8)return;var g=Object.Instantiate(tpl,root);g.name=tpl.name+"_Path";g.SetActive(true);g.transform.localScale=Vector3.one*scale;g.transform.rotation=Quaternion.Euler(U(-8,8),U(0,360),U(-8,8));
   var rs=g.GetComponentsInChildren<Renderer>();float height=rs.Length>0?rs.Max(r=>r.bounds.size.y):scale;g.transform.position=new Vector3(p.x,h-height*bury,p.y);count++;}
  foreach(var r in routes){bool main=r.ruts>0;float s=U(4,10);
   while(s<r.length-4){int i=r.s.FindIndex(x=>x>=s);if(i<1){s+=5;continue;}var a=r.pts[i-1];var b=r.pts[i];var dir=(b-a).normalized;var perp=new Vector2(-dir.y,dir.x);float half=r.w[i]*.5f;
    var c=a+(b-a)*.5f;bool clear=CampFade(c)>.99f&&RopeFade(c)>.99f&&!(r.name=="Approach"&&c.y<-150&&c.y>-190);
    if(clear){float side=rng.NextDouble()<.5?-1:1;int cluster=rng.NextDouble()<.35?rng.Next(2,4):1;var basePt=c+perp*side*(half+U(.3f,1.6f));
     for(int q=0;q<cluster;q++){var p=basePt+new Vector2(U(-1.4f,1.4f),U(-1.4f,1.4f))*(q>0?1:0);if(Query(p,out var hh)&&hh.d<hh.w*.5f-.2f)continue;
      bool big=q==0&&rng.NextDouble()<(main?.42:.3);if(big)Place(boulders[rng.Next(boulders.Count)],p,U(.7f,1.6f),.28f);else Place(stones[rng.Next(stones.Count)],p,U(.35f,.9f),.3f);}
     // Occasional half-buried flat stone inside the trail.
     if(rng.NextDouble()<.22){var p=c+perp*U(-half*.6f,half*.6f)+dir*U(-2,2);Place(stones[rng.Next(stones.Count)],p,U(.22f,.38f),.55f);}}
    s+=main?U(5,11):U(5,10);}}
  return $"edge boulders and stones placed {count}";
 }
}
}
