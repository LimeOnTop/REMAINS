// Stylized rock (style V8): flat facets, world-aligned moss that settles on upward faces and breaks up with noise,
// broad colour mottling, darker foot where the rock meets the ground and a cool tint in shadow.
Shader "Remains/RockV8" {
 Properties { _BaseColor("Stone",Color)=(.5,.48,.43,1) _MossColor("Moss",Color)=(.34,.43,.18,1) _Moss("Moss amount",Range(0,1))=.7
  _ShadowTint("Shadow tint",Color)=(.55,.62,.72,1) _FootDark("Ground contact darkening",Range(0,1))=.35 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass { Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct A { float4 p:POSITION; float3 n:NORMAL; };
 struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 n:TEXCOORD1; float oy:TEXCOORD2; };
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor,_MossColor,_ShadowTint; float _Moss,_FootDark;
 CBUFFER_END
 float hash(float2 p){ return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
 float vnoise(float2 p){ float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
  return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+float2(1,1)),f.x),f.y); }
 V vert(A i){V o;float3 w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(w);o.world=w;o.n=TransformObjectToWorldNormal(i.n);o.oy=i.p.y;return o;}
 half4 frag(V i):SV_Target{
  float3 n=normalize(cross(ddy(i.world),ddx(i.world)));if(dot(n,i.n)<0)n=-n;
  float3 w=i.world;float mott=vnoise(w.xz*.35+w.y*.2)*.6+vnoise(w.xz*1.4-w.y*.9)*.4;
  float3 col=_BaseColor.rgb*(.86+.28*mott);
  // Moss from the sky side: upward facets plus noise so the edge is organic, more on lower humid rocks.
  float mn=vnoise(w.xz*1.1+w.y*.5);
  float moss=smoothstep(.5,.78,n.y+(mn-.5)*.5)*_Moss;
  col=lerp(col,_MossColor.rgb*(.85+.3*mn),moss);
  col*=1-_FootDark*(1-smoothstep(0,.35,i.oy));
  Light l=GetMainLight(TransformWorldToShadowCoord(w));float nd=saturate(dot(n,l.direction))*l.shadowAttenuation;
  float3 amb=col*_ShadowTint.rgb*.55+SampleSH(n)*col*.3;
  float3 sun=col*lerp(half3(1,1,1),l.color,.4)*(.55+.55*nd);
  return half4(lerp(amb,sun,smoothstep(0,.35,nd)*.6+nd*.4),1);}
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
