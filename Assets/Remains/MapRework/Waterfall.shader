// Waterfall ribbon: water colour with foam streaks scrolling down (uv.y grows downward) and a foamy base.
Shader "Remains/Waterfall" {
 Properties { _Color("Water",Color)=(.18,.62,.66,1) _Foam("Foam",Color)=(.95,.98,.96,1) _Speed("Speed",Float)=1.6 }
 SubShader { Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass { Tags{"LightMode"="UniversalForward"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial) float4 _Color,_Foam;float _Speed; CBUFFER_END
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float vnoise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+float2(1,1)),f.x),f.y);}
   V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
   half4 frag(V i):SV_Target{float t=_Time.y*_Speed;float s=vnoise(float2(i.uv.x*14,i.uv.y*3-t*2))*.6+vnoise(float2(i.uv.x*31,i.uv.y*7-t*3.1))*.4;
    float streak=step(.62,s);float top=1-smoothstep(0,.08,i.uv.y);float edge=smoothstep(0,.06,i.uv.x)*smoothstep(1,.94,i.uv.x);
    float3 c=lerp(_Color.rgb*(1+.15*s),_Foam.rgb,saturate(streak*.8+top));float a=lerp(.75,.95,streak)*edge*(1-smoothstep(.8,1,i.uv.y));return half4(c,a);}
   ENDHLSL
  }
 }
}
