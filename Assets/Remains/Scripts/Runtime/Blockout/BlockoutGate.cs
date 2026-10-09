using UnityEngine;
namespace Remains {
public class BlockoutGate : MonoBehaviour {
    public Transform leftHinge,rightHinge;
    public bool open=true;
    public float openAngle=100;   // leaves must stop short of the passage walls
    public void Toggle(){open=!open;}
    void Update(){
        leftHinge.localRotation=Quaternion.RotateTowards(leftHinge.localRotation,Quaternion.Euler(0,open?-openAngle:0,0),60*Time.deltaTime);
        rightHinge.localRotation=Quaternion.RotateTowards(rightHinge.localRotation,Quaternion.Euler(0,open?openAngle:0,0),60*Time.deltaTime);
    }
}
}
