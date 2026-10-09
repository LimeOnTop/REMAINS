using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Remains {
// Two-person capstan in a gatehouse winch room. E takes a free bar end, W winds the bridge up, S pays it out, E lets go.
// Every occupied bar in either room adds speed, so two people on one capstan raise the bridge twice as fast.
public class DrawbridgeCapstan : MonoBehaviour {
    public static readonly List<DrawbridgeCapstan> All=new List<DrawbridgeCapstan>();
    public static DrawbridgeCapstan LocalHeld;
    public static bool LocalHolding=>LocalHeld!=null;
    public BlockoutDrawbridge bridge;
    public Transform head;                 // bars + head, turned by the chain as it winds
    public float barRadius=2.75f;          // where an operator stands, just outside the bar ends
    public float degreesPerSecondPerOperator=4.5f;
    public int Slots=>2;
    public int occupied;                   // remote operators would add to this; the local player is counted separately
    int localSlot=-1;
    float input;
    CastleWalkthrough player;

    void OnEnable(){All.Add(this);}
    void OnDisable(){All.Remove(this);if(LocalHeld==this)Release();}
    void Start(){player=FindAnyObjectByType<CastleWalkthrough>();}

    Vector3 SlotPosition(int slot){
        float a=(head?head.eulerAngles.y:0)*Mathf.Deg2Rad+slot*Mathf.PI;
        return transform.position+new Vector3(Mathf.Sin(a)*barRadius,0,Mathf.Cos(a)*barRadius);
    }
    int NearestFreeSlot(Vector3 p,out float distance){
        int best=-1;distance=999;
        for(int s=0;s<Slots;s++){if(s==localSlot)continue;var q=SlotPosition(s);q.y=p.y;float d=Vector3.Distance(p,q);if(d<distance){distance=d;best=s;}}
        return best;
    }
    bool Blocked=>SiegeDirector.Instance&&(SiegeDirector.Instance.menu||SiegeDirector.Instance.held);

    void Update(){
        var k=Keyboard.current;if(k==null||!player)return;
        if(LocalHeld==this){
            if(k.eKey.wasPressedThisFrame||k.escapeKey.wasPressedThisFrame){Release();return;}
            input=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);if(input==0)input=debugInput;
            // Keep the operator at the bar end; the bar carries him round as the capstan turns.
            var c=player.GetComponent<CharacterController>();var p=SlotPosition(localSlot);p.y=transform.position.y+.08f;
            c.enabled=false;player.transform.position=p;c.enabled=true;
        }else if(!LocalHolding&&!Blocked&&k.eKey.wasPressedThisFrame){
            int s=NearestFreeSlot(player.transform.position,out float d);
            if(s>=0&&d<1.9f&&Mathf.Abs(player.transform.position.y-transform.position.y)<2.2f)Grab(s);
        }
    }
    [Tooltip("Debug: winding input while the local player holds a bar (-1..1).")]public float debugInput;
    public void Grab(int slot){if(LocalHeld)LocalHeld.Release();localSlot=slot;LocalHeld=this;}
    public void Release(){localSlot=-1;input=0;if(LocalHeld==this)LocalHeld=null;}

    public int Operators=>occupied+(LocalHeld==this?1:0);
    public float Input=>LocalHeld==this?input:0;

    void OnGUI(){
        if(!player)return;
        string text=null;
        if(LocalHeld==this){int total=0;foreach(var c in All)total+=c.Operators;
            text=$"ВОРОТ МОСТА  ·  W — поднимать  ·  S — опускать  ·  E — отпустить\nНа воротах: {total} из {All.Count*Slots} · мост поднят на {Mathf.RoundToInt(bridge.angle/BlockoutDrawbridge.MaxAngle*100)}%";}
        else if(!LocalHolding&&!Blocked){int s=NearestFreeSlot(player.transform.position,out float d);if(s>=0&&d<1.9f&&Mathf.Abs(player.transform.position.y-transform.position.y)<2.2f)text="E — взяться за ворот подъёмного моста";}
        if(text!=null)GUI.Box(new Rect((Screen.width-640)/2,Screen.height-185,640,52),text);
    }
}
}
