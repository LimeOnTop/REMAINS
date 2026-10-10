using UnityEngine;
namespace Remains {
// Tints the view with a short blue-green fog while the camera is below the river surface.
[RequireComponent(typeof(Camera))]
public class UnderwaterView : MonoBehaviour {
    public float waterLevel=7f;
    public Color fogColor=new Color(.07f,.36f,.40f);
    public float density=.085f;
    bool under,saved;bool fog;Color color;FogMode mode;float dens;
    void LateUpdate(){
        var p=transform.position;bool now=p.y<waterLevel-.05f&&Physics.Raycast(p+Vector3.up*.1f,Vector3.down,out var hit,30,~0,QueryTriggerInteraction.Ignore)&&hit.point.y<waterLevel-.1f&&!Physics.Raycast(p,Vector3.up,waterLevel-p.y,~0,QueryTriggerInteraction.Ignore);
        if(now==under)return;under=now;
        if(under){if(!saved){fog=RenderSettings.fog;color=RenderSettings.fogColor;mode=RenderSettings.fogMode;dens=RenderSettings.fogDensity;saved=true;}
            RenderSettings.fog=true;RenderSettings.fogColor=fogColor;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=density;}
        else if(saved){RenderSettings.fog=fog;RenderSettings.fogColor=color;RenderSettings.fogMode=mode;RenderSettings.fogDensity=dens;}
    }
    void OnDisable(){if(under&&saved){RenderSettings.fog=fog;RenderSettings.fogColor=color;RenderSettings.fogMode=mode;RenderSettings.fogDensity=dens;under=false;}}
}
}
