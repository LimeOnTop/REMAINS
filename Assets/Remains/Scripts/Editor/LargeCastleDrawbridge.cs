using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace Remains.Editor {
public static partial class LargeCastleBlockout {
    static void GateMechanismRooms(Transform g){
        foreach(int side in new[]{-1,1}){
            var w=Group(g,side<0?"West_Winch_Chamber":"East_Winch_Chamber");float x=side*10.25f;
            Box(w,"Chamber_Foundation",new Vector3(x,-8.1f,0),new Vector3(9.5f,15.8f,6),stone);
            Box(w,"Chamber_Floor",new Vector3(x,-.2f,0),new Vector3(9.5f,.4f,6),deck);
            // Front masonry is cut around a real vertical chain/pulley opening.
            Panel(w,"Chain_Aperture_Front",new Vector3(x,0,-2.9f),Quaternion.identity,9.5f,13.2f,.8f,new List<Hole>{new Hole(side*7.3f-x,9f,1.1f,4.3f)});
            Panel(w,"Courtyard_Door",new Vector3(x,0,2.8f),Quaternion.identity,9.5f,13.2f,.4f,new List<Hole>{new Hole(0,0,2.4f,3.6f)});
            Box(w,"Outer_Chamber_Wall",new Vector3(side*14.8f,6.6f,0),new Vector3(.4f,13.2f,6),stone);
            // Solid narrow jamb carries the arch, behind it the chamber remains hollow.
            Box(w,"Arch_Jamb",new Vector3(side*5.75f,6.6f,0),new Vector3(.5f,13.2f,6),stone);
        }
    }
    static Transform Rod(Transform p,string name,Vector3 a,Vector3 b,float radius,Material mat,bool collider=false){
        var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=(a+b)*.5f;g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);g.transform.localScale=new Vector3(radius*2,Vector3.Distance(a,b)/2,radius*2);g.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;
    }
    static Mesh RingMesh(string name,float rx,float ry,float tube){
        var v=new List<Vector3>();var tris=new List<int>();const int around=16,section=6;
        for(int i=0;i<around;i++){float a=i*2*Mathf.PI/around;for(int j=0;j<section;j++){float b=j*2*Mathf.PI/section;v.Add(new Vector3((rx+tube*Mathf.Cos(b))*Mathf.Cos(a),(ry+tube*Mathf.Cos(b))*Mathf.Sin(a),tube*Mathf.Sin(b)));}}
        for(int i=0;i<around;i++)for(int j=0;j<section;j++){int a=i*section+j,b=((i+1)%around)*section+j,c=((i+1)%around)*section+(j+1)%section,d=i*section+(j+1)%section;tris.AddRange(new[]{a,b,c,a,c,d});}
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Dir+"/"+name+".asset"));return mesh;
    }
    static Transform VisualMesh(Transform p,string name,Mesh mesh,Material mat,Vector3 pos,Quaternion rotation){var t=Group(p,name,pos,rotation);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;return t;}
    static void BuildDrawbridge(Transform root){
        var iron=Mat("Drawbridge_Iron",new Color(.18f,.20f,.22f));var linkMesh=RingMesh("ChainLink",.13f,.215f,.047f);var wheelMesh=RingMesh("PulleyRim",.85f,.85f,.105f);var axleRing=RingMesh("WoundChain",.8f,.8f,.07f);
        var fixedBridge=Group(root,"Bridge_Rectangle_12x72m",new Vector3(0,0,-105.1f));
        Box(fixedBridge,"Fixed_Bridge_Deck",new Vector3(0,15.5f,0),new Vector3(12,1,49.8f),wood);
        foreach(int side in new[]{-1,1})Box(fixedBridge,"Fixed_Bridge_Railing",new Vector3(side*5.8f,16.65f,0),new Vector3(.4f,1.3f,49.8f),trim);
        foreach(float z in new[]{-18f,0,18})foreach(int side in new[]{-1,1})Box(fixedBridge,"Fixed_Bridge_Pier",new Vector3(side*4,7.5f,z),new Vector3(1.5f,15,2),stone);
        var system=Group(root,"Drawbridge_System");var pivot=Group(system,"Drawbridge_Leaf",new Vector3(0,16,-62.2f));
        Box(pivot,"Moving_Deck",new Vector3(0,-.45f,-9),new Vector3(16,.9f,18),wood).isStatic=false;
        foreach(int side in new[]{-1,1}){
            Box(pivot,"Moving_Deck_Rail",new Vector3(side*7.8f,.65f,-10.5f),new Vector3(.35f,1.3f,15),iron).isStatic=false;
            Box(pivot,"Longitudinal_Beam",new Vector3(side*5,-1,-9),new Vector3(.6f,.4f,18),wood).isStatic=false;
            Rod(pivot,"Hinge_Axle",new Vector3(side*6.5f,-.15f,0),new Vector3(side*8.3f,-.15f,0),.22f,iron);
        }
        var rb=pivot.gameObject.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
        var gate=root.Find("Castle_150x130m/Gatehouse_DoubleDoors");
        Box(gate,"Bridge_Hinge_Landing",new Vector3(0,-.25f,-3.6f),new Vector3(16,.5f,1.2f),deck);
        var ctl=system.gameObject.AddComponent<Remains.BlockoutDrawbridge>();ctl.leaf=pivot;ctl.attachments=new Transform[2];ctl.pulleys=new Transform[2];ctl.chainRoots=new Transform[2];ctl.drums=new Transform[2];ctl.controls=new Transform[2];
        for(int s=0;s<2;s++){
            int side=s==0?-1:1;string suffix=side<0?"West":"East";float x=side*7.3f;
            ctl.attachments[s]=Group(pivot,"Chain_Attachment_"+suffix,new Vector3(x,.3f,-17.7f));VisualMesh(ctl.attachments[s],"Anchor_Eye",linkMesh,iron,Vector3.zero,Quaternion.Euler(0,90,0));
            var pulley=Group(system,"Pulley_Assembly_"+suffix,new Vector3(x,26.6f,-60.5f));ctl.pulleys[s]=pulley;
            foreach(float dx in new[]{-.22f,.22f})VisualMesh(pulley,"Pulley_Flange",wheelMesh,iron,new Vector3(dx,0,0),Quaternion.Euler(0,90,0));
            Rod(pulley,"Pulley_Axle",new Vector3(-.6f,0,0),new Vector3(.6f,0,0),.15f,iron);
            foreach(float dx in new[]{-.55f,.55f})Rod(pulley,"Hanging_Bracket",new Vector3(dx,0,0),new Vector3(dx,2.6f,0),.1f,iron);
            for(int j=0;j<6;j++){float a=j*Mathf.PI/3;Rod(pulley,"Pulley_Spoke",Vector3.zero,new Vector3(0,.77f*Mathf.Sin(a),.77f*Mathf.Cos(a)),.055f,iron);}
            var winch=Group(system,"Winch_Assembly_"+suffix,new Vector3(x,18.8f,-58.85f));
            var drum=Group(winch,"Winding_Drum");ctl.drums[s]=drum;
            Rod(drum,"Wooden_Drum",new Vector3(-.85f,0,0),new Vector3(.85f,0,0),.7f,wood);
            for(int j=0;j<10;j++)VisualMesh(drum,"Wound_Chain_Turn",axleRing,iron,new Vector3(-.7f+j*.155f,0,0),Quaternion.Euler(0,90,0));
            foreach(int end in new[]{-1,1}){
                Box(winch,"Winch_Stand",new Vector3(end*1.1f,-1.4f,0),new Vector3(.4f,2.8f,1.3f),wood);
                Rod(winch,"Drum_Axle",new Vector3(-1.45f,0,0),new Vector3(1.45f,0,0),.15f,iron);
            }
            Rod(drum,"Crank_Arm",new Vector3(side*1.55f,0,0),new Vector3(side*1.55f,-1,0),.13f,iron);
            Rod(drum,"Crank_Handle",new Vector3(side*1.55f,-1,0),new Vector3(side*2.15f,-1,0),.12f,wood);
            Box(winch,"Ratchet_Brake",new Vector3(side*1.05f,.4f,.5f),new Vector3(.3f,.2f,.8f),iron);
            ctl.controls[s]=Group(winch,"Control_F",new Vector3(side*2.3f,-1.5f,1.6f));
            var chain=Group(system,"Lifting_Chain_"+suffix);ctl.chainRoots[s]=chain;
            for(int k=0;k<190;k++)VisualMesh(chain,"Link_"+k,linkMesh,iron,Vector3.zero,Quaternion.identity);
        }
        ctl.ApplyPose(0);
    }
}
}
