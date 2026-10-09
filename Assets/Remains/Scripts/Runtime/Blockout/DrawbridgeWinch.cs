using UnityEngine;
namespace Remains {
// Drives the drawbridge leaf from the capstans: the bridge moves only while someone winds, at a speed
// proportional to the number of people on the bars. With both chains broken SiegeBridge takes over.
[RequireComponent(typeof(BlockoutDrawbridge))]
public class DrawbridgeWinch : MonoBehaviour {
    BlockoutDrawbridge bridge;SiegeBridge siege;
    [Tooltip("Debug: degrees per second applied without anyone at the capstans (0 = off).")]public float debugRate;
    void Awake(){bridge=GetComponent<BlockoutDrawbridge>();siege=GetComponent<SiegeBridge>();bridge.manual=true;}
    void Update(){
        if(siege&&(siege.broken[0]||siege.broken[1]))return;
        float rate=debugRate;
        foreach(var c in DrawbridgeCapstan.All)rate+=c.Input*c.Operators*c.degreesPerSecondPerOperator;
        if(rate!=0)bridge.ApplyPose(bridge.angle+rate*Time.deltaTime);
        bridge.raised=bridge.angle>BlockoutDrawbridge.MaxAngle*.5f;
    }
}
}
