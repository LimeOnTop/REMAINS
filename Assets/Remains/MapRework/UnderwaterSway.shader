// Lit low-poly colour for underwater life. Mode 0 (kelp): object-space height drives a slow two-axis sway.
// Mode 1 (fish): the rear of the body (object -Z) wags sideways, faster the further back.
Shader "Remains/UnderwaterSway" {
 Properties { _BaseColor("Colour",Color)=(1,1,1,1) _Mode("0 kelp, 1 fish",Float)=0 _Height("Kelp height",Float)=2.4 _Sway("Sway",Float)=.35 _Speed("Speed",Float)=.8 }
 SubShader { Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass { Tags{"LightMode"="UniversalForward"} Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial) float4 _BaseColor;float _Mode,_Height,_Sway,_Speed; CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
   V vert(A i){UNITY_SETUP_INSTANCE_ID(i);V o;float3 p=i.p.xyz;float3 origin=TransformObjectToWorld(float3(0,0,0));float ph=origin.x*.37+origin.z*.53;
    if(_Mode<.5){float h=saturate(p.y/_Height);float k=h*h;float t=_Time.y*_Speed+ph;
     p.x+=(sin(t)+.35*sin(t*2.3+p.y*1.7))*_Sway*k;p.z+=(cos(t*.8+.7)+.3*sin(t*1.9+p.y*2.1))*_Sway*.7*k;}
    else{float back=saturate(-p.z/.17);p.x+=sin(_Time.y*_Speed*9+ph+p.z*18)*.035*back*back;}
    float3 w=TransformObjectToWorld(p);o.w=w;o.p=TransformWorldToHClip(w);o.n=TransformObjectToWorldNormal(i.n);return o;}
   half4 frag(V i):SV_Target{float3 n=normalize(cross(ddy(i.w),ddx(i.w)));if(dot(n,i.n)<0)n=-n;Light l=GetMainLight(TransformWorldToShadowCoord(i.w));
    float nd=saturate(dot(n,l.direction));return half4(_BaseColor.rgb*(.5+.5*nd*l.shadowAttenuation),1);}
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}
