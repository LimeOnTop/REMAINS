using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace Remains.Editor {
public static partial class LargeCastleBlockout {
    const string Dir="Assets/Remains/Blockout";
    public const float FloorHeight=6;
    public static readonly float[] TowerBases={15,16,16,20,26,27,23,20};
    public static float TowerWalk(int i)=>TowerBases[i]+14;
    static readonly Vector3[] Homes={new Vector3(-34,19,-13),new Vector3(26,22,12),new Vector3(-6,26,40)};
    static readonly float[] HomeAngles={-5,8,-5};
    static Dictionary<string,Mesh> meshCache=new Dictionary<string,Mesh>();
    static Material stone,deck,trim,wood,water,earth,room;
    public static readonly Vector3[] Outline={new Vector3(-59,0,-39),new Vector3(-23,0,-58),new Vector3(23,0,-58),new Vector3(60,0,-38),new Vector3(76,0,20),new Vector3(43,0,68),new Vector3(-25,0,74),new Vector3(-70,0,25)};
    static Transform Group(Transform p,string n,Vector3 pos=default,Quaternion rot=default){var g=new GameObject(n);g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localRotation=rot==default?Quaternion.identity:rot;return g.transform;}
    static Material Mat(string n,Color c){string path=Dir+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=c;m.SetFloat("_Smoothness",.05f);return m;}
    static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;g.isStatic=true;return g;}
    static void Cylinder(Transform p,string n,Vector3 pos,float radius,float height,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=new Vector3(radius*2,height/2,radius*2);g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.AddComponent<MeshCollider>();g.isStatic=true;}
    public static GameObject Build(){
        Directory.CreateDirectory(Dir);AssetDatabase.Refresh();meshCache.Clear();
        stone=Mat("Wall_Stone",new Color(.49f,.53f,.55f)); deck=Mat("Walk_Stone",new Color(.66f,.69f,.66f));trim=Mat("Edge_Stone",new Color(.37f,.42f,.46f));wood=Mat("Door_Wood",new Color(.33f,.19f,.09f));water=Mat("River_Blue",new Color(.13f,.43f,.57f));earth=Mat("Hill_Green",new Color(.32f,.40f,.25f));room=Mat("Interior_Warm",new Color(.62f,.57f,.47f));
        var root=Group(null,"Map_LargeCastle_Blockout");
        Terrain(root);River(root);
        var castle=Group(root,"Castle_150x130m");
        for(int i=0;i<Outline.Length;i++){
            if(i!=1)Wall(castle,i,Outline[i],Outline[(i+1)%Outline.Length],i==3||i==6);
            Tower(castle,i);
        }
        Gate(castle);
        Building(castle,"Building_A_TwoFloors",Homes[0],-5,2);
        Building(castle,"Building_B_TwoFloors",Homes[1],8,2);
        Building(castle,"Building_C_ThreeFloors",Homes[2],-5,3);
        Bridge(root);
        SpawnMarkers(root);
        var player=Group(root,"Player_WASD_E_Gate",new Vector3(0,TerrainHeight(0,-140)+.08f,-140));
        var cc=player.gameObject.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.35f;cc.center=new Vector3(0,.9f,0);cc.stepOffset=.3f;cc.slopeLimit=50;cc.skinWidth=.035f;
        var camera=Group(player,"FirstPersonCamera",new Vector3(0,1.65f,0));var cam=camera.gameObject.AddComponent<Camera>();cam.nearClipPlane=.05f;cam.farClipPlane=600;cam.fieldOfView=75;cam.enabled=false;
        camera.gameObject.AddComponent<AudioListener>();
        var ctl=player.gameObject.AddComponent<Remains.CastleWalkthrough>();ctl.viewCamera=cam;ctl.spawn=player.position;
        var overview=Group(root,"OverviewCamera",new Vector3(145,145,-180));overview.LookAt(new Vector3(0,8,-15));var ov=overview.gameObject.AddComponent<Camera>();ov.farClipPlane=800;ov.fieldOfView=47;ov.clearFlags=CameraClearFlags.SolidColor;ov.backgroundColor=new Color(.56f,.67f,.76f);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.69f,.74f);RenderSettings.fog=false;
        Physics.SyncTransforms();AssetDatabase.SaveAssets();
        Selection.activeGameObject=castle.gameObject;
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,12,0),Quaternion.Euler(48,-30,0),180);
        return root.gameObject;
    }
    static float EdgeDistance(Vector3 p){float d=float.MaxValue;for(int i=0;i<Outline.Length;i++){Vector3 a=Outline[i],b=Outline[(i+1)%Outline.Length];Vector3 e=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,e)/e.sqrMagnitude);d=Mathf.Min(d,Vector3.Distance(p,a+e*t));}return d;}
    static bool Inside(Vector3 p){bool inside=false;for(int i=0,j=Outline.Length-1;i<Outline.Length;j=i++){var a=Outline[i];var b=Outline[j];if((a.z>p.z)!=(b.z>p.z)&&p.x<(b.x-a.x)*(p.z-a.z)/(b.z-a.z)+a.x)inside=!inside;}return inside;}
    static float RiverZ(float x){
        float front=x < -23?Mathf.Lerp(-50,-58,Mathf.InverseLerp(-70,-23,x)):x<=23?-58:Mathf.Lerp(-58,-43,Mathf.InverseLerp(23,67,x));
        return front-32;
    }
    static float Hill(float x,float z){return 16+.067f*(z+58)+2.5f*Mathf.Sin((x+18)/37)+1.8f*Mathf.Sin(z/28);}
    public static float TerrainHeight(float x,float z){
        var p=new Vector3(x,0,z);float dist=Inside(p)?0:EdgeDistance(p);
        float hill=Hill(x,z),height=Mathf.Lerp(3.5f,hill,1-Mathf.SmoothStep(0,1,Mathf.Clamp01((dist-3)/65)));
        for(int i=0;i<Outline.Length;i++){
            float d=Vector2.Distance(new Vector2(x,z),new Vector2(Outline[i].x,Outline[i].z));
            float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,19,d));height=Mathf.Lerp(height,TowerBases[i]-.03f,blend);
        }
        for(int i=0;i<Homes.Length;i++){
            Vector3 q=Quaternion.Euler(0,-HomeAngles[i],0)*(p-new Vector3(Homes[i].x,0,Homes[i].z));
            float outside=Mathf.Max(Mathf.Abs(q.x)-13.5f,Mathf.Abs(q.z)-12);
            height=Mathf.Lerp(height,Homes[i].y-.03f,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,10,outside)));
        }
        // A short level gate court blends into the uneven inner hill.
        float gateD=Mathf.Max(Mathf.Abs(x)-8,Mathf.Abs(z+56)-6);
        height=Mathf.Lerp(height,16-.03f,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,9,gateD)));
        float channel=Mathf.Abs(z-RiverZ(x));height=Mathf.Lerp(1,height,Mathf.SmoothStep(0,1,Mathf.InverseLerp(18,27,channel)));
        if(Mathf.Abs(x)<17&&z<=-119&&z>-178){
            float approach=z>=-130?Mathf.Lerp(16,height,Mathf.InverseLerp(-130,-119,z)):Mathf.Lerp(4,16,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-174,-130,z)));
            height=Mathf.Lerp(height,approach,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,17,Mathf.Abs(x))));
        }
        return height;
    }
    static void Terrain(Transform root){
        var data=new TerrainData();data.heightmapResolution=513;data.size=new Vector3(360,64,360);var heights=new float[513,513];
        for(int z=0;z<513;z++)for(int x=0;x<513;x++)heights[z,x]=TerrainHeight(x*360f/512-180,z*360f/512-180)/64;
        data.SetHeights(0,0,heights);AssetDatabase.CreateAsset(data,AssetDatabase.GenerateUniqueAssetPath(Dir+"/CastleHill.asset"));
        var go=UnityEngine.Terrain.CreateTerrainGameObject(data);go.name="Terrain_360m_Hill_Riverbed";go.transform.SetParent(root);go.transform.position=new Vector3(-180,0,-180);
        var terrain=go.GetComponent<UnityEngine.Terrain>();var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");terrain.materialTemplate=new Material(shader);
        var tex=new Texture2D(2,2);tex.SetPixels(new[]{earth.color,earth.color,earth.color,earth.color});tex.Apply();AssetDatabase.CreateAsset(tex,AssetDatabase.GenerateUniqueAssetPath(Dir+"/GroundColor.asset"));
        var layer=new TerrainLayer{diffuseTexture=tex,tileSize=new Vector2(12,12),smoothness=0};AssetDatabase.CreateAsset(layer,AssetDatabase.GenerateUniqueAssetPath(Dir+"/GroundLayer.terrainlayer"));var sandTex=new Texture2D(2,2);var sand=new Color(.58f,.53f,.39f);sandTex.SetPixels(new[]{sand,sand,sand,sand});sandTex.Apply();AssetDatabase.CreateAsset(sandTex,AssetDatabase.GenerateUniqueAssetPath(Dir+"/SandColor.asset"));
        var sandLayer=new TerrainLayer{diffuseTexture=sandTex,tileSize=new Vector2(10,10),smoothness=0};AssetDatabase.CreateAsset(sandLayer,AssetDatabase.GenerateUniqueAssetPath(Dir+"/SandLayer.terrainlayer"));data.terrainLayers=new[]{layer,sandLayer};
        data.alphamapResolution=128;var alpha=new float[128,128,2];for(int z=0;z<128;z++)for(int x=0;x<128;x++){float wx=x*360f/127-180,wz=z*360f/127-180;float sandWeight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,11,TerrainHeight(wx,wz)));alpha[z,x,0]=1-sandWeight;alpha[z,x,1]=sandWeight;}data.SetAlphamaps(0,0,alpha);
    }
    static void River(Transform root){
        var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<=90;i++){float x=-180+i*4;v.Add(new Vector3(x,4,RiverZ(x)-24));v.Add(new Vector3(x,4,RiverZ(x)+24));if(i>0){int a=(i-1)*2;t.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});}}
        var mesh=new Mesh{name="RiverRibbon"};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Dir+"/River.asset"));
        var g=Group(root,"River");g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=water;
    }
    // Convex 2D polygon extruded through Z, with explicit flat-shaded faces.
    static Mesh PrismMesh(string key,List<Vector2> polygon,float depth){
        if(meshCache.TryGetValue(key,out var existing))return existing;
        var v=new List<Vector3>();var tri=new List<int>();int n=polygon.Count;
        foreach(float z in new[]{-depth/2,depth/2})foreach(var q in polygon)v.Add(new Vector3(q.x,q.y,z));
        for(int k=1;k<n-1;k++){tri.AddRange(new[]{0,k+1,k,n,n+k,n+k+1});}
        for(int k=0;k<n;k++){int j=(k+1)%n;tri.AddRange(new[]{k,j,j+n,k,j+n,k+n});}
        var vv=new Vector3[tri.Count];var tt=new int[tri.Count];for(int i=0;i<tri.Count;i++){vv[i]=v[tri[i]];tt[i]=i;}
        var m=new Mesh{name=key};m.vertices=vv;m.triangles=tt;m.RecalculateNormals();m.RecalculateBounds();AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(Dir+"/Mesh_"+meshCache.Count+".asset"));meshCache[key]=m;return m;
    }
    static GameObject MeshObject(Transform p,string name,Mesh mesh,Material material,Vector3 pos,Quaternion rotation,bool convex=false){
        var g=Group(p,name,pos,rotation).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;var c=g.AddComponent<MeshCollider>();c.sharedMesh=mesh;c.convex=convex;g.isStatic=!convex;return g;
    }
    static void Sector(Transform p,string name,float ri,float ro,float angle,float span,float bottom,float top,Material mat){
        // A small annular sector uses a concave polygon: build independent radial cells.
        int count=Mathf.Max(1,Mathf.CeilToInt(span/5.625f));float da=span/count;
        for(int k=0;k<count;k++){
            float a=da*Mathf.Deg2Rad;
            var poly=new List<Vector2>{new Vector2(ri,0),new Vector2(ro,0),new Vector2(ro*Mathf.Cos(a),ro*Mathf.Sin(a)),new Vector2(ri*Mathf.Cos(a),ri*Mathf.Sin(a))};
            string key=$"Sector_{ri:F3}_{ro:F3}_{da:F3}_{top-bottom:F3}";
            var mesh=PrismMesh(key,poly,top-bottom);
            // Local polygon X points radially east at angle 90 degrees.
            var rot=Quaternion.Euler(0,angle+k*da-90,0)*Quaternion.Euler(-90,0,0);
            MeshObject(p,name,mesh,mat,new Vector3(0,(bottom+top)/2,0),rot);
        }
    }
    public static float WallHeight(int i,float distance){float len=Vector3.Distance(Outline[i],Outline[(i+1)%Outline.Length]);return Mathf.Lerp(TowerWalk(i),TowerWalk((i+1)%Outline.Length),Mathf.InverseLerp(7.6f,len-7.6f,distance));}
    static void Wall(Transform root,int index,Vector3 a,Vector3 b,bool stairs){
        Vector3 d=(b-a).normalized,inward=new Vector3(-d.z,0,d.x);float length=Vector3.Distance(a,b),usable=length-15.2f;
        var w=Group(root,"Wall_"+index,(a+b)/2,Quaternion.LookRotation(inward));
        int count=Mathf.CeilToInt(usable/.8f);float step=usable/count;
        for(int k=0;k<count;k++){
            float x=-usable/2+(k+.5f)*step,y=WallHeight(index,x+length/2);
            Box(w,"Foundation_"+k,new Vector3(x,(y-.4f)/2,0),new Vector3(step,y-.4f,6),stone);
            Box(w,"Walk_Step_"+k,new Vector3(x,y-.2f,0),new Vector3(step,.4f,6),deck);
            Box(w,"Outer_Parapet",new Vector3(x,y+.7f,-2.75f),new Vector3(step,1.4f,.5f),trim);
            if(!stairs||Mathf.Abs(x)>3)Box(w,"Inner_Parapet",new Vector3(x,y+.55f,2.75f),new Vector3(step,1.1f,.5f),trim);
            if(k%4<2)Box(w,"Merlon",new Vector3(x,y+1.75f,-2.75f),new Vector3(step,.7f,.65f),stone);
        }
        if(stairs)WallStairs(w,index);
    }
    static void WallStairs(Transform w,int index){
        float top=(TowerWalk(index)+TowerWalk((index+1)%Outline.Length))/2;
        var foot=w.TransformPoint(new Vector3(0,0,33));float bottom=TerrainHeight(foot.x,foot.z)+.03f;
        int count=Mathf.CeilToInt((top-bottom)/.18f);float run=30f/count;
        for(int i=0;i<count;i++){
            float y=Mathf.Lerp(bottom,top,(i+1f)/count),z=33-(i+.5f)*run;
            Box(w,"Wall_Step_"+i,new Vector3(0,(bottom+y)/2,z),new Vector3(5,y-bottom,run),deck);
            foreach(int side in new[]{-1,1})Box(w,"Stair_Guard",new Vector3(side*2.65f,y+.55f,z),new Vector3(.3f,1.1f,run),trim);
        }
        Group(w,"Stair_Foot",new Vector3(0,bottom,33.8f));Group(w,"Stair_Arrival",new Vector3(0,top,0));
    }
    public static float EntranceAngle(int i){var c=Outline[i];return Mathf.Atan2(-c.x,-c.z)*Mathf.Rad2Deg;}
    public static Vector3 Radial(float angle,float r,float y){float a=angle*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a)*r,y,Mathf.Cos(a)*r);}
    static bool ArcContains(float a,float start,float span){return Mathf.Repeat(a-start,360)<span;}
    static void Tower(Transform root,int i){
        var center=Outline[i];float floor=TowerBases[i],walk=TowerWalk(i),entry=EntranceAngle(i),stairStart=entry+45;
        const int steps=80;const float da=5;float end=stairStart+steps*da,holeStart=end-105;
        var tower=Group(root,"Round_Tower_"+i,center);
        Cylinder(tower,"Tower_Foundation",new Vector3(0,(floor-.4f)/2,0),8,floor-.4f,stone);
        Cylinder(tower,"Lower_Room_Floor",new Vector3(0,floor-.18f,0),7.15f,.4f,deck);
        for(int j=0;j<64;j++){
            float angle=entry-180+j*5.625f;bool doorway=Mathf.Abs(Mathf.DeltaAngle(angle+2.8125f,entry))<15;
            Sector(tower,doorway?"Entrance_Lintel":"Hollow_Tower_Wall",7,8,angle,5.625f,doorway?floor+4:floor-.4f,walk-.4f,stone);
        }
        // Upper floor: solid centre and outer circulation ring, with a real stair opening.
        Cylinder(tower,"Upper_Centre_Floor",new Vector3(0,walk-.18f,0),3.45f,.4f,deck);
        for(int j=0;j<64;j++){
            float angle=j*5.625f;bool hole=ArcContains(angle+2.8125f,holeStart,118);
            Sector(tower,"Upper_Ring_Floor",hole?6.9f:3.45f,8.2f,angle,5.625f,walk-.38f,walk+.02f,deck);
            if(hole){
                Sector(tower,"Stairwell_Inner_Guard",3.35f,3.5f,angle,5.625f,walk+.02f,walk+1.12f,trim);
                Sector(tower,"Stairwell_Outer_Guard",6.85f,6.98f,angle,5.625f,walk+.02f,walk+1.12f,trim);
            }
        }
        for(int j=0;j<steps;j++){
            float y=floor+.02f+14*(j+1f)/steps;
            Sector(tower,"Spiral_Tread_"+j,3.8f,6.65f,stairStart+j*da,da,y-.18f,y,deck);
            if(j%3==0){var rail=Box(tower,"Spiral_Inner_Rail",Radial(stairStart+(j+.5f)*da,3.7f,y+.55f),new Vector3(.15f,1.1f,.15f),wood);}
        }
        Sector(tower,"Spiral_Top_Landing",3.8f,6.85f,end,18,walk-.16f,walk+.04f,deck);
        Vector3 prev=(Outline[(i+7)%8]-center).normalized,next=(Outline[(i+1)%8]-center).normalized;
        for(int j=0;j<32;j++){
            float a=j*11.25f;Vector3 n=Radial(a,1,0);if(Vector3.Dot(n,prev)>.87f||Vector3.Dot(n,next)>.87f)continue;
            Sector(tower,"Tower_Parapet",7.85f,8.2f,a,11.25f,walk+.02f,walk+1.4f,trim);
            if(j%2==0)Sector(tower,"Tower_Merlon",7.9f,8.25f,a,11.25f,walk+1.4f,walk+2.1f,stone);
        }
        Group(tower,"Room_Entrance",Radial(entry,9,floor+.03f));Group(tower,"Stair_Start",Radial(stairStart-5,5.2f,floor+.02f));Group(tower,"Stair_End",Radial(end+19,5.2f,walk+.02f));
    }
    static void Gate(Transform root){
        float ground=16,top=30,radius=5.5f,spring=6.5f;
        var g=Group(root,"Gatehouse_DoubleDoors",new Vector3(0,ground,-58));
        GateMechanismRooms(g);
        Box(g,"Over_Gate_Walk",new Vector3(0,13.6f,0),new Vector3(30,.8f,6),deck);
        foreach(int side in new[]{-1,1})Box(g,"Gate_Parapet",new Vector3(0,14.7f,side*2.75f),new Vector3(30,1.4f,.5f),trim);
        Box(g,"Gate_Threshold",new Vector3(0,-.27f,0),new Vector3(11,.6f,6),deck);
        // Voussoirs form the semicircle; masonry above follows its curved silhouette.
        for(int j=0;j<32;j++){
            float a=j*Mathf.PI/32,b=(j+1)*Mathf.PI/32;
            var poly=new List<Vector2>{new Vector2(radius*Mathf.Cos(a),spring+radius*Mathf.Sin(a)),new Vector2(6.15f*Mathf.Cos(a),spring+6.15f*Mathf.Sin(a)),new Vector2(6.15f*Mathf.Cos(b),spring+6.15f*Mathf.Sin(b)),new Vector2(radius*Mathf.Cos(b),spring+radius*Mathf.Sin(b))};
            MeshObject(g,"Arch_Stone_"+j,PrismMesh("Arch_"+j,poly,6.1f),trim,Vector3.zero,Quaternion.identity);
            float x0=6.15f*Mathf.Cos(b),x1=6.15f*Mathf.Cos(a),y0=spring+6.15f*Mathf.Sin(b),y1=spring+6.15f*Mathf.Sin(a);
            var fill=new List<Vector2>{new Vector2(x0,y0),new Vector2(x1,y1),new Vector2(x1,13.2f),new Vector2(x0,13.2f)};
            MeshObject(g,"Arch_Infill_"+j,PrismMesh("ArchFill_"+j,fill,6),stone,Vector3.zero,Quaternion.identity);
        }
        var hingeL=Group(g,"Left_Hinge",new Vector3(-radius,0,-2.55f));var hingeR=Group(g,"Right_Hinge",new Vector3(radius,0,-2.55f));
        for(int side=0;side<2;side++){
            float lo=side==0?-radius+.04f:.04f,hi=side==0?-.04f:radius-.04f,hinge=side==0?-radius:radius;
            var poly=new List<Vector2>{new Vector2(lo-hinge,.03f),new Vector2(hi-hinge,.03f)};
            for(int k=0;k<=24;k++){float x=Mathf.Lerp(hi,lo,k/24f);poly.Add(new Vector2(x-hinge,spring+Mathf.Sqrt(radius*radius-x*x)-.08f));}
            MeshObject(side==0?hingeL:hingeR,"Arched_Door",PrismMesh("ArchedDoor_"+side,poly,.4f),wood,Vector3.zero,Quaternion.identity,true);
        }
        hingeL.localRotation=Quaternion.Euler(0,-100,0);hingeR.localRotation=Quaternion.Euler(0,100,0);
        var gate=g.gameObject.AddComponent<Remains.BlockoutGate>();gate.leftHinge=hingeL;gate.rightHinge=hingeR;
    }
    struct Hole {public float x,y,w,h;public Hole(float X,float Y,float W,float H){x=X;y=Y;w=W;h=H;}}
    static void Panel(Transform root,string name,Vector3 center,Quaternion rot,float width,float height,float thickness,List<Hole> holes){
        var p=Group(root,name,center,rot);var xs=new List<float>{-width/2,width/2};var ys=new List<float>{0,height};
        foreach(var h in holes){xs.Add(h.x-h.w/2);xs.Add(h.x+h.w/2);ys.Add(h.y);ys.Add(h.y+h.h);}xs.Sort();ys.Sort();
        for(int i=0;i<xs.Count-1;i++)for(int j=0;j<ys.Count-1;j++){float x=(xs[i]+xs[i+1])/2,y=(ys[j]+ys[j+1])/2;if(xs[i+1]-xs[i]<.001||ys[j+1]-ys[j]<.001)continue;bool cut=false;foreach(var h in holes)if(x>h.x-h.w/2&&x<h.x+h.w/2&&y>h.y&&y<h.y+h.h)cut=true;if(!cut)Box(p,"Wall_Block",new Vector3(x,y,0),new Vector3(xs[i+1]-xs[i],ys[j+1]-ys[j],thickness),room);}
    }
    static void Building(Transform root,string name,Vector3 pos,float angle,int floors){
        var b=Group(root,name,pos,Quaternion.Euler(0,angle,0));
        for(int f=0;f<floors;f++){
            float y=f*FloorHeight;var level=Group(b,"Floor_"+(f+1),new Vector3(0,y,0));
            if(f==0)Box(level,"Ground_Slab",new Vector3(0,-.16f,0),new Vector3(24,.4f,20),deck);
            else { // stair opening x [8,11], z [-8,3]; all slabs terminate at its boundaries
                Box(level,"Main_Floor",new Vector3(-2,-.2f,0),new Vector3(20,.4f,20),deck);
                Box(level,"Outer_Strip",new Vector3(11.5f,-.2f,0),new Vector3(1,.4f,20),deck);
                Box(level,"South_Landing",new Vector3(9.5f,-.2f,-8.6f),new Vector3(3,.4f,2.8f),deck);
                Box(level,"North_Landing",new Vector3(9.5f,-.2f,6.5f),new Vector3(3,.4f,7),deck);
                Box(level,"Stairwell_Guard",new Vector3(7.85f,.55f,-2.1f),new Vector3(.25f,1.1f,10.2f),trim);
                Box(level,"Stairwell_Outer_Guard",new Vector3(11.15f,.55f,-2.1f),new Vector3(.25f,1.1f,10.2f),trim);
                if(f==floors-1)Box(level,"Stairwell_End_Guard",new Vector3(9.5f,.55f,-7.35f),new Vector3(3,1.1f,.25f),trim);
            }
            var front=new List<Hole>{new Hole(-8,1.5f,2.4f,2.4f),new Hole(-2,1.5f,2.4f,2.4f)};
            front.Add(f==0?new Hole(6.5f,0,2.4f,3.5f):new Hole(6.5f,1.5f,2.4f,2.4f));
            Panel(level,"South_Window_Wall",new Vector3(0,0,-10),Quaternion.identity,24,FloorHeight,.4f,front);
            Panel(level,"North_Window_Wall",new Vector3(0,0,10),Quaternion.identity,24,FloorHeight,.4f,new List<Hole>{new Hole(-8,1.5f,2.4f,2.4f),new Hole(-2,1.5f,2.4f,2.4f),new Hole(6.5f,1.5f,2.4f,2.4f)});
            Panel(level,"West_Room_Windows",new Vector3(-12,0,0),Quaternion.Euler(0,90,0),20,FloorHeight,.4f,new List<Hole>{new Hole(-6.6f,1.5f,2.4f,2.4f),new Hole(0,1.5f,2.4f,2.4f),new Hole(6.6f,1.5f,2.4f,2.4f)});
            Panel(level,"East_Stair_Windows",new Vector3(12,0,0),Quaternion.Euler(0,90,0),20,FloorHeight,.4f,new List<Hole>{new Hole(-6.6f,1.5f,2.4f,2.4f),new Hole(6.6f,1.5f,2.4f,2.4f)});
            // Distinct simple medieval functions, with a continuous service corridor.
            float[] cuts=name.Contains("_B_")?(f==0?new[]{3.33f}:new[]{2.5f}):name.Contains("_C_")&&f==1?new[]{2.5f}:new[]{-3.33f,3.33f};
            foreach(float z in cuts)Box(level,"Room_Partition",new Vector3(-3.5f,FloorHeight/2,z),new Vector3(17,FloorHeight,.3f),room);
            var boundaries=new List<float>{-10};boundaries.AddRange(cuts);boundaries.Add(10);var doors=new List<Hole>();
            string[] roles=name.Contains("_A_")?(f==0?new[]{"Guard_Hall","Kitchen","Stores"}:new[]{"Dormitory","Officers","Equipment"}):name.Contains("_B_")?(f==0?new[]{"Great_Workshop","Stockroom"}:new[]{"Guild_Hall","Steward_Office"}):f==0?new[]{"Entrance_Hall","Kitchen","Supplies"}:f==1?new[]{"Council_Hall","Armoury"}:new[]{"Lord_Chamber","Study","Guard_Room"};
            for(int q=0;q<boundaries.Count-1;q++){float z=(boundaries[q]+boundaries[q+1])/2;doors.Add(new Hole(-z,0,1.8f,3.2f));Group(level,"Room_"+roles[q],new Vector3(-3.5f,0,z));}
            Panel(level,"Room_Doors",new Vector3(5,0,0),Quaternion.Euler(0,90,0),20,FloorHeight,.3f,doors);
            if(f<floors-1)for(int step=0;step<30;step++){float h=(step+1)*.2f;Box(level,"Interior_Step_"+step,new Vector3(9.5f,h-.1f,-7.2f+(step+.5f)*.34f),new Vector3(3,.2f,.34f),deck);}
            foreach(float z in new[]{-10.25f,10.25f})Box(level,"Timber_Belt",new Vector3(0,FloorHeight-.25f,z),new Vector3(24.4f,.3f,.25f),wood);
            foreach(float x in new[]{-12.25f,12.25f})Box(level,"Timber_Belt",new Vector3(x,FloorHeight-.25f,0),new Vector3(.25f,.3f,20.4f),wood);
            foreach(float x in new[]{-11.8f,4.7f,11.8f})foreach(float z in new[]{-10.25f,10.25f})Box(level,"Timber_Post",new Vector3(x,FloorHeight/2,z),new Vector3(.25f,FloorHeight,.25f),wood);
        }
        float roofBase=floors*FloorHeight;
        Box(b,"Attic_Ceiling",new Vector3(0,roofBase+.1f,0),new Vector3(24,.2f,20),wood);
        float slope=Mathf.Atan2(7,12.6f)*Mathf.Rad2Deg;
        foreach(int side in new[]{-1,1}){var roof=Box(b,"Pitched_Roof",new Vector3(side*6.3f,roofBase+3.7f,0),new Vector3(Mathf.Sqrt(12.6f*12.6f+49),.35f,21.2f),trim);roof.transform.localRotation=Quaternion.Euler(0,0,-side*slope);}
        var triangle=new List<Vector2>{new Vector2(-12,0),new Vector2(12,0),new Vector2(0,6.67f)};
        foreach(int side in new[]{-1,1})MeshObject(b,"Gable",PrismMesh("Gable",triangle,.4f),room,new Vector3(0,roofBase+.2f,side*10),Quaternion.identity);
        Box(b,"Stone_Plinth",new Vector3(0,-.55f,0),new Vector3(24.3f,.7f,20.3f),stone);
    }
    static void Bridge(Transform root){ BuildDrawbridge(root); }
    static void SpawnMarkers(Transform root){var g=Group(root,"100_Capacity_Positions");int count=0;for(float z=-39;z<=25&&count<100;z+=4)for(float x=-55;x<=55&&count<100;x+=4){if((x>-50&&x<-18&&z>-27&&z<2)||(x>17&&x<51&&z>-14&&z<18))continue;Group(g,"Position_"+(++count),new Vector3(x,TerrainHeight(x,z)+.05f,z));}}
}
}
