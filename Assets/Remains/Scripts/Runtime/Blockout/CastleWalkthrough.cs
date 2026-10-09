using UnityEngine;
using UnityEngine.InputSystem;
namespace Remains {
[RequireComponent(typeof(CharacterController))]
public class CastleWalkthrough : MonoBehaviour {
    public Camera viewCamera;
    public Vector3 spawn;
    CharacterController controller;
    float pitch, vertical;
    void Start() {
        controller=GetComponent<CharacterController>();
        foreach(var c in Camera.allCameras) if(c!=viewCamera)c.enabled=false;
        viewCamera.enabled=true;
        Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
    }
    void Update() {
        var k=Keyboard.current; var m=Mouse.current; if(k==null)return;
        if(SiegeDirector.BlockLook)return;
        if(k.escapeKey.wasPressedThisFrame){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        if(m!=null && m.leftButton.wasPressedThisFrame){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        if(Cursor.lockState==CursorLockMode.Locked && m!=null){
            Vector2 d=m.delta.ReadValue()*.10f; transform.Rotate(0,d.x,0);
            pitch=Mathf.Clamp(pitch-d.y,-85,85);viewCamera.transform.localEulerAngles=new Vector3(pitch,0,0);
        }
        if(SiegeDirector.BlockMovement||DrawbridgeCapstan.LocalHolding)return;
        float x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0), z=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);
        Vector3 input=Vector3.ClampMagnitude(new Vector3(x,0,z),1);
        if(BattlefieldClimbRope.TryClimb(this,controller,k.wKey.isPressed)){vertical=0;return;}
        if(controller.isGrounded){vertical=-2;if(k.spaceKey.wasPressedThisFrame)vertical=6;}
        vertical+=Physics.gravity.y*2*Time.deltaTime;
        controller.Move((transform.TransformDirection(input)*(k.leftShiftKey.isPressed?9:5.5f)+Vector3.up*vertical)*Time.deltaTime);
        if(k.rKey.wasPressedThisFrame||transform.position.y < -5){controller.enabled=false;transform.position=spawn;controller.enabled=true;vertical=0;}
        if(k.fKey.wasPressedThisFrame)foreach(var bridge in FindObjectsByType<BlockoutDrawbridge>(FindObjectsSortMode.None))
            if(bridge.enabled&&bridge.NearControl(transform.position))bridge.Toggle();
        if(k.eKey.wasPressedThisFrame)foreach(var gate in FindObjectsByType<BlockoutGate>(FindObjectsSortMode.None))
            if(!SiegeDirector.Instance&&Vector3.Distance(transform.position,gate.transform.position)<22)gate.Toggle();
    }
    void OnGUI(){
        if(SiegeDirector.Instance)return;
        GUI.Box(new Rect(15,15,680,65),"CASTLE BLOCKOUT • 100 player layout\nWASD: move  |  Mouse: look  |  Shift: run  |  Space: jump\nE: gates  |  F near winch: drawbridge  |  R: return to bridge  |  Esc: release cursor");
    }
    void OnDisable(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
}
}
