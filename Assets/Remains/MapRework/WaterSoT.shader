// Stylised river water in the spirit of Sea of Thieves: Gerstner swell, depth-tinted transparency with refraction,
// sun glints, fresnel sky reflection, crest highlights and broken shoreline foam. Transparent, no depth write.
Shader "Remains/WaterSoT" {
 Properties {
  _ShallowColor("Shallow",Color)=(.25,.85,.76,1) _DeepColor("Deep",Color)=(.03,.33,.46,1) _SkyColor("Sky reflection",Color)=(.62,.80,.92,1)
  _FoamColor("Foam",Color)=(.95,.98,.96,1) _DepthRange("Colour depth (m)",Float)=5 _Visibility("Visibility (m)",Float)=4.2
  _WaveHeight("Wave height",Float)=.16 _WaveLength("Wave length",Float)=9 _WaveSpeed("Wave speed",Float)=1.1
  _Refraction("Refraction",Range(0,.2))=.045 _FoamDistance("Shore foam (m)",Float)=.9 _Gloss("Gloss",Float)=180
 }
 SubShader {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass {
   Tags{"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _ShallowColor,_DeepColor,_SkyColor,_FoamColor;float _DepthRange,_Visibility,_WaveHeight,_WaveLength,_WaveSpeed,_Refraction,_FoamDistance,_Gloss;
   CBUFFER_END
   struct A{float4 p:POSITION;};
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float4 s:TEXCOORD2;float crest:TEXCOORD3;};
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float vnoise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+float2(1,1)),f.x),f.y);}
   // One Gerstner wave: displacement and its contribution to the tangent frame.
   void Gerstner(float2 dir,float steep,float len,float spd,float3 p,inout float3 o,inout float3 t,inout float3 b){
    float k=6.2831/len;float c=sqrt(9.8/k)*spd;float2 d=normalize(dir);float f=k*(dot(d,p.xz)-c*_Time.y);float a=steep/k;
    o+=float3(d.x*a*cos(f),a*sin(f),d.y*a*cos(f));
    t+=float3(-d.x*d.x*steep*sin(f),d.x*steep*cos(f),-d.x*d.y*steep*sin(f));
    b+=float3(-d.x*d.y*steep*sin(f),d.y*steep*cos(f),-d.y*d.y*steep*sin(f));
   }
   V vert(A i){V o;float3 w=TransformObjectToWorld(i.p.xyz);float3 off=0,t=float3(1,0,0),b=float3(0,0,1);
    float st=_WaveHeight*6.2831/_WaveLength;
    Gerstner(float2(1,.35),st,_WaveLength,_WaveSpeed,w,off,t,b);
    Gerstner(float2(-.4,1),st*.7,_WaveLength*.57,_WaveSpeed,w,off,t,b);
    Gerstner(float2(.7,-.8),st*.45,_WaveLength*.31,_WaveSpeed,w,off,t,b);
    w+=off;o.w=w;o.n=normalize(cross(b,t));o.p=TransformWorldToHClip(w);o.s=ComputeScreenPos(o.p);o.crest=off.y/_WaveHeight;return o;}
   half4 frag(V i,bool front:SV_IsFrontFace):SV_Target{
    float2 suv=i.s.xy/i.s.w;
    // Seen from below: a bright, rippling ceiling of light.
    if(!front){float rp=vnoise(i.w.xz*.8+_Time.y*.3);return half4(lerp(_ShallowColor.rgb*1.15,_SkyColor.rgb,.35+.2*rp),.72);}
    // Small-scale ripples perturb the normal (two scrolling noise layers).
    float2 q=i.w.xz*.9;float t=_Time.y;
    float n1=vnoise(q+float2(t*.35,t*.22)),n2=vnoise(q*2.3-float2(t*.28,-t*.31));
    float3 n=normalize(i.n+float3((n1-.5)*.35+(n2-.5)*.2,0,(n2-.5)*.35-(n1-.5)*.2));
    float sceneEye=LinearEyeDepth(SampleSceneDepth(suv),_ZBufferParams);float surfEye=LinearEyeDepth(i.p.z,_ZBufferParams);
    float depth=max(0,sceneEye-surfEye);
    // Refraction: offset the opaque grab by the ripple normal, but never sample things in front of the water.
    float2 ruv=suv+n.xz*_Refraction*saturate(depth);
    float rEye=LinearEyeDepth(SampleSceneDepth(ruv),_ZBufferParams);if(rEye<surfEye)ruv=suv;
    float rDepth=max(0,LinearEyeDepth(SampleSceneDepth(ruv),_ZBufferParams)-surfEye);
    float3 below=SampleSceneColor(ruv);
    float dt=saturate(rDepth/_DepthRange);
    float3 water=lerp(_ShallowColor.rgb,_DeepColor.rgb,dt);
    float clarity=exp(-rDepth/_Visibility);
    float3 col=lerp(water,below*lerp(float3(1,1,1),_ShallowColor.rgb*1.3,.55),clarity*.85);
    Light l=GetMainLight(TransformWorldToShadowCoord(i.w));
    float3 v=normalize(GetWorldSpaceViewDir(i.w));
    float fres=pow(1-saturate(dot(n,v)),4);
    col=lerp(col,_SkyColor.rgb,fres*.6);
    // Light passing through wave crests: the bright green-blue glow of stylised sea water.
    col+=_ShallowColor.rgb*saturate(i.crest)*.25*(1-dt*.5);
    col*=lerp(.75,1,l.shadowAttenuation);
    float3 h=normalize(l.direction+v);float spec=pow(saturate(dot(n,h)),_Gloss);
    col+=l.color*smoothstep(.45,.55,spec)*.9*l.shadowAttenuation;
    // Shoreline foam: bands broken by noise, pulsing with the swell; thin foam on crests.
    float fn=vnoise(i.w.xz*1.6+float2(t*.2,0))*.6+vnoise(i.w.xz*4.1-t*.3)*.4;
    float shore=1-saturate(depth/_FoamDistance);
    float band=step(1-shore,fn*.9+.1*sin(t*1.5+depth*6));
    float crestFoam=step(.93,saturate(i.crest*.5+.5)*fn*1.2);
    float foam=saturate(band*shore*1.4+crestFoam*.8);
    col=lerp(col,_FoamColor.rgb,foam);
    // The refracted scene is already blended in, so alpha only softens the very edge of the waterline.
    return half4(col,saturate(depth*5+foam*.5));
   }
   ENDHLSL
  }
 }
}
