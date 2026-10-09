using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Remains.Editor {
public static class CastleBlockoutAudit {
    static List<string> problems;static int samples;
    static void Check(Vector3 feet,string label){
        samples++;
        if(!Physics.SphereCast(feet+Vector3.up*.4f,.025f,Vector3.down,out var hit,1f,~0,QueryTriggerInteraction.Ignore)){problems.Add(label+": missing floor "+feet);return;}
        foreach(var c in Physics.OverlapCapsule(hit.point+Vector3.up*.68f,hit.point+Vector3.up*1.48f,.3f,~0,QueryTriggerInteraction.Ignore))
            if(!(c is CharacterController))problems.Add(label+": clearance blocked by "+c.name+" at "+feet);
    }
    public static string Run(){
        problems=new List<string>();samples=0;Physics.SyncTransforms();
        var outline=LargeCastleBlockout.Outline;var root=GameObject.Find("Map_LargeCastle_Blockout").transform;var castle=root.Find("Castle_150x130m");
        for(int i=0;i<8;i++){
            var a=outline[i];var b=outline[(i+1)%8];float length=Vector3.Distance(a,b);Vector3 d=(b-a).normalized;
            if(i!=1)for(float t=7.8f;t<=length-7.8f;t+=.4f){var p=a+d*t;p.y=LargeCastleBlockout.WallHeight(i,t);Check(p,"Wall "+i);}
            var tower=castle.Find("Round_Tower_"+i);float start=LargeCastleBlockout.EntranceAngle(i)+45;
            for(int k=0;k<80;k++)Check(tower.TransformPoint(LargeCastleBlockout.Radial(start+(k+.5f)*5,5.2f,LargeCastleBlockout.TowerBases[i]+.02f+14*(k+1f)/80)),"Spiral "+i+" tread "+k);
            for(float angle=0;angle<360;angle+=5)Check(tower.TransformPoint(LargeCastleBlockout.Radial(angle,7.4f,LargeCastleBlockout.TowerWalk(i)+.02f)),"Tower ring "+i);
            Check(tower.TransformPoint(Vector3.up*(LargeCastleBlockout.TowerBases[i]+.02f)),"Tower lower room "+i);
        }
        foreach(Transform building in castle){
            if(building.name.StartsWith("Wall_"))foreach(Transform step in building)if(step.name.StartsWith("Wall_Step_"))Check(step.position+Vector3.up*step.localScale.y/2,"Broad staircase");
            if(!building.name.StartsWith("Building_"))continue;
            int floors=building.name.Contains("Three")?3:2;
            for(int f=0;f<floors;f++){
                var floor=building.Find("Floor_"+(f+1));
                foreach(Transform marker in floor)if(marker.name.StartsWith("Room_")&&marker.name!="Room_Partition"&&marker.name!="Room_Doors")for(float x=-10;x<=6.5f;x+=.5f)Check(floor.TransformPoint(new Vector3(x,.04f,marker.localPosition.z)),"Room access "+building.name+" "+f);
                for(float z=-8.7f;z<=8.5f;z+=.5f)Check(floor.TransformPoint(new Vector3(6.5f,.04f,z)),"Corridor");
                if(f<floors-1)for(int j=0;j<30;j++)Check(floor.TransformPoint(new Vector3(9.5f,(j+1)*.2f,-7.2f+(j+.5f)*.34f)),"Indoor stair "+building.name);
                foreach(float z in new[]{-6.6f,0,6.6f}){samples++;if(Physics.Raycast(floor.TransformPoint(new Vector3(-14,2.5f,z)),floor.TransformDirection(Vector3.right),3))problems.Add("Window obstructed: "+building.name+" floor "+f);}
            }
        }
        for(float z=-160;z<=-42;z+=.5f)Check(new Vector3(0,z>=-130&&z<=-55?16.03f:LargeCastleBlockout.TerrainHeight(0,z),z),"Bridge/gate route");
        foreach(Transform t in root.Find("100_Capacity_Positions"))Check(t.position,"Capacity "+t.name);
        string report="Uneven hill castle audit\nFloor / body / head / window checks: "+samples+"\nIssues: "+problems.Count+"\n"+string.Join("\n",problems);
        Directory.CreateDirectory("output/castle-blockout");File.WriteAllText("output/castle-blockout/validation.txt",report);return report;
    }
    static void Place(CharacterController c,Vector3 p){c.enabled=false;c.transform.position=p+Vector3.up*.06f;c.enabled=true;Physics.SyncTransforms();}
    static bool Walk(CharacterController c,Vector3 target){
        for(int i=0;i<3200;i++){var delta=target-c.transform.position;delta.y=0;if(delta.magnitude<.07f)return Mathf.Abs(c.transform.position.y-target.y)<.45f;c.Move(delta.normalized*Mathf.Min(.06f,delta.magnitude)+Vector3.down*.06f);}
        return false;
    }
    public static string WalkBridge(){
        var player=Object.FindAnyObjectByType<Remains.CastleWalkthrough>();var pc=player.GetComponent<CharacterController>();pc.enabled=false;
        var probe=new GameObject("TemporaryMovementProbe");var c=probe.AddComponent<CharacterController>();c.height=1.8f;c.radius=.35f;c.center=Vector3.up*.9f;c.stepOffset=.3f;c.skinWidth=.035f;c.slopeLimit=50;
        var lines=new List<string>();var castle=GameObject.Find("Map_LargeCastle_Blockout").transform.Find("Castle_150x130m");
        Place(c,new Vector3(0,LargeCastleBlockout.TerrainHeight(0,-160),-160));bool ok=Walk(c,new Vector3(0,LargeCastleBlockout.TerrainHeight(0,-42),-42));lines.Add("Bridge to courtyard: "+ok+" at "+probe.transform.position);
        foreach(Transform t in castle){
            if(t.name=="Wall_3"||t.name=="Wall_6"){
                Place(c,t.Find("Stair_Foot").position);ok=Walk(c,t.Find("Stair_Arrival").position);lines.Add(t.name+" ascent: "+ok+" at "+probe.transform.position);
                ok=Walk(c,t.Find("Stair_Foot").position);lines.Add(t.name+" descent: "+ok);
            }
            if(t.name.StartsWith("Round_Tower_")){
                int id=int.Parse(t.name.Substring("Round_Tower_".Length));float y=LargeCastleBlockout.TowerBases[id],start=LargeCastleBlockout.EntranceAngle(id)+45;
                Place(c,t.Find("Room_Entrance").position);ok=Walk(c,t.TransformPoint(Vector3.up*(y+.02f)));lines.Add(t.name+" lower doorway: "+ok);
                ok=Walk(c,t.Find("Stair_Start").position);
                int failed=-1;for(int j=0;j<80&&ok;j++){ok=Walk(c,t.TransformPoint(LargeCastleBlockout.Radial(start+(j+.6f)*5,5.2f,y+.02f+14*(j+1f)/80)));if(!ok)failed=j;}
                if(ok)ok=Walk(c,t.Find("Stair_End").position);
                lines.Add(t.name+" spiral ascent: "+ok+" failedStep="+failed+" local="+t.InverseTransformPoint(probe.transform.position));
                if(ok){for(int j=79;j>=0&&ok;j--)ok=Walk(c,t.TransformPoint(LargeCastleBlockout.Radial(start+(j+.4f)*5,5.2f,y+.02f+14*(j+1f)/80)));lines.Add(t.name+" spiral descent: "+ok);}
            }
            if(!t.name.StartsWith("Building_"))continue;
            int flights=t.name.Contains("Three")?2:1;
            for(int f=0;f<flights;f++){
                Place(c,t.TransformPoint(new Vector3(9.5f,f*6+.04f,-8.6f)));ok=Walk(c,t.TransformPoint(new Vector3(9.5f,(f+1)*6,5)));lines.Add(t.name+" flight "+f+" ascent: "+ok+" local="+t.InverseTransformPoint(probe.transform.position));
                ok=Walk(c,t.TransformPoint(new Vector3(6.5f,(f+1)*6,5)));lines.Add(t.name+" landing: "+ok);
            }
        }
        for(int id=0;id<8;id++){
            var tower=castle.Find("Round_Tower_"+id);float y=LargeCastleBlockout.TowerWalk(id)+.02f;
            Place(c,tower.TransformPoint(LargeCastleBlockout.Radial(2,7.4f,y)));ok=true;
            for(int deg=7;deg<=362&&ok;deg+=5)ok=Walk(c,tower.TransformPoint(LargeCastleBlockout.Radial(deg,7.4f,y)));
            lines.Add("Tower "+id+" upper circulation ring: "+ok);
            var a=LargeCastleBlockout.Outline[id];var b=LargeCastleBlockout.Outline[(id+1)%8];Vector3 d=(b-a).normalized;float len=Vector3.Distance(a,b);
            var begin=a+d*7.35f;begin.y=y;Place(c,begin);ok=true;
            for(float dist=7.5f;dist<len-7.3f&&ok;dist+=.4f){var target=a+d*dist;target.y=id==1?30:LargeCastleBlockout.WallHeight(id,dist);ok=Walk(c,target);}
            lines.Add("Wall "+id+" tower-to-tower traversal: "+ok);
            if(id>=3){Vector3 outward=new Vector3(d.z,0,-d.x);var mid=(a+b)/2;var start=mid+outward*65;start.y=LargeCastleBlockout.TerrainHeight(start.x,start.z);Place(c,start);ok=true;
                for(float dist=64;dist>=4&&ok;dist-=1){var target=mid+outward*dist;target.y=LargeCastleBlockout.TerrainHeight(target.x,target.z);ok=Walk(c,target);}
                lines.Add("Dry land approach "+id+": "+ok);
            }
        }
        Object.DestroyImmediate(probe);pc.enabled=true;string report=string.Join("\n",lines);File.AppendAllText("output/castle-blockout/validation.txt","\n"+report);return report;
    }
}
}
